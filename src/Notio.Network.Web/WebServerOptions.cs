using Notio.Common.Enums;
using Notio.Network.Web.Enums;
using Notio.Network.Web.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace Notio.Network.Web;

/// <summary>
/// Contains options for configuring an instance of <see cref="WebServer"/>.
/// </summary>
public sealed class WebServerOptions : WebServerOptionsBase
{
    private const string NetShLogSource = "NetSh";

    private readonly List<string> _urlPrefixes = [];

    private HttpListenerMode _mode = HttpListenerMode.Notio;

    private X509Certificate2? _certificate;

    private string? _certificateThumbprint;

    private bool _autoLoadCertificate;

    private bool _autoRegisterCertificate;

    private StoreName _storeName = StoreName.My;

    private StoreLocation _storeLocation = StoreLocation.LocalMachine;

    /// <summary>
    /// Gets the URL prefixes.
    /// </summary>
    public IReadOnlyList<string> UrlPrefixes => _urlPrefixes;

    /// <summary>
    /// Gets or sets the type of HTTP listener.
    /// </summary>
    /// <exception cref="InvalidOperationException">This property is being set,
    /// and this instance's configuration is locked.</exception>
    /// <seealso cref="HttpListenerMode"/>
    public HttpListenerMode Mode
    {
        get => _mode;
        set
        {
            EnsureConfigurationNotLocked();
            _mode = value;
        }
    }

    /// <summary>
    /// Gets or sets the X.509 certificate to use for SSL connections.
    /// </summary>
    /// <exception cref="InvalidOperationException">This property is being set,
    /// and this instance's configuration is locked.</exception>
    public X509Certificate2? Certificate
    {
        get => AutoRegisterCertificate
                ? TryRegisterCertificate() ? _certificate : null
                : _certificate ?? (AutoLoadCertificate ? LoadCertificate() : null);
        set
        {
            EnsureConfigurationNotLocked();
            _certificate = value;
        }
    }

