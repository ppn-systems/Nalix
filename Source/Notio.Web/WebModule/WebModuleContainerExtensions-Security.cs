using Notio.Web.Security;
using System;
using System.Collections.Generic;

namespace Notio.Web.WebModule;

public static partial class WebModuleContainerExtensions
{
    /// <summary>
    /// Creates an instance of <see cref="IPBanningModule" /> and adds it to a module container.
    /// </summary>
    /// <typeparam name="TContainer">The type of the module container.</typeparam>
    /// <param name="this">The <typeparamref name="TContainer" /> on which this method is called.</param>
    /// <param name="whiteList">A collection of valid IPs that never will be banned.</param>
    /// <param name="banMinutes">Minutes that an IP will remain banned.</param>
    /// <returns>
    ///   <paramref name="this" /> with an <see cref="IPBanningModule" /> added.
    /// </returns>
    public static TContainer WithIPBanning<TContainer>(this TContainer @this,
        IEnumerable<string>? whiteList = null,
        int banMinutes = IPBanningModule.DefaultBanMinutes)
        where TContainer : class, IWebModuleContainer
    {
        return WithIPBanning(@this, null, whiteList, banMinutes);
    }

    /// <summary>
    /// Creates an instance of <see cref="IPBanningModule" /> and adds it to a module container.
    /// </summary>
    /// <typeparam name="TContainer">The type of the module container.</typeparam>
    /// <param name="this">The <typeparamref name="TContainer" /> on which this method is called.</param>
    /// <param name="configure">The configure.</param>
    /// <param name="whiteList">A collection of valid IPs that never will be banned.</param>
    /// <param name="banMinutes">Minutes that an IP will remain banned.</param>
    /// <returns>
    ///   <paramref name="this" /> with an <see cref="IPBanningModule" /> added.
    /// </returns>
    public static TContainer WithIPBanning<TContainer>(this TContainer @this,
        Action<IPBanningModule>? configure,
        IEnumerable<string>? whiteList = null,
        int banMinutes = IPBanningModule.DefaultBanMinutes)
        where TContainer : class, IWebModuleContainer
    {
        return WithModule(@this, new IPBanningModule("/", whiteList, banMinutes), configure);
    }
}