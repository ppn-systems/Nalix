using System.Runtime.CompilerServices;

internal static class TimingWheelTestSetup
{
    [ModuleInitializer]
    internal static void Init()
    {
        var taskManager = new global::Nalix.Framework.Tasks.TaskManager();
        global::Nalix.Framework.Injection.InstanceManager.Instance.Register<global::Nalix.Abstractions.Concurrency.ITaskManager>(taskManager);
        
        try
        {
            var config = new global::Nalix.Framework.Options.ObjectPoolOptions { EnableMetrics = false, EnableDiagnostics = false };
            var poolManager = new global::Nalix.Framework.Memory.Objects.ObjectPoolManager(config);
            global::Nalix.Framework.Memory.Objects.ObjectPoolManager.Configure(poolManager);
            global::Nalix.Framework.Injection.InstanceManager.Instance.Register<global::Nalix.Abstractions.IObjectPoolManager>(poolManager);
            global::Nalix.Framework.Injection.InstanceManager.Instance.Register<global::Nalix.Framework.Memory.Objects.ObjectPoolManager>(poolManager);
        }
        catch (System.InvalidOperationException) { }

        RuntimeHelpers.RunClassConstructor(typeof(global::Nalix.Network.Connections.Connection).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(global::Nalix.Network.Connections.WebSocketConnection).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(global::Nalix.Network.Connections.PassthroughConnection).TypeHandle);
    }
}
