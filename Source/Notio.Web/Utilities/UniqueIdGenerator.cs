using System;

namespace Notio.Web.Utilities;

/// <summary>
/// <para>Generates locally unique string IDs, mainly for logging purposes.</para>
/// </summary>
public static class UniqueIdGenerator
{
    /// <summary>
    /// Generates and returns a unique ID.
    /// </summary>
    /// <returns>The generated ID.</returns>
    public static string GetNext()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..22];
    }
}