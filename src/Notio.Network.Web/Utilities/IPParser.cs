using Notio.Common.Logging;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Notio.Network.Web.Utilities;

/// <summary>
/// Provides standard methods to parse IP address strings.
/// </summary>
public static class IpParser
{
    /// <summary>
    /// Parses the specified IP address.
    /// </summary>
    /// <param name="address">The IP address.</param>
    /// <returns>A collection of <see cref="IPAddress"/> parsed correctly from <paramref name="address"/>.</returns>
    public static async Task<IEnumerable<IPAddress>> ParseAsync(string? address)
    {
        if (address == null)
        {
            return [];
        }

        if (IPAddress.TryParse(address, out IPAddress? ip))
        {
            return [ip];
        }

        try
        {
            return await Dns.GetHostAddressesAsync(address).ConfigureAwait(false);
        }
        catch (SocketException socketEx)
        {
            socketEx.Log(nameof(IpParser));
        }
        catch
        {
            // Ignore
        }

        return IsCidrNotation(address) ? ParseCidrNotation(address) : IsSimpleIpRange(address) ? TryParseSimpleIpRange(address) : [];
    }

    /// <summary>
    /// Determines whether the IP-range string is in CIDR notation.
    /// </summary>
    /// <param name="range">The IP-range string.</param>
    /// <returns>
    ///   <c>true</c> if the IP-range string is CIDR notation; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsCidrNotation(string range)
    {
        if (string.IsNullOrWhiteSpace(range))
        {
            return false;
        }

        string[] parts = range.Split('/');
        if (parts.Length != 2)
        {
            return false;
        }

        string prefix = parts[0];
        string prefixLen = parts[1];

        string[] prefixParts = prefix.Split('.');
        return prefixParts.Length == 4 && byte.TryParse(prefixLen, out byte len) && len <= 32;
    }

    /// <summary>
    /// Parse IP-range string in CIDR notation. For example "12.15.0.0/16".
    /// </summary>
    /// <param name="range">The IP-range string.</param>
    /// <returns>A collection of <see cref="IPAddress"/> parsed correctly from <paramref name="range"/>.</returns>
    public static IEnumerable<IPAddress> ParseCidrNotation(string range)
    {
        if (!IsCidrNotation(range))
        {
            return [];
        }

        string[] parts = range.Split('/');
        string prefix = parts[0];

        if (!byte.TryParse(parts[1], out byte prefixLen))
        {
            return [];
        }

        string[] prefixParts = prefix.Split('.');
        if (prefixParts.Select(x => byte.TryParse(x, out _)).Any(x => !x))
        {
            return [];
        }

        uint ip = 0;
        for (int i = 0; i < 4; i++)
        {
            ip <<= 8;
            ip += uint.Parse(prefixParts[i], NumberFormatInfo.InvariantInfo);
        }

        byte shiftBits = (byte)(32 - prefixLen);
        uint ip1 = ip >> shiftBits << shiftBits;

        if ((ip1 & ip) != ip1) // Check correct subnet address
        {
            return [];
        }

        uint ip2 = ip1 >> shiftBits;
        for (int k = 0; k < shiftBits; k++)
        {
            ip2 = (ip2 << 1) + 1;
        }

        byte[] beginIp = new byte[4];
        byte[] endIp = new byte[4];

        for (int i = 0; i < 4; i++)
        {
            beginIp[i] = (byte)(ip1 >> (3 - i) * 8 & 255);
            endIp[i] = (byte)(ip2 >> (3 - i) * 8 & 255);
        }

        return GetAllIpAddresses(beginIp, endIp);
    }

    /// <summary>
    /// Determines whether the IP-range string is in simple IP range notation.
    /// </summary>
    /// <param name="range">The IP-range string.</param>
    /// <returns>
    ///   <c>true</c> if the IP-range string is in simple IP range notation; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsSimpleIpRange(string range)
    {
        if (string.IsNullOrWhiteSpace(range))
        {
            return false;
        }

        string[] parts = range.Split('.');
        if (parts.Length != 4)
        {
            return false;
        }

        foreach (string part in parts)
        {
            string[] rangeParts = part.Split('-');
            if (rangeParts.Length is < 1 or > 2)
            {
                return false;
            }

            if (!byte.TryParse(rangeParts[0], out _) ||
                rangeParts.Length > 1 && !byte.TryParse(rangeParts[1], out _))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Tries to parse IP-range string "12.15-16.1-30.10-255"
    /// </summary>
    /// <param name="range">The IP-range string.</param>
    /// <returns>A collection of <see cref="IPAddress"/> parsed correctly from <paramref name="range"/>.</returns>
    public static IEnumerable<IPAddress> TryParseSimpleIpRange(string range)
    {
        if (!IsSimpleIpRange(range))
        {
            return [];
        }

        byte[] beginIp = new byte[4];
        byte[] endIp = new byte[4];

        string[] parts = range.Split('.');
        for (int i = 0; i < 4; i++)
        {
            string[] rangeParts = parts[i].Split('-');
            beginIp[i] = byte.Parse(rangeParts[0], NumberFormatInfo.InvariantInfo);
            endIp[i] = rangeParts.Length == 1 ? beginIp[i] : byte.Parse(rangeParts[1], NumberFormatInfo.InvariantInfo);
        }

        return GetAllIpAddresses(beginIp, endIp);
    }

    /// <inheritdoc />
    public static List<IPAddress> GetAllIpAddresses(byte[] beginIp, byte[] endIp)
    {
        for (int i = 0; i < 4; i++)
        {
            if (endIp[i] < beginIp[i])
            {
                return [];
            }
        }

        int capacity = 1;
        for (int i = 0; i < 4; i++)
        {
            capacity *= endIp[i] - beginIp[i] + 1;
        }

        List<IPAddress> ips = new(capacity);
        for (int i0 = beginIp[0]; i0 <= endIp[0]; i0++)
        {
            for (int i1 = beginIp[1]; i1 <= endIp[1]; i1++)
            {
                for (int i2 = beginIp[2]; i2 <= endIp[2]; i2++)
                {
                    for (int i3 = beginIp[3]; i3 <= endIp[3]; i3++)
                    {
                        ips.Add(new IPAddress([(byte)i0, (byte)i1, (byte)i2, (byte)i3]));
                    }
                }
            }
        }

        return ips;
    }
}
