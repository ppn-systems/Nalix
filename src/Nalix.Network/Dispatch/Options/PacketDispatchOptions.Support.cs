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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private System.Func<TPacket, IConnection, System.Threading.Tasks.Task> CreateHandlerDelegate(
        System.Reflection.MethodInfo method, System.Object controllerInstance)
    {
        PacketDescriptor attributes = GetPacketAttributes(method);
        return async (packet, connection) =>
        {
            try
            {
                // Permission check
                if (!CheckPermission(connection, attributes))
                {
                    connection.Tcp.Send(TPacket.Create(0, ProtocolErrorTexts.PermissionDenied));
                    return;
                }

                // Method invocation
                System.Object? result = await InvokeMethod(method, controllerInstance, packet, connection, attributes);

                // Handle result
                System.Func<System.Object?, TPacket, IConnection,
                    System.Threading.Tasks.Task> handler = ResolveHandlerDelegate(method.ReturnType);

                await handler(result, packet, connection).ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
                await HandleException(ex, controllerInstance, method, attributes, connection);
            }
            finally
            {
                packet.Dispose();
            }
        };
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static bool CheckPermission(
        IConnection connection,
        PacketDescriptor attributes)
        => attributes.Permission?.Level <= connection.Level;

    private async System.Threading.Tasks.Task<System.Object?> InvokeMethod(
        System.Reflection.MethodInfo method,
        System.Object controllerInstance,
        TPacket packet,
        IConnection connection,
        PacketDescriptor attributes)
    {
        System.Object[] parameters = [packet, connection];

        if (attributes.Timeout is not null)
        {
            using System.Threading.CancellationTokenSource cts = new(attributes.Timeout.TimeoutMilliseconds);
            try
            {
                return await System.Threading.Tasks.Task.Run(
                    () => method.Invoke(controllerInstance, parameters), cts.Token);
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

    private async System.Threading.Tasks.Task HandleException(
        System.Exception ex, System.Object controllerInstance,
        System.Reflection.MethodInfo method, PacketDescriptor attributes, IConnection connection)
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