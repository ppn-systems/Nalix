namespace Notio.Cryptography.Hashing;

/// <summary>
/// Provides SHA-family cryptographic constants used in hash computations.
/// </summary>
public static class SHA
{
    /// <summary>
    /// The SHA-1 round constants (Ka values) used in the message expansion and compression functions.
    /// </summary>
    /// <remarks>
    /// There are four constants corresponding to different rounds of the SHA-1 process:
    /// - `0x5A827999` for rounds 0-19
    /// - `0x6ED9EBA1` for rounds 20-39
    /// - `0x8F1BBCDC` for rounds 40-59
    /// - `0xCA62C1D6` for rounds 60-79
    /// </remarks>
    public static readonly uint[] K1 = [0x5A827999, 0x6ED9EBA1, 0x8F1BBCDC, 0xCA62C1D6];

    /// <summary>
    /// The initial hash values (H0-H4) as defined in the SHA-1 specification.
    /// These values are used as the starting state of the hash computation.
    /// </summary>
    public static readonly uint[] H1 = [0x67452301, 0xEFCDAB89, 0x98BADCFE, 0x10325476, 0xC3D2E1F0];

    /// <summary>
    /// SHA-224 round constants (Ka), also used for SHA-256.
    /// </summary>
    /// <remarks>
    /// These constants are derived from the fractional parts of the cube roots of the first 64 prime numbers.
    /// </remarks>
    public static readonly uint[] K224 =
    [
        0X428A2F98, 0X71374491, 0XB5C0FBCF, 0XE9B5DBA5, 0X3956C25B, 0X59F111F1, 0X923F82A4, 0XAB1C5ED5,
        0XD807AA98, 0X12835B01, 0X243185BE, 0X550C7DC3, 0X72BE5D74, 0X80DEB1FE, 0X9BDC06A7, 0XC19BF174,
        0XE49B69C1, 0XEFBE4786, 0X0FC19DC6, 0X240CA1CC, 0X2DE92C6F, 0X4A7484AA, 0X5CB0A9DC, 0X76F988DA,
        0X983E5152, 0XA831C66D, 0XB00327C8, 0XBF597FC7, 0XC6E00BF3, 0XD5A79147, 0X06CA6351, 0X14292967,
        0X27B70A85, 0X2E1B2138, 0X4D2C6DFC, 0X53380D13, 0X650A7354, 0X766A0ABB, 0X81C2C92E, 0X92722C85,
        0XA2BFE8A1, 0XA81A664B, 0XC24B8B70, 0XC76C51A3, 0XD192E819, 0XD6990624, 0XF40E3585, 0X106AA070,
        0X19A4C116, 0X1E376C08, 0X2748774C, 0X34B0BCB5, 0X391C0CB3, 0X4ED8AA4A, 0X5B9CCA4F, 0X682E6FF3,
        0X748F82EE, 0X78A5636F, 0X84C87814, 0X8CC70208, 0X90BEFFFA, 0XA4506CEB, 0XBEF9A3F7, 0XC67178F2
    ];

    /// <summary>
    /// SHA-224 initial hash values (H0-H7).
    /// These values are used as the starting state of the SHA-224 hash computation.
    /// </summary>
    public static readonly uint[] H224 =
    [
        0XC1059ED8, 0X367CD507, 0X3070DD17, 0XF70E5939,
        0XFFC00B31, 0X68581511, 0X64F98FA7, 0XBEFA4FA4
    ];

    /// <summary>
    /// SHA-256 round constants (Ka).
    /// </summary>
    /// <remarks>
    /// SHA-256 uses the same constants as SHA-224.
    /// </remarks>
    public static readonly uint[] K256 = K224;

    /// <summary>
    /// SHA-256 initial hash values (H0-H7).
    /// These values are used as the starting state of the SHA-256 hash computation.
    /// </summary>
    public static readonly uint[] H256 =
    [
        0x6A09E667, 0xBB67AE85, 0x3C6EF372, 0xA54FF53A,
        0x510E527F, 0x9B05688C, 0x1F83D9AB, 0x5BE0CD19
    ];
}
