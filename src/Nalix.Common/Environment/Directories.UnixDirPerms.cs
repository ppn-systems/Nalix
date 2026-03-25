// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Environment;

public static partial class Directories
{
    /// <summary>
    /// Recommended Unix directory permissions for common directory types.
    /// </summary>
    private enum UnixDirPerms : byte
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
    private static string ENSURE_AND_HARDEN(
        [System.Diagnostics.CodeAnalysis.DisallowNull] string path,
        UnixDirPerms perms = UnixDirPerms.Default755)
    {
        ENSURE_DIRECTORY_EXISTS(path);
        HARDEN_PERMISSIONS(path, perms);
        return path;
    }

    /// <summary>
    /// Creates a directory if it does not already exist. Thread safe.
    /// </summary>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="System.IO.IOException"></exception>
    [System.ComponentModel.EditorBrowsable(
        System.ComponentModel.EditorBrowsableState.Never)]
    private static void ENSURE_DIRECTORY_EXISTS(
        [System.Diagnostics.CodeAnalysis.DisallowNull] string path,
        [System.Runtime.CompilerServices.CallerMemberName] string callerMemberName = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int callerLineNumber = 0)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentNullException(nameof(path));
        }

        path = System.IO.Path.GetFullPath(path);

        DirectoryLock.EnterReadLock();
        try
        {
            if (System.IO.Directory.Exists(path))
            {
                return;
            }
        }
        finally
        {
            DirectoryLock.ExitReadLock();
        }

        DirectoryLock.EnterWriteLock();
        try
        {
            if (!System.IO.Directory.Exists(path))
            {
                _ = System.IO.Directory.CreateDirectory(path);
                RAISE_DIRECTORY_CREATED(path);
            }
        }
        catch (Exception ex)
        {
            string msg =
                "Failed to create directory: " + path + ". ERROR: " + ex.Message +
                " (Caller: " + callerMemberName + " at " + nameof(Directories) + ":" + callerLineNumber + ")";
            throw new System.IO.IOException(msg, ex);
        }
        finally
        {
            DirectoryLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Attempts to set secure permissions on the specified directory depending on the platform. Fails silently if cannot be set.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(
        System.ComponentModel.EditorBrowsableState.Never)]
    private static void HARDEN_PERMISSIONS(
        [System.Diagnostics.CodeAnalysis.DisallowNull] string path, UnixDirPerms perms)
    {
        try
        {
            if (OperatingSystem.IsWindows())
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
                    UnixDirPerms.Default755 => throw new NotImplementedException(),
                    _ =>
                                            System.IO.UnixFileMode.UserRead |
                                            System.IO.UnixFileMode.UserWrite |
                                            System.IO.UnixFileMode.UserExecute |
                                            System.IO.UnixFileMode.GroupRead |
                                            System.IO.UnixFileMode.GroupExecute |
                                            System.IO.UnixFileMode.OtherRead |
                                            System.IO.UnixFileMode.OtherExecute,
                };

                _ = SET_UNIX_FILE_MODE_COMPAT(path, mode);
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
        [System.Diagnostics.CodeAnalysis.DisallowNull] string path)
    {
        Action<string> handlers = DirectoryCreated;
        if (handlers == null)
        {
            return;
        }

        Delegate[] invocationList = handlers.GetInvocationList();
        for (int i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action<string>)invocationList[i]).Invoke(path);
            }
            catch { }
        }
    }

    /// <summary>
    /// Combines and normalizes a child path under a base directory,
    /// preventing directory traversal outside of the base directory.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException"></exception>
    [System.ComponentModel.EditorBrowsable(
        System.ComponentModel.EditorBrowsableState.Never)]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static string COMBINE_SAFE(
        [System.Diagnostics.CodeAnalysis.DisallowNull] string baseDir,
        [System.Diagnostics.CodeAnalysis.DisallowNull] string name)
    {
        string full = System.IO.Path.GetFullPath(System.IO.Path.Join(baseDir, name));
        string baseFull = System.IO.Path.GetFullPath(baseDir);

        if (!baseFull.EndsWith(System.IO.Path.DirectorySeparatorChar))
        {
            baseFull += System.IO.Path.DirectorySeparatorChar;
        }

        string rel = System.IO.Path.GetRelativePath(baseFull, full);

        char sep = System.IO.Path.DirectorySeparatorChar;
        StringComparison comp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return System.IO.Path.IsPathRooted(rel) ||
            rel.Equals("..", comp) ||
            rel.StartsWith(".." + sep, comp)
            ? throw new UnauthorizedAccessException($"Path '{name}' escapes base directory.")
            : full;
    }

    [System.ComponentModel.EditorBrowsable(
        System.ComponentModel.EditorBrowsableState.Never)]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool SET_UNIX_FILE_MODE_COMPAT(
        [System.Diagnostics.CodeAnalysis.DisallowNull] string path, System.IO.UnixFileMode mode)
    {
        try
        {
            System.Reflection.MethodInfo m = typeof(System.IO.Directory).GetMethod("SetUnixFileMode",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null, [typeof(string), typeof(System.IO.UnixFileMode)], null);

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
            if (!OperatingSystem.IsWindows())
            {
                uint native = TO_NATIVE_CHMOD_MODE(mode);
                int rc = CHMOD(path, native);
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
    private static uint TO_NATIVE_CHMOD_MODE(System.IO.UnixFileMode mode)
    {
        uint m = 0;

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

    /// <summary>
    /// P/Invoke libc chmod (fallback for Unix)
    /// </summary>
    [System.Runtime.CompilerServices.SkipLocalsInit]
    [System.ComponentModel.EditorBrowsable(
        System.ComponentModel.EditorBrowsableState.Never)]
    private static unsafe int CHMOD([System.Diagnostics.CodeAnalysis.DisallowNull] string pathname, uint mode)
    {
        byte* __pathname_native = default;
        int __retVal = 0;
        System.Runtime.InteropServices.Marshalling.Utf8StringMarshaller.ManagedToUnmanagedIn __pathname_native__marshaller = default;
        int __lastError;

        try
        {
            Span<byte> buffer = stackalloc byte[System.Runtime.InteropServices.Marshalling.Utf8StringMarshaller.ManagedToUnmanagedIn.BufferSize];
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
        static extern int __PInvoke(byte* __pathname_native, uint __mode_native);
    }
}