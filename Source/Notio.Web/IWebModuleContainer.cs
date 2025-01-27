using Notio.Web.Utilities;
using System;

namespace Notio.Web
{
    /// <summary>
    /// Represents an object that contains a collection of <see cref="IWebModule"/> interfaces.
    /// </summary>
    public interface IWebModuleContainer : IDisposable
    {
        /// <summary>
        /// Gets the modules.
        /// </summary>
        /// <value>
        /// The modules.
        /// </value>
        IComponentCollection<IWebModule> Modules { get; }
    }
}