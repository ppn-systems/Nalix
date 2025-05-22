using Nalix.Common.Attributes;
using Nalix.Common.Connection;
using Nalix.Common.Connection.Protocols;
using Nalix.Common.Exceptions;
using Nalix.Common.Package;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket,
    IPacketFactory<TPacket>,
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>
{
    /// <summary>
    /// Defines metadata and behavior for a packet.
    /// </summary>
    /// <param name="Opcode">Unique identifier for the packet type.</param>
    /// <param name="Timeout">Optional response timeout.</param>
    /// <param name="RateLimit">Optional sending rate limit.</param>
    /// <param name="Permission">Optional required privileges.</param>
    /// <param name="Encryption">Optional encryption setting.</param>
    private record PacketDescriptor(
        PacketOpcodeAttribute Opcode,
        PacketTimeoutAttribute? Timeout,
        PacketRateLimitAttribute? RateLimit,
        PacketPermissionAttribute? Permission,
        PacketEncryptionAttribute? Encryption
    );

    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static T EnsureNotNull<T>(T value, string paramName) where T : class
        => value ?? throw new System.ArgumentNullException(paramName);

    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static PacketDescriptor GetPacketAttributes(System.Reflection.MethodInfo method)
    => new(
        System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketOpcodeAttribute>(method)!,
        System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketTimeoutAttribute>(method),
        System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketRateLimitAttribute>(method),
        System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketPermissionAttribute>(method),
        System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketEncryptionAttribute>(method)
    );

    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void Failure(System.Type returnType, System.Exception ex)
        => _logger?.Error("Handler failed: {0} - {1}", returnType.Name, ex.Message);

    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static async System.Threading.Tasks.ValueTask DispatchPacketAsync(
        TPacket packet,
        IConnection connection)
    {
        packet = TPacket.Compress(packet);
        packet = TPacket.Encrypt(packet, connection.EncryptionKey, connection.Encryption);

        await connection.Tcp.SendAsync(packet);
    }

    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool CheckRateLimit(
        string remoteEndPoint,
        PacketDescriptor attributes)
    {
        if (attributes.RateLimit != null && !_rateLimiter.Check(
            remoteEndPoint, attributes.RateLimit))
        {
            return false;
        }
        return true;
    }

    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Func<TPacket, IConnection, System.Threading.Tasks.Task>
        CreateHandlerDelegate(System.Reflection.MethodInfo method, object controllerInstance)
    {
        PacketDescriptor attributes = PacketDispatchOptions<TPacket>.GetPacketAttributes(method);

        return async (packet, connection) =>
        {
            System.Diagnostics.Stopwatch? stopwatch = _isMetricsEnabled ? System.Diagnostics.Stopwatch.StartNew() : null;

            if (!this.CheckRateLimit(connection.RemoteEndPoint.ToString()!, attributes))
            {
                _logger?.Warn("Rate limit exceeded on '{0}' from {1}", method.Name, connection.RemoteEndPoint);
                connection.Tcp.Send(TPacket.Create(0, ProtocolMessage.RateLimited));

                return;
            }

            if (attributes.Permission?.Level > connection.Level)
            {
                _logger?.Warn("You do not have permission to perform this action.");
                connection.Tcp.Send(TPacket.Create(0, ProtocolMessage.PermissionDenied));

                return;
            }

            // Handle Compression (e.g., apply compression to packet)
            try { packet = TPacket.Decompress(packet); }
            catch (System.Exception ex)
            {
                _logger?.Error("Failed to decompress packet: {0}", ex.Message);
                connection.Tcp.Send(TPacket.Create(0, ProtocolMessage.ServerError));

                return;
            }

            if (attributes.Encryption?.IsEncrypted == true && !packet.IsEncrypted)
            {
                string message = $"Encrypted packet not allowed for command " +
                                 $"'{attributes.Opcode.Id}' " +
                                 $"from connection {connection.RemoteEndPoint}.";

                _logger?.Warn(message);
                connection.Tcp.Send(TPacket.Create(0, ProtocolMessage.PacketEncryption));

                return;
            }
            else
            {
                // Handle Encryption (e.g., apply encryption to packet)
                packet = TPacket.Decrypt(packet, connection.EncryptionKey, connection.Encryption);
            }

            try
            {
                object? result;

                // Cache method invocation with improved performance
                if (attributes.Timeout != null)
                {
                    using System.Threading.CancellationTokenSource cts = new(attributes.Timeout.TimeoutMilliseconds);
                    try
                    {
                        result = await System.Threading.Tasks.Task.Run(
                            () => method.Invoke(controllerInstance, [packet, connection]), cts.Token);
                    }
                    catch (System.OperationCanceledException)
                    {
                        _logger?.Error("Packet '{0}' timed out after {1}ms.",
                            attributes.Opcode.Id,
                            attributes.Timeout.TimeoutMilliseconds);
                        connection.Tcp.Send(TPacket.Create(0, ProtocolMessage.RequestTimeout));

                        return;
                    }
                }
                else
                {
                    result = method.Invoke(controllerInstance, [packet, connection]);
                }

                // Await the return result, could be ValueTask if method is synchronous
                await this.ResolveHandlerDelegate(method.ReturnType)(result, packet, connection).ConfigureAwait(false);
            }
            catch (PackageException ex)
            {
                _logger?.Error("Error occurred while processing packet id '{0}' in controller '{1}' (Method: '{2}'). " +
                               "Exception: {3}. Net: {4}, Exception Details: {5}",
                                attributes.Opcode.Id,             // Opcode ID
                                controllerInstance.GetType().Name,// ConnectionOps name
                                method.Name,                      // Method name
                                ex.GetType().Name,                // Exception type
                                connection.RemoteEndPoint,        // Connection details for traceability
                                ex.Message                        // Exception message itself
                );
                _errorHandler?.Invoke(ex, attributes.Opcode.Id);
                connection.Tcp.Send(TPacket.Create(0, ProtocolMessage.ServerError));
            }
            catch (System.Exception ex)
            {
                _logger?.Error("Packet [Id={0}] ({1}.{2}) threw {3}: {4} [Net: {5}]",
                    attributes.Opcode.Id,
                    controllerInstance.GetType().Name,
                    method.Name,
                    ex.GetType().Name,
                    ex.Message,
                    connection.RemoteEndPoint
                );
                _errorHandler?.Invoke(ex, attributes.Opcode.Id);
                connection.Tcp.Send(TPacket.Create(0, ProtocolMessage.ServerError));
            }
            finally
            {
                if (stopwatch is not null)
                {
                    stopwatch.Stop();
                    _metricsCallback?.Invoke($"{controllerInstance.GetType().Name}.{method.Name}", stopwatch.ElapsedMilliseconds);
                }
            }
        };
    }

    /// <summary>
    /// Determines the correct handler based on the method's return type.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Func<object?, TPacket, IConnection, System.Threading.Tasks.Task>
        ResolveHandlerDelegate(System.Type returnType)
        => returnType switch
        {
            System.Type t when t == typeof(void) => (_, _, _) =>
            {
                return System.Threading.Tasks.Task.CompletedTask;
            }
            ,
            System.Type t when t == typeof(byte[]) => async (result, _, connection) =>
            {
                if (result is byte[] data)
                    await connection.Tcp.SendAsync(data);
            }
            ,
            System.Type t when t == typeof(string) => async (result, _, connection) =>
            {
                if (result is string data)
                {
                    await connection.Tcp.SendAsync(TPacket.Create(0, data));
                }
            }
            ,
            System.Type t when t == typeof(System.Memory<byte>) => async (result, _, connection) =>
            {
                if (result is System.Memory<byte> memory)
                    await connection.Tcp.SendAsync(memory);
            }
            ,
            System.Type t when t == typeof(TPacket) => async (result, _, connection) =>
            {
                if (result is TPacket packet)
                    await PacketDispatchOptions<TPacket>.DispatchPacketAsync(packet, connection);
            }
            ,
            System.Type t when t == typeof(System.Threading.Tasks.ValueTask) => async (result, _, _) =>
            {
                if (result is System.Threading.Tasks.ValueTask task)
                {
                    try
                    {
                        await task;
                    }
                    catch (System.Exception ex) { this.Failure(returnType, ex); }
                }
            }
            ,
            System.Type t when t == typeof(System.Threading.Tasks.ValueTask<byte[]>) => async (result, _, connection) =>
            {
                if (result is System.Threading.Tasks.ValueTask<byte[]> task)
                {
                    try
                    {
                        byte[] data = await task;
                        await connection.Tcp.SendAsync(data);
                    }
                    catch (System.Exception ex) { this.Failure(returnType, ex); }
                }
            }
            ,
            System.Type t when t == typeof(System.Threading.Tasks.ValueTask<string>) => async (result, _, connection) =>
            {
                if (result is System.Threading.Tasks.ValueTask<string> task)
                {
                    try
                    {
                        string data = await task;
                        await connection.Tcp.SendAsync(TPacket.Create(0, data));
                    }
                    catch (System.Exception ex) { this.Failure(returnType, ex); }
                }
            }
            ,
            System.Type t when t == typeof(System.Threading.Tasks.ValueTask<System.Memory<byte>>)
            => async (result, _, connection) =>
            {
                if (result is System.Threading.Tasks.ValueTask<System.Memory<byte>> task)
                {
                    try
                    {
                        System.Memory<byte> memory = await task;
                        await connection.Tcp.SendAsync(memory);
                    }
                    catch (System.Exception ex) { this.Failure(returnType, ex); }
                }
            }
            ,
            System.Type t when t == typeof(System.Threading.Tasks.ValueTask<TPacket>)
            => async (result, _, connection) =>
            {
                if (result is System.Threading.Tasks.ValueTask<TPacket> task)
                {
                    try
                    {
                        TPacket packet = await task;
                        await PacketDispatchOptions<TPacket>.DispatchPacketAsync(packet, connection);
                    }
                    catch (System.Exception ex) { this.Failure(returnType, ex); }
                }
            }
            ,
            System.Type t when t == typeof(System.Threading.Tasks.Task)
            => async (result, _, _) =>
            {
                if (result is System.Threading.Tasks.Task task)
                {
                    try
                    {
                        await task;
                    }
                    catch (System.Exception ex) { this.Failure(returnType, ex); }
                }
            }
            ,
            System.Type t when t == typeof(System.Threading.Tasks.Task<byte[]>)
            => async (result, _, connection) =>
            {
                if (result is System.Threading.Tasks.Task<byte[]> task)
                {
                    try
                    {
                        byte[] data = await task;
                        await connection.Tcp.SendAsync(data);
                    }
                    catch (System.Exception ex) { this.Failure(returnType, ex); }
                }
            }
            ,
            System.Type t when t == typeof(System.Threading.Tasks.Task<string>)
            => async (result, _, connection) =>
            {
                if (result is System.Threading.Tasks.Task<string> task)
                {
                    try
                    {
                        string data = await task;
                        await connection.Tcp.SendAsync(TPacket.Create(0, data));
                    }
                    catch (System.Exception ex) { this.Failure(returnType, ex); }
                }
            }
            ,
            System.Type t when t == typeof(System.Threading.Tasks.Task<System.Memory<byte>>)
            => async (result, _, connection) =>
            {
                if (result is System.Threading.Tasks.Task<System.Memory<byte>> task)
                {
                    try
                    {
                        System.Memory<byte> memory = await task;
                        await connection.Tcp.SendAsync(memory);
                    }
                    catch (System.Exception ex) { this.Failure(returnType, ex); }
                }
            }
            ,
            System.Type t when t == typeof(System.Threading.Tasks.Task<TPacket>)
            => async (result, _, connection) =>
            {
                if (result is System.Threading.Tasks.Task<TPacket> task)
                {
                    try
                    {
                        TPacket packet = await task;
                        await PacketDispatchOptions<TPacket>.DispatchPacketAsync(packet, connection);
                    }
                    catch (System.Exception ex) { this.Failure(returnType, ex); }
                }
            }
            ,
            _ => (_, _, _) =>
            {
                _logger?.Warn("Unsupported return type: {0}", returnType.Name);
                return System.Threading.Tasks.Task.CompletedTask;
            }
        };
}
