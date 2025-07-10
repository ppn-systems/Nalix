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
    /// <param name="callerLineNumber">The line Number in the source file at which the method is called.</param>
    private static void EnsureDirectoryExists(string path,
        [System.Runtime.CompilerServices.CallerMemberName] string callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int callerLineNumber = 0)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new System.ArgumentNullException(nameof(path));

        bool created = false;

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
                System.IO.Directory.CreateDirectory(path);
                created = true;
            }
        }
        catch (System.Exception ex)
        {
            string errorMessage = $"Failed to create directory: {path}. Error: {ex.Message} " +
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