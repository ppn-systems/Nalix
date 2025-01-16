using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;

namespace Notio.Network.Firewall
{
    public class JwtAuthenticator(string secretKey)
    {
        private readonly string _secretKey = secretKey;

        public static ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);

                // Simple validation for demo
                if (jwtToken.ValidTo < DateTime.UtcNow)
                    return null;

                var claims = jwtToken.Claims.ToList();
                return new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
            }
            catch
            {
                return null;
            }
        }
    }
}
