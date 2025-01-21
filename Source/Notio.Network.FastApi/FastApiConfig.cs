using Notio.Shared.Configuration;

namespace Notio.Network.FastApi;

public class FastApiConfig : ConfigurationBinder
{
    /// <summary>
    /// Gets the server's base URL.
    /// Default is "http://localhost:8080".
    /// </summary>
    public string UniformResourceLocator { get; set; } = "http://localhost:8080";
}
