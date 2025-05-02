namespace Nalix.Cryptography.Hashing;

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

    /// <summary>
    /// SHA-512 round constants (Ka), also used for SHA-512.
    /// </summary>
    /// <remarks>
    /// These constants are derived from the fractional parts of the cube roots of the first 64 prime numbers.
    /// </remarks>
    public static readonly ulong[] K512 =
    [
        0x428a2f98d728ae22UL, 0x7137449123ef65cdUL, 0xb5c0fbcfec4d3b2fUL, 0xe9b5dba58189dbbcUL,
        0x3956c25bf348b538UL, 0x59f111f1b605d019UL, 0x923f82a4af194f9bUL, 0xab1c5ed5da6d8118UL,
        0xd807aa98a3030242UL, 0x12835b0145706fbeUL, 0x243185be4ee4b28cUL, 0x550c7dc3d5ffb4e2UL,
        0x72be5d74f27b896fUL, 0x80deb1fe3b1696b1UL, 0x9bdc06a725c71235UL, 0xc19bf174cf692694UL,
        0xe49b69c19ef14ad2UL, 0xefbe4786384f25e3UL, 0x0fc19dc68b8cd5b5UL, 0x240ca1cc77ac9c65UL,
        0x2de92c6f592b0275UL, 0x4a7484aa6ea6e483UL, 0x5cb0a9dcbd41fbd4UL, 0x76f988da831153b5UL,
        0x983e5152ee66dfabUL, 0xa831c66d2db43210UL, 0xb00327c898fb213fUL, 0xbf597fc7beef0ee4UL,
        0xc6e00bf33da88fc2UL, 0xd5a79147930aa725UL, 0x06ca6351e003826fUL, 0x142929670a0e6e70UL,
        0x27b70a8546d22ffcUL, 0x2e1b21385c26c926UL, 0x4d2c6dfc5ac42aedUL, 0x53380d139d95b3dfUL,
        0x650a73548baf63deUL, 0x766a0abb3c77b2a8UL, 0x81c2c92e47edaee6UL, 0x92722c851482353bUL,
        0xa2bfe8a14cf10364UL, 0xa81a664bbc423001UL, 0xc24b8b70d0f89791UL, 0xc76c51a30654be30UL,
        0xd192e819d6ef5218UL, 0xd69906245565a910UL, 0xf40e35855771202aUL, 0x106aa07032bbd1b8UL,
        0x19a4c116b8d2d0c8UL, 0x1e376c085141ab53UL, 0x2748774cdf8eeb99UL, 0x34b0bcb5e19b48a8UL,
        0x391c0cb3c5c95a63UL, 0x4ed8aa4ae3418acbUL, 0x5b9cca4f7763e373UL, 0x682e6ff3d6b2b8a3UL,
        0x748f82ee5defb2fcUL, 0x78a5636f43172f60UL, 0x84c87814a1f0ab72UL, 0x8cc702081a6439ecUL,
        0x90befffa23631e28UL, 0xa4506cebde82bde9UL, 0xbef9a3f7b2c67915UL, 0xc67178f2e372532bUL,
        0xca273eceea26619cUL, 0xd186b8c721c0c207UL, 0xeada7dd6cde0eb1eUL, 0xf57d4f7fee6ed178UL,
        0x06f067aa72176fbaUL, 0x0a637dc5a2c898a6UL, 0x113f9804bef90daeUL, 0x1b710b35131c471bUL,
        0x28db77f523047d84UL, 0x32caab7b40c72493UL, 0x3c9ebe0a15c9bebcUL, 0x431d67c49c100d4cUL,
        0x4cc5d4becb3e42b6UL, 0x597f299cfc657e2aUL, 0x5fcb6fab3ad6faecUL, 0x6c44198c4a475817UL
    ];

    /// <summary>
    /// SHA-512 initial hash values (H0-H7).
    /// These values are used as the starting state of the SHA-512 hash computation.
    /// </summary>
    public static readonly ulong[] H512 =
    [
        0x6a09e667f3bcc908ul, 0xbb67ae8584caa73bul, 0x3c6ef372fe94f82bul, 0xa54ff53a5f1d36f1ul,
        0x510e527fade682d1ul, 0x9b05688c2b3e6c1ful, 0x1f83d9abfb41bd6bul, 0x5be0cd19137e2179ul
    ];
}
