// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Environment;

public static partial class Directories
{
    /// <summary>
    /// Recommended Unix directory permissions for common directory types.
    /// </summary>
    private enum UnixDirPerms : System.Byte
    {
        Default755 = 0x01,
        Private700 = 0x02,
        Shared750 = 0x03,
        WorldReadable = 0x04
    }

    /// <summary>
    /// Ensures a directory exists and applies platform-appropriate permissions. Returns the normalized full path.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(
        System.ComponentModel.EditorBrowsableState.Never)]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.String ENSURE_AND_HARDEN(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.String path,
        UnixDirPerms perms = UnixDirPerms.Default755)
    {
        Directories.ENSURE_DIRECTORY_EXISTS(path);
        Directories.HARDEN_PERMISSIONS(path, perms);
        return path;
    }

    /// <summary>
    /// Creates a directory if it does not already exist. Thread safe.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(
        System.ComponentModel.EditorBrowsableState.Never)]
    private static void ENSURE_DIRECTORY_EXISTS(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.String path,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
    {
        if (System.String.IsNullOrWhiteSpace(path))
        {
            throw new System.ArgumentNullException(nameof(path));
        }

        path = System.IO.Path.GetFullPath(path);

        Directories.DirectoryLock.EnterReadLock();
        try
        {
            if (System.IO.Directory.Exists(path))
            {
                return;
            }
        }
        finally
        {
            Directories.DirectoryLock.ExitReadLock();
        }

        Directories.DirectoryLock.EnterWriteLock();
        try
        {
            if (!System.IO.Directory.Exists(path))
            {
                _ = System.IO.Directory.CreateDirectory(path);
                RAISE_DIRECTORY_CREATED(path);
            }
        }
        catch (System.Exception ex)
        {
            System.String msg =
                "Failed to create directory: " + path + ". ERROR: " + ex.Message +
                " (Caller: " + callerMemberName + " at " + nameof(Directories) + ":" + callerLineNumber + ")";
            throw new System.IO.IOException(msg, ex);
        }
        finally
        {
            Directories.DirectoryLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Attempts to set secure permissions on the specified directory depending on the platform. Fails silently if cannot be set.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(
        System.ComponentModel.EditorBrowsableState.Never)]
    private static void HARDEN_PERMISSIONS(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.String path, UnixDirPerms perms)
    {
        try
        {
            if (System.OperatingSystem.IsWindows())
            {
                System.IO.DirectoryInfo di = new(path);
                System.Security.AccessControl.DirectorySecurity ds = System.IO.FileSystemAclExtensions.GetAccessControl(di);

                System.Security.Principal.SecurityIdentifier users =
                    new(System.Security.Principal.WellKnownSidType.BuiltinUsersSid, null);

                System.Security.Principal.SecurityIdentifier admins =
                    new(System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid, null);

                System.Security.AccessControl.FileSystemAccessRule ruleUsers =
                    new(users,
                        System.Security.AccessControl.FileSystemRights.Modify,
                        System.Security.AccessControl.InheritanceFlags.ContainerInherit |
                        System.Security.AccessControl.InheritanceFlags.ObjectInherit,
                        System.Security.AccessControl.PropagationFlags.None,
                        System.Security.AccessControl.AccessControlType.Allow);

                System.Security.AccessControl.FileSystemAccessRule ruleAdmins =
                    new(admins,
                        System.Security.AccessControl.FileSystemRights.FullControl,
                        System.Security.AccessControl.InheritanceFlags.ContainerInherit |
                        System.Security.AccessControl.InheritanceFlags.ObjectInherit,
                        System.Security.AccessControl.PropagationFlags.None,
                        System.Security.AccessControl.AccessControlType.Allow);

                ds.AddAccessRule(ruleUsers);
                ds.AddAccessRule(ruleAdmins);
                System.IO.FileSystemAclExtensions.SetAccessControl(di, ds);
            }
            else
            {
                System.IO.UnixFileMode mode = perms switch
                {
                    UnixDirPerms.Private700 =>
                        System.IO.UnixFileMode.UserRead |
                        System.IO.UnixFileMode.UserWrite |
                        System.IO.UnixFileMode.UserExecute,
                    UnixDirPerms.Shared750 =>
                        System.IO.UnixFileMode.UserRead |
                        System.IO.UnixFileMode.UserWrite |
                        System.IO.UnixFileMode.UserExecute |
                        System.IO.UnixFileMode.GroupRead |
                        System.IO.UnixFileMode.GroupExecute,
                    UnixDirPerms.WorldReadable =>
                        System.IO.UnixFileMode.UserRead |
                        System.IO.UnixFileMode.UserWrite |
                        System.IO.UnixFileMode.UserExecute |
                        System.IO.UnixFileMode.GroupRead |
                        System.IO.UnixFileMode.GroupExecute |
                        System.IO.UnixFileMode.OtherRead |
                        System.IO.UnixFileMode.OtherExecute,
                    _ =>
                        System.IO.UnixFileMode.UserRead |
                        System.IO.UnixFileMode.UserWrite |
                        System.IO.UnixFileMode.UserExecute |
                        System.IO.UnixFileMode.GroupRead |
                        System.IO.UnixFileMode.GroupExecute |
                        System.IO.UnixFileMode.OtherRead |
                        System.IO.UnixFileMode.OtherExecute,
                };

                _ = Directories.SET_UNIX_FILE_MODE_COMPAT(path, mode);
            }
        }
        catch { }
    }

    /// <summary>
    /// Invokes directory created event safely per handler.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(
        System.ComponentModel.EditorBrowsableState.Never)]
    private static void RAISE_DIRECTORY_CREATED(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.String path)
    {
        System.Action<System.String> handlers = Directories.DirectoryCreated;
        if (handlers == null)
        {
            return;
        }

        System.Delegate[] invocationList = handlers.GetInvocationList();
        for (System.Int32 i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((System.Action<System.String>)invocationList[i]).Invoke(path);
            }
            catch { }
        }
    }

    /// <summary>
    /// Combines and normalizes a child path under a base directory,
    /// preventing directory traversal outside of the base directory.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(
        System.ComponentModel.EditorBrowsableState.Never)]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.String COMBINE_SAFE(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.String baseDir,
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.String name)
    {
        System.String full = System.IO.Path.GetFullPath(System.IO.Path.Join(baseDir, name));
        System.String baseFull = System.IO.Path.GetFullPath(baseDir);

        if (!baseFull.EndsWith(System.IO.Path.DirectorySeparatorChar))
        {
            baseFull += System.IO.Path.DirectorySeparatorChar;
        }

        System.String rel = System.IO.Path.GetRelativePath(baseFull, full);

        System.Char sep = System.IO.Path.DirectorySeparatorChar;
        var comp = System.OperatingSystem.IsWindows() ? System.StringComparison.OrdinalIgnoreCase : System.StringComparison.Ordinal;
        if (System.IO.Path.IsPathRooted(rel) ||
            rel.Equals("..", comp) ||
            rel.StartsWith(".." + sep, comp))
        {
            throw new System.UnauthorizedAccessException($"Path '{name}' escapes base directory.");
        }

        return full;
    }

    [System.ComponentModel.EditorBrowsable(
        System.ComponentModel.EditorBrowsableState.Never)]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean SET_UNIX_FILE_MODE_COMPAT(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.String path, System.IO.UnixFileMode mode)
    {
        try
        {
            System.Reflection.MethodInfo m = typeof(System.IO.Directory).GetMethod("SetUnixFileMode",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null, [typeof(System.String), typeof(System.IO.UnixFileMode)], null);

            if (m != null)
            {
                _ = m.Invoke(null, [path, mode]);
                return true;
            }
        }
        catch
        {
            // ignore; will fallback to chmod
        }

        // 2) Fallback to libc chmod on Unix
        try
        {
            if (!System.OperatingSystem.IsWindows())
            {
                System.UInt32 native = TO_NATIVE_CHMOD_MODE(mode);
                System.Int32 rc = CHMOD(path, native);
                // rc == 0 success; else errno in Marshal.GetLastWin32Error()
                return rc == 0;
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    [System.ComponentModel.EditorBrowsable(
        System.ComponentModel.EditorBrowsableState.Never)]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt32 TO_NATIVE_CHMOD_MODE(System.IO.UnixFileMode mode)
    {
        System.UInt32 m = 0;

        // Special bits
        if ((mode & System.IO.UnixFileMode.SetUser) != 0)
        {
            m |= 0x800; // 04000
        }

        if ((mode & System.IO.UnixFileMode.SetGroup) != 0)
        {
            m |= 0x400; // 02000
        }

        if ((mode & System.IO.UnixFileMode.StickyBit) != 0)
        {
            m |= 0x200; // 01000
        }

        // USER
        if ((mode & System.IO.UnixFileMode.UserRead) != 0)
        {
            m |= 0x100; // 0400
        }

        if ((mode & System.IO.UnixFileMode.UserWrite) != 0)
        {
            m |= 0x080; // 0200
        }

        if ((mode & System.IO.UnixFileMode.UserExecute) != 0)
        {
            m |= 0x040; // 0100
        }

        // Group
        if ((mode & System.IO.UnixFileMode.GroupRead) != 0)
        {
            m |= 0x020; // 0040
        }

        if ((mode & System.IO.UnixFileMode.GroupWrite) != 0)
        {
            m |= 0x010; // 0020
        }

        if ((mode & System.IO.UnixFileMode.GroupExecute) != 0)
        {
            m |= 0x008; // 0010
        }

        // Others
        if ((mode & System.IO.UnixFileMode.OtherRead) != 0)
        {
            m |= 0x004; // 0004
        }

        if ((mode & System.IO.UnixFileMode.OtherWrite) != 0)
        {
            m |= 0x002; // 0002
        }

        if ((mode & System.IO.UnixFileMode.OtherExecute) != 0)
        {
            m |= 0x001; // 0001
        }

        return m;
    }

    // P/Invoke libc chmod (fallback for Unix)
    [System.Runtime.CompilerServices.SkipLocalsInit]
    [System.ComponentModel.EditorBrowsable(
        System.ComponentModel.EditorBrowsableState.Never)]
    private static unsafe System.Int32 CHMOD([System.Diagnostics.CodeAnalysis.DisallowNull] System.String pathname, System.UInt32 mode)
    {
        System.Byte* __pathname_native = default;
        System.Int32 __retVal = 0;
        System.Runtime.InteropServices.Marshalling.Utf8StringMarshaller.ManagedToUnmanagedIn __pathname_native__marshaller = default;
        System.Int32 __lastError;

        try
        {
            System.Span<System.Byte> buffer = stackalloc System.Byte[System.Runtime.InteropServices.Marshalling.Utf8StringMarshaller.ManagedToUnmanagedIn.BufferSize];
#pragma warning disable CS9080
            __pathname_native__marshaller.FromManaged(pathname, buffer);
#pragma warning restore CS9080
            __pathname_native = __pathname_native__marshaller.ToUnmanaged();
            System.Runtime.InteropServices.Marshal.SetLastSystemError(0);
            __retVal = __PInvoke(__pathname_native, mode);
            __lastError = System.Runtime.InteropServices.Marshal.GetLastSystemError();
        }
        finally
        {
            __pathname_native__marshaller.Free();
        }

        System.Runtime.InteropServices.Marshal.SetLastPInvokeError(__lastError);
        return __retVal;

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "chmod", ExactSpelling = true)]
        static extern System.Int32 __PInvoke(System.Byte* __pathname_native, System.UInt32 __mode_native);
    }
}