    /// <summary>
    /// Gets or sets the thumbprint of the X.509 certificate to use for SSL connections.
    /// </summary>
    /// <exception cref="InvalidOperationException">This property is being set,
    /// and this instance's configuration is locked.</exception>
    public string? CertificateThumbprint
    {
        get => _certificateThumbprint;
        set
        {
            EnsureConfigurationNotLocked();

            // strip any non-hexadecimal values and make uppercase
            _certificateThumbprint = value == null
                ? null
                : Regex.Replace(value, @"[^\da-fA-F]", string.Empty).ToUpper(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to automatically load the X.509 certificate.
    /// </summary>
    /// <exception cref="InvalidOperationException">This property is being set,
    /// and this instance's configuration is locked.</exception>
    /// <exception cref="PlatformNotSupportedException">This property is being set to <see langword="true"/>
    /// and the underlying operating system is not Windows.</exception>
    public bool AutoLoadCertificate
    {
        get => _autoLoadCertificate;
        set
        {
            EnsureConfigurationNotLocked();
            if (value && RuntimeInfo.OS != OSType.Windows)
            {
                throw new PlatformNotSupportedException("AutoLoadCertificate functionality is only available under Windows.");
            }

            _autoLoadCertificate = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to automatically bind the X.509 certificate
    /// to the port used for HTTPS.
    /// </summary>
    /// <exception cref="InvalidOperationException">This property is being set,
    /// and this instance's configuration is locked.</exception>
    /// <exception cref="PlatformNotSupportedException">This property is being set to <see langword="true"/>
    /// and the underlying operating system is not Windows.</exception>
    public bool AutoRegisterCertificate
    {
        get => _autoRegisterCertificate;
        set
        {
            EnsureConfigurationNotLocked();
            if (value && RuntimeInfo.OS != OSType.Windows)
            {
                throw new PlatformNotSupportedException("AutoRegisterCertificate functionality is only available under Windows.");
            }

            _autoRegisterCertificate = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating the X.509 certificate store where to load the certificate from.
    /// </summary>
    /// <exception cref="InvalidOperationException">This property is being set,
    /// and this instance's configuration is locked.</exception>
    /// <seealso cref="System.Security.Cryptography.X509Certificates.StoreName"/>
    public StoreName StoreName
    {
        get => _storeName;
        set
        {
            EnsureConfigurationNotLocked();
            _storeName = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating the location of the X.509 certificate store where to load the certificate from.
    /// </summary>
    /// <exception cref="InvalidOperationException">This property is being set,
    /// and this instance's configuration is locked.</exception>
    /// <seealso cref="System.Security.Cryptography.X509Certificates.StoreLocation"/>
    public StoreLocation StoreLocation
    {
        get => _storeLocation;
        set
        {
            EnsureConfigurationNotLocked();
            _storeLocation = value;
        }
    }

    /// <summary>
    /// Adds a URL prefix.
    /// </summary>
    /// <param name="urlPrefix">The URL prefix.</param>
    /// <exception cref="InvalidOperationException">This instance's configuration is locked.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="urlPrefix"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <para><paramref name="urlPrefix"/> is the empty string.</para>
    /// <para>- or -</para>
    /// <para><paramref name="urlPrefix"/> is already registered.</para>
    /// </exception>
    public void AddUrlPrefix(string urlPrefix)
    {
        EnsureConfigurationNotLocked();

        urlPrefix = Validate.NotNullOrEmpty(nameof(urlPrefix), urlPrefix);
        if (_urlPrefixes.Contains(urlPrefix))
        {
            throw new ArgumentException("URL prefix is already registered.", nameof(urlPrefix));
        }

        _urlPrefixes.Add(urlPrefix);
    }

    private X509Certificate2? LoadCertificate()
    {
        if (RuntimeInfo.OS != OSType.Windows)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(_certificateThumbprint))
        {
            return GetCertificate(_certificateThumbprint);
        }

        using Process netsh = GetNetsh("show");

        string? thumbprint = null;

        netsh.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            Trace.WriteLine($"[ERROR] {e.Data}", NetShLogSource);
        };

        netsh.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            Trace.WriteLine($"[ERROR] {e.Data}", NetShLogSource);

            string line = e.Data.Trim();

            if (line.StartsWith("Certificate Hash") && line.IndexOf(':', StringComparison.Ordinal) > -1)
            {
                thumbprint = line.Split(':')[1].Trim();
            }
        };

        if (!netsh.Start())
        {
            return null;
        }

        netsh.BeginOutputReadLine();
        netsh.BeginErrorReadLine();
        netsh.WaitForExit();

        return netsh.ExitCode == 0 && !string.IsNullOrEmpty(thumbprint)
            ? GetCertificate(thumbprint)
            : null;
    }

    private X509Certificate2? GetCertificate(string? thumbprint = null)
    {
        if (string.IsNullOrEmpty(thumbprint ?? _certificateThumbprint))
        {
            return null;
        }

        using X509Store store = new(StoreName, StoreLocation);
        store.Open(OpenFlags.ReadOnly);
        X509Certificate2Collection signingCert = store.Certificates.Find(
            X509FindType.FindByThumbprint,
            thumbprint ?? _certificateThumbprint!,
            false);
        return signingCert.Count == 0 ? null : signingCert[0];
    }

    private bool AddCertificateToStore()
    {
        if (_certificate == null)
        {
            throw new InvalidOperationException("Certificate cannot be null when adding to store.");
        }

        using X509Store store = new(StoreName, StoreLocation);
        try
        {
            store.Open(OpenFlags.ReadWrite);
            store.Add(_certificate);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryRegisterCertificate()
    {
        if (RuntimeInfo.OS != OSType.Windows)
        {
            return false;
        }

        if (_certificate == null)
        {
            throw new InvalidOperationException("A certificate is required to AutoRegister");
        }

        if (GetCertificate(_certificate.Thumbprint) == null && !AddCertificateToStore())
        {
            throw new InvalidOperationException(
                "The provided certificate cannot be added to the default store, add it manually");
        }

        using Process netsh = GetNetsh("add", $"certhash={_certificate.Thumbprint} appid={{adaa04bb-8b63-4073-a12f-d6f8c0b4383f}}");

        StringBuilder sb = new();

        void PushLine(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            _ = sb.AppendLine(e.Data);
            Trace.WriteLine($"[ERROR] {e.Data}", NetShLogSource);
        }

        netsh.OutputDataReceived += PushLine;

        netsh.ErrorDataReceived += PushLine;

        if (!netsh.Start())
        {
            return false;
        }

        netsh.BeginOutputReadLine();
        netsh.BeginErrorReadLine();
        netsh.WaitForExit();

        return netsh.ExitCode == 0 ? true : throw new InvalidOperationException($"NetSh error: {sb}");
    }

    private int GetSslPort()
    {
        int port = 443;

        foreach (string? url in UrlPrefixes.Where(x =>
            x.StartsWith("https:", StringComparison.OrdinalIgnoreCase)))
        {
            Match match = Regex.Match(url, @":(\d+)");

            if (match.Success && int.TryParse(match.Groups[1].Value, out port))
            {
                break;
            }
        }

        return port;
    }

    private Process GetNetsh(string verb, string options = "")
    {
        return new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                Arguments = $"http {verb} sslcert ipport=0.0.0.0:{GetSslPort()} {options}",
            },
        };
    }
}
