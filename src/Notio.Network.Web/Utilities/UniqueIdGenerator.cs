using System;

namespace Notio.Network.Web.Utilities;

/// <summary>
/// <para>Generates locally unique string IDs, mainly for logging purposes.</para>
/// </summary>
public static class UniqueIdGenerator
{
    /// <summary>
    /// Generates and returns a unique Number.
    /// </summary>
    /// <returns>The generated Number.</returns>
    public static string GetNext()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..22];
    }
}