using Nalix.Common.Connection;
using Nalix.Common.Connection.Protocols;
using Nalix.Common.Exceptions;
using Nalix.Common.Package;
using Nalix.Common.Package.Attributes;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket,
    IPacketFactory<TPacket>,
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>
{
    /// <summary>
    /// Defines metadata and behavior for a packet.
    /// </summary>
    /// <param name="OpCode">Unique identifier for the packet type.</param>
    /// <param name="Timeout">Optional response timeout.</param>
    /// <param name="RateLimit">Optional sending rate limit.</param>
    /// <param name="Permission">Optional required privileges.</param>
    /// <param name="Encryption">Optional encryption setting.</param>
    private record PacketDescriptor(
        PacketOpcodeAttribute OpCode,
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
        TPacket workingPacket = packet;

        try
        {
            if (packet.IsCompression)
            {
                TPacket? compressed = TPacket.Compress(packet);
                workingPacket = compressed;

                if (packet.IsEncrypted)
                {
                    TPacket encrypted = TPacket.Encrypt(workingPacket, connection.EncryptionKey, connection.Encryption);
                    workingPacket = encrypted;
                }
            }
        }
        catch
        {
            if (packet.IsEncrypted)
            {
                TPacket encrypted = TPacket.Encrypt(packet, connection.EncryptionKey, connection.Encryption);
                workingPacket = encrypted;
            }
        }

        await connection.Tcp.SendAsync(workingPacket);
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

            if (!CheckRateLimit(connection.RemoteEndPoint.ToString()!, attributes))
            {
                _logger?.Warn("Rate limit exceeded on '{0}' from {1}", method.Name, connection.RemoteEndPoint);
                connection.Tcp.Send(TPacket.Create(0, ProtocolErrorTexts.RateLimited));
                return;
            }

            if (attributes.Permission?.Level > connection.Level)
            {
                _logger?.Warn("You do not have permission to perform this action.");
                connection.Tcp.Send(TPacket.Create(0, ProtocolErrorTexts.PermissionDenied));
                return;
            }

            if (!packet.IsEncrypted &&
                     attributes.Encryption?.IsEncrypted == true)
            {
                System.String message = $"Encrypted packet request for command " +
                                        $"'{attributes.OpCode.OpCode}' " +
                                        $"from connection {connection.RemoteEndPoint}.";

                _logger?.Warn(message);
                connection.Tcp.Send(TPacket.Create(0, ProtocolErrorTexts.PacketEncryption));
                return;
            }
            else if (packet.IsEncrypted && attributes.Encryption?.IsEncrypted == false)
            {
                System.String message = $"Encrypted packet not allowed for command " +
                                        $"'{attributes.OpCode.OpCode}' " +
                                        $"from connection {connection.RemoteEndPoint}.";

                _logger?.Warn(message);
                connection.Tcp.Send(TPacket.Create(0, ProtocolErrorTexts.PacketEncryption));
                return;
            }
            else if (packet.IsEncrypted && attributes.Encryption?.IsEncrypted == true)
            {
                try
                {
                    packet = TPacket.Decrypt(
                        packet,
                        connection.EncryptionKey,
                        attributes.Encryption!.AlgorithmType);
                }
                catch (System.Exception ex)
                {
                    _logger?.Error("Failed to Decrypt packet: {0}", ex.Message);
                    connection.Tcp.Send(TPacket.Create(0, ProtocolErrorTexts.ServerError));
                    return;
                }
            }

            try
            {
                try
                {
                    System.Object? result;

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
                                attributes.OpCode.OpCode,
                                attributes.Timeout.TimeoutMilliseconds);

                            connection.Tcp.Send(TPacket.Create(0, ProtocolErrorTexts.RequestTimeout));
                            return;
                        }
                    }
                    else
                    {
                        result = method.Invoke(controllerInstance, [packet, connection]);
                    }

                    // Await the return result, could be ValueTask if method is synchronous
                    await ResolveHandlerDelegate(method.ReturnType)(result, packet, connection).ConfigureAwait(false);
                }
                catch (PackageException ex)
                {
                    _logger?.Error("Error occurred while processing packet id '{0}' in controller '{1}' (Method: '{2}'). " +
                                   "Exception: {3}. Clients: {4}, Exception Details: {5}",
                                    attributes.OpCode.OpCode,
                                    controllerInstance.GetType().Name,
                                    method.Name,
                                    ex.GetType().Name,
                                    connection.RemoteEndPoint,
                                    ex.Message
                    );
                    _errorHandler?.Invoke(ex, attributes.OpCode.OpCode);
                    connection.Tcp.Send(TPacket.Create(0, ProtocolErrorTexts.ServerError));
                }
                catch (System.Exception ex)
                {
                    _logger?.Error("Packet [OpCode={0}] ({1}.{2}) threw {3}: {4} [Clients: {5}]",
                        attributes.OpCode.OpCode,
                        controllerInstance.GetType().Name,
                        method.Name,
                        ex.GetType().Name,
                        ex.Message,
                        connection.RemoteEndPoint
                    );

                    _errorHandler?.Invoke(ex, attributes.OpCode.OpCode);
                    connection.Tcp.Send(TPacket.Create(0, ProtocolErrorTexts.ServerError));
                }
            }
            finally
            {
                if (stopwatch is not null)
                {
                    stopwatch.Stop();
                    _metricsCallback?.Invoke($"{controllerInstance.GetType().Name}.{method.Name}", stopwatch.ElapsedMilliseconds);
                }

                packet.Dispose();
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
            System.Type t when t == typeof(System.Byte[]) => async (result, _, connection) =>
            {
                if (result is System.Byte[] data)
                    await connection.Tcp.SendAsync(data);
            }
            ,
            System.Type t when t == typeof(System.String) => async (result, _, connection) =>
            {
                if (result is System.String data)
                {
                    await connection.Tcp.SendAsync(TPacket.Create(0, data));
                }
            }
            ,
            System.Type t when t == typeof(System.Memory<System.Byte>) => async (result, _, connection) =>
            {
                if (result is System.Memory<System.Byte> memory)
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
                    catch (System.Exception ex) { Failure(returnType, ex); }
                }
            }
            ,
            System.Type t when t == typeof(System.Threading.Tasks.ValueTask<System.Byte[]>) => async (result, _, connection) =>
            {
                if (result is System.Threading.Tasks.ValueTask<System.Byte[]> task)
                {
                    try
                    {
                        System.Byte[] data = await task;
                        await connection.Tcp.SendAsync(data);
                    }
                    catch (System.Exception ex) { Failure(returnType, ex); }
                }
            }
            ,
            System.Type t when t == typeof(System.Threading.Tasks.ValueTask<System.String>) => async (result, _, connection) =>
            {
                if (result is System.Threading.Tasks.ValueTask<System.String> task)
                {
                    try
                    {
                        System.String data = await task;
                        using TPacket packet = TPacket.Create(0, data);
                        await connection.Tcp.SendAsync(packet.Serialize());
                    }
                    catch (System.Exception ex) { Failure(returnType, ex); }
                }
            }
            ,
            System.Type t when t == typeof(System.Threading.Tasks.ValueTask<System.Memory<System.Byte>>)
            => async (result, _, connection) =>
            {
                if (result is System.Threading.Tasks.ValueTask<System.Memory<System.Byte>> task)
                {
                    try
                    {
                        System.Memory<System.Byte> memory = await task;
                        await connection.Tcp.SendAsync(memory);
                    }
                    catch (System.Exception ex) { Failure(returnType, ex); }
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
                    catch (System.Exception ex) { Failure(returnType, ex); }
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
                    catch (System.Exception ex) { Failure(returnType, ex); }
                }
            }
            ,
            System.Type t when t == typeof(System.Threading.Tasks.Task<System.Byte[]>)
            => async (result, _, connection) =>
            {
                if (result is System.Threading.Tasks.Task<System.Byte[]> task)
                {
                    try
                    {
                        System.Byte[] data = await task;
                        await connection.Tcp.SendAsync(data);
                    }
                    catch (System.Exception ex) { Failure(returnType, ex); }
                }
            }
            ,
            System.Type t when t == typeof(System.Threading.Tasks.Task<System.String>)
            => async (result, _, connection) =>
            {
                if (result is System.Threading.Tasks.Task<System.String> task)
                {
                    try
                    {
                        System.String data = await task;
                        using TPacket packet = TPacket.Create(0, data);
                        await connection.Tcp.SendAsync(packet.Serialize());
                    }
                    catch (System.Exception ex) { Failure(returnType, ex); }
                }
            }
            ,
            System.Type t when t == typeof(System.Threading.Tasks.Task<System.Memory<System.Byte>>)
            => async (result, _, connection) =>
            {
                if (result is System.Threading.Tasks.Task<System.Memory<System.Byte>> task)
                {
                    try
                    {
                        System.Memory<System.Byte> memory = await task;
                        await connection.Tcp.SendAsync(memory);
                    }
                    catch (System.Exception ex) { Failure(returnType, ex); }
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
                    catch (System.Exception ex) { Failure(returnType, ex); }
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