using Notio.Network.Web.WebModule;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Web.Authentication;

/// <summary>
/// Simple HTTP basic authentication module that stores credentials
/// in a <seealso cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public class BasicAuthenticationModule : BasicAuthenticationModuleBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BasicAuthenticationModule"/> class.
    /// </summary>
    /// <param name="baseRoute">The base route.</param>
    /// <param name="realm">The authentication realm.</param>
    /// <remarks>
    /// <para>If <paramref name="realm"/> is <see langword="null"/> or the empty string,
    /// the <see cref="BasicAuthenticationModuleBase.Realm">Realm</see> property will be set equal to
    /// <see cref="IWebModule.BaseRoute">BaseRoute</see>.</para>
    /// </remarks>
    public BasicAuthenticationModule(string baseRoute, string? realm = null)
        : base(baseRoute, realm)
    {
    }

    /// <summary>
    /// Gets a dictionary of valid user names and passwords.
    /// </summary>
    /// <value>
    /// The accounts.
    /// </value>
    public ConcurrentDictionary<string, string> Accounts { get; } = new ConcurrentDictionary<string, string>(StringComparer.InvariantCulture);

    /// <inheritdoc />
    protected override Task<bool> VerifyCredentialsAsync(string path, string userName, string password, CancellationToken cancellationToken)
    {
        return Task.FromResult(VerifyCredentialsInternal(userName, password));
    }

    private bool VerifyCredentialsInternal(string userName, string password)
    {
        return userName != null
            && Accounts.TryGetValue(userName, out string? storedPassword)
            && string.Equals(password, storedPassword, StringComparison.Ordinal);
    }
}