using System.Security.Cryptography.X509Certificates;

namespace Notio.Network.Config
{
    public class SecurityConfig
    {
        public X509Certificate2? ServerCertificate { get; set; }
        public bool RequireClientCertificate { get; set; }

        public System.Security.Authentication.SslProtocols EnabledProtocols { get; set; } =
            System.Security.Authentication.SslProtocols.Tls12 |
            System.Security.Authentication.SslProtocols.Tls13;

        public bool CheckCertificateRevocation { get; set; } = true;
    }
}