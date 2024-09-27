using Nalix.Common.Connection;
using Nalix.Common.Connection.Protocols;
using Nalix.Common.Exceptions;
using Nalix.Common.Package;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket,
    IPacketFactory<TPacket>,
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private System.Func<TPacket, IConnection, Task> CreateHandlerDelegate(MethodInfo method, object controllerInstance)
        => CreateOptimizedHandler(method, controllerInstance, GetPacketAttributes(method));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private System.Func<TPacket, IConnection, Task> CreateOptimizedHandler(
        MethodInfo method,
        object controllerInstance,
        PacketDescriptor attributes)
    {
        return async (packet, connection) =>
        {
            var stopwatch = _isMetricsEnabled ? System.Diagnostics.Stopwatch.StartNew() : null;

            try
            {
                // Rate limiting check
                if (!CheckRateLimit(connection, attributes))
                {
                    connection.Tcp.Send(TPacket.Create(0, ProtocolErrorTexts.RateLimited));
                    return;
                }

                // Permission check
                if (!CheckPermission(connection, attributes))
                {
                    connection.Tcp.Send(TPacket.Create(0, ProtocolErrorTexts.PermissionDenied));
                    return;
                }

                // Encryption handling
                packet = (await this.ProcessEncryption(packet, connection, attributes))!;
                if (packet == null) return; // Error already sent

                // Method invocation
                System.Object? result = await InvokeMethod(method, controllerInstance, packet, connection, attributes);

                // Handle result
                var handler = ResolveHandlerDelegate(method.ReturnType);
                await handler(result, packet, connection).ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
                await HandleException(ex, controllerInstance, method, attributes, connection);
            }
            finally
            {
                if (stopwatch is not null)
                {
                    stopwatch.Stop();
                    _metricsCallback?.Invoke($"{controllerInstance.GetType().Name}.{method.Name}",
                                             stopwatch.ElapsedMilliseconds);
                }
                packet.Dispose();
            }
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CheckRateLimit(IConnection connection, PacketDescriptor attributes)
    {
        if (attributes.RateLimit is null) return true;

        System.String endPointStr = connection.RemoteEndPoint.ToString()!;
        unsafe
        {
            fixed (char* endPointPtr = endPointStr)
            {
                System.ReadOnlySpan<char> span = new(endPointPtr, endPointStr.Length);
                return _rateLimiter.Check(span.ToString(), attributes.RateLimit);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CheckPermission(IConnection connection, PacketDescriptor attributes)
        => attributes.Permission?.Level <= connection.Level;

    private async Task<TPacket?> ProcessEncryption(TPacket packet, IConnection connection, PacketDescriptor attributes)
    {
        if (attributes.Encryption is null) return packet;

        var isEncrypted = packet.IsEncrypted;
        var shouldBeEncrypted = attributes.Encryption.IsEncrypted;

        if (shouldBeEncrypted != isEncrypted)
        {
            await connection.Tcp.SendAsync(TPacket.Create(0, ProtocolErrorTexts.PacketEncryption));
            return default;
        }

        if (shouldBeEncrypted && isEncrypted)
        {
            try
            {
                return TPacket.Decrypt(packet, connection.EncryptionKey, attributes.Encryption.AlgorithmType);
            }
            catch (System.Exception ex)
            {
                _logger?.Error("Failed to decrypt packet: {0}", ex.Message);
                await connection.Tcp.SendAsync(TPacket.Create(0, ProtocolErrorTexts.PacketEncryption));
                return default;
            }
        }

        return packet;
    }

    private async Task<object?> InvokeMethod(
        MethodInfo method,
        object controllerInstance,
        TPacket packet,
        IConnection connection,
        PacketDescriptor attributes)
    {
        var parameters = new object[] { packet, connection };

        if (attributes.Timeout is not null)
        {
            using var cts = new CancellationTokenSource(attributes.Timeout.TimeoutMilliseconds);
            try
            {
                return await Task.Run(() => method.Invoke(controllerInstance, parameters), cts.Token);
            }
            catch (System.OperationCanceledException)
            {
                _logger?.Error("Packet '{0}' timed out after {1}ms.",
                               attributes.OpCode.OpCode, attributes.Timeout.TimeoutMilliseconds);

                await connection.Tcp.SendAsync(TPacket.Create(0, ProtocolErrorTexts.RequestTimeout));
                throw;
            }
        }

        return method.Invoke(controllerInstance, parameters);
    }

    private async Task HandleException(
        System.Exception ex, object controllerInstance,
        MethodInfo method, PacketDescriptor attributes, IConnection connection)
    {
        if (ex is PackageException packageEx)
        {
            _logger?.Error("Package exception in {0}.{1}: {2}",
                controllerInstance.GetType().Name, method.Name, packageEx.Message);
            _errorHandler?.Invoke(packageEx, attributes.OpCode.OpCode);
        }
        else
        {
            _logger?.Error("Handler exception in {0}.{1}: {2}",
                controllerInstance.GetType().Name, method.Name, ex.Message);
            _errorHandler?.Invoke(ex, attributes.OpCode.OpCode);
        }

        await connection.Tcp.SendAsync(TPacket.Create(0, ProtocolErrorTexts.ServerError));
    }
}