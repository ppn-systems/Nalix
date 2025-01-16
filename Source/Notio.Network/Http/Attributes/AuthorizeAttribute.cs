using System;

namespace Notio.Network.Http.Attributes;

/// <summary>
/// Specifies authorization requirements for a method.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AuthorizeAttribute"/> class.
/// </remarks>
/// <param name="roles">Roles required for authorization.</param>
/// <param name="permissions">Permissions required for authorization.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AuthorizeAttribute(string[]? roles = null, string[]? permissions = null) : Attribute
{
    /// <summary>
    /// List of required roles for authorization.
    /// </summary>
    public string[] Roles { get; } = roles?.Length > 0 ? roles : [];

    /// <summary>
    /// List of required permissions for authorization.
    /// </summary>
    public string[] Permissions { get; } = permissions?.Length > 0 ? permissions : [];
}
