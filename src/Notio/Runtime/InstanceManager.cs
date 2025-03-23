using System.Threading;

namespace Notio.Runtime;

/// <summary>
/// Manages instances of the application to ensure only one instance is running at a time.
/// </summary>
public static class InstanceManager
{
    private static readonly Lock SyncLock = new();

    private static readonly string ApplicationMutexName =
        "Global\\{{" + ApplicationInfo.EntryAssembly.FullName + "}}";

    /// <summary>
    /// Checks if this application (including version number) is the only instance currently running.
    /// </summary>
    public static bool IsTheOnlyInstance
    {
        get
        {
            lock (SyncLock)
            {
                try
                {
                    // Try to open an existing global mutex.
                    using Mutex existingMutex = Mutex.OpenExisting(ApplicationMutexName);
                }
                catch
                {
                    try
                    {
                        // If no mutex exists, create one. This instance is the only instance.
                        using Mutex appMutex = new(true, ApplicationMutexName);
                        return true;
                    }
                    catch
                    {
                        // In case mutex creation fails.
                    }
                }
                return false;
            }
        }
    }
}
