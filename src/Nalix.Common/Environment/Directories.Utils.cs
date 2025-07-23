using Nalix.Common.Exceptions;

namespace Nalix.Common.Environment;

public static partial class Directories
{
    #region Private Methods

    /// <summary>
    /// Ensures that a directory exists, creating it if necessary.
    /// Uses a reader-writer lock to ensure thread safety.
    /// </summary>
    /// <param name="path">The path of the directory to check or create.</param>
    /// <param name="callerMemberName">The method or property name of the caller.</param>
    /// <param name="callerFilePath">The path of the source file that contains the caller.</param>
    /// <param name="callerLineNumber">The line ProtocolType in the source file at which the method is called.</param>
    private static void EnsureDirectoryExists(System.String path,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
    {
        if (System.String.IsNullOrWhiteSpace(path))
        {
            throw new System.ArgumentNullException(nameof(path));
        }

        System.Boolean created = false;

        // First try with read lock to avoid contention
        DirectoryLock.EnterReadLock();
        try
        {
            if (System.IO.Directory.Exists(path))
            {
                return; // Directory already exists
            }
        }
        finally
        {
            DirectoryLock.ExitReadLock();
        }

        // Directory doesn't exist, acquire write lock and try to create
        DirectoryLock.EnterWriteLock();
        try
        {
            // Check again in case another thread created it while we were waiting
            if (!System.IO.Directory.Exists(path))
            {
                _ = System.IO.Directory.CreateDirectory(path);
                created = true;
            }
        }
        catch (System.Exception ex)
        {
            System.String errorMessage = $"Failed to create directory: {path}. Error: {ex.Message} " +
                                  $"(Called from {callerMemberName} at " +
                                  $"{System.IO.Path.GetFileName(callerFilePath)}:{callerLineNumber})";

            throw new InternalErrorException(errorMessage, ex);
        }
        finally
        {
            DirectoryLock.ExitWriteLock();
        }

        // Notify listeners about the directory creation
        if (created)
        {
            DirectoryCreated?.Invoke(path);
        }
    }

    #endregion Private Methods
}