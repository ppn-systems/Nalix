using Nalix.Common.Connection;
using Nalix.Common.Exceptions;
using Nalix.Common.Package;
using Nalix.Common.Package.Attributes;
using Nalix.Common.Package.Enums;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket,
    IPacketFactory<TPacket>,
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>
{
    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static T EnsureNotNull<T>(T value, string paramName) where T : class
        => value ?? throw new System.ArgumentNullException(paramName);

    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static PacketAttributes GetPacketAttributes(System.Reflection.MethodInfo method)
    => new(
        System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketIdAttribute>(method)!,
        System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketTimeoutAttribute>(method),
        System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketRateGroupAttribute>(method),
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

        await connection.SendAsync(packet);
    }

    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool CheckRateLimit(
        string remoteEndPoint,
        PacketAttributes attributes,
        System.Reflection.MethodInfo method)
    {
        if (attributes.RateLimit != null && !_rateLimiter.Check(
            remoteEndPoint, attributes.RateGroup?.GroupName ?? method.Name,
            attributes.RateLimit, attributes.RateGroup))
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
        PacketAttributes attributes = PacketDispatchOptions<TPacket>.GetPacketAttributes(method);

        return async (packet, connection) =>
        {
            System.Diagnostics.Stopwatch? stopwatch = _isMetricsEnabled ? System.Diagnostics.Stopwatch.StartNew() : null;

            if (!this.CheckRateLimit(connection.RemoteEndPoint, attributes, method))
            {
                _logger?.Warn("Rate limit exceeded on '{0}' from {1}", method.Name, connection.RemoteEndPoint);
                connection.Send(TPacket.Create(0, PacketCode.RateLimited));

                return;
            }

            if (attributes.Permission?.Level > connection.Level)
            {
                _logger?.Warn("You do not have permission to perform this action.");
                connection.Send(TPacket.Create(0, PacketCode.PermissionDenied));

                return;
            }

            // Handle Compression (e.g., apply compression to packet)
            try { packet = TPacket.Decompress(packet); }
            catch (System.Exception ex)
            {
                _logger?.Error("Failed to decompress packet: {0}", ex.Message);
                connection.Send(TPacket.Create(0, PacketCode.ServerError));

                return;
            }

            if (attributes.Encryption?.IsEncrypted == true && !packet.IsEncrypted)
            {
                string message = $"Encrypted packet not allowed for command " +
                                 $"'{attributes.PacketId.Id}' " +
                                 $"from connection {connection.RemoteEndPoint}.";

                _logger?.Warn(message);
                connection.Send(TPacket.Create(0, PacketCode.PacketEncryption));

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
                            attributes.PacketId.Id,
                            attributes.Timeout.TimeoutMilliseconds);
                        connection.Send(TPacket.Create(0, PacketCode.RequestTimeout));

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
                    attributes.PacketId.Id,           // Command ID
                    controllerInstance.GetType().Name,// ConnectionOps name
                    method.Name,                      // Method name
                    ex.GetType().Name,                // Exception type
                    connection.RemoteEndPoint,        // Connection details for traceability
                    ex.Message                        // Exception message itself
                );
                _errorHandler?.Invoke(ex, attributes.PacketId.Id);
                connection.Send(TPacket.Create(0, PacketCode.ServerError));
            }
            catch (System.Exception ex)
            {
                _logger?.Error("Packet [Id={0}] ({1}.{2}) threw {3}: {4} [Net: {5}]",
                    attributes.PacketId.Id,
                    controllerInstance.GetType().Name,
                    method.Name,
                    ex.GetType().Name,
                    ex.Message,
                    connection.RemoteEndPoint
                );
                _errorHandler?.Invoke(ex, attributes.PacketId.Id);
                connection.Send(TPacket.Create(0, PacketCode.ServerError));
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
                    await connection.SendAsync(data);
            }
            ,
            System.Type t when t == typeof(string) => async (result, _, connection) =>
            {
                if (result is string data)
                {
                    TPacket packet = TPacket.Create(
                        0, PacketCode.Success, PacketType.String,
                        PacketFlags.None, PacketPriority.Low, data);
                    await connection.SendAsync(packet);
                }
            }
            ,
            System.Type t when t == typeof(System.Memory<byte>) => async (result, _, connection) =>
            {
                if (result is System.Memory<byte> memory)
                    await connection.SendAsync(memory);
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
                        await connection.SendAsync(data);
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
                        await connection.SendAsync(memory);
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
                        await connection.SendAsync(data);
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
                        await connection.SendAsync(memory);
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
