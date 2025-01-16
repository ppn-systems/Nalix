using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notio.Http.Enums;

/// <summary>
/// Corresponds to the possible values of the SameSite attribute of the Set-Cookie header.
/// </summary>
public enum SameSite
{
    /// <summary>
    /// Indicates a browser should only send cookie for same-site requests.
    /// </summary>
    Strict,
    /// <summary>
    /// Indicates a browser should send cookie for cross-site requests only with top-level navigation. 
    /// </summary>
    Lax,
    /// <summary>
    /// Indicates a browser should send cookie for same-site and cross-site requests.
    /// </summary>
    None
}
