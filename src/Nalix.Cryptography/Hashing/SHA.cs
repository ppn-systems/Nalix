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
    public static readonly System.UInt32[] K1 = [0x5A827999, 0x6ED9EBA1, 0x8F1BBCDC, 0xCA62C1D6];

    /// <summary>
    /// The initial hash values (H0-H4) as defined in the SHA-1 specification.
    /// These values are used as the starting state of the hash computation.
    /// </summary>
    public static readonly System.UInt32[] H1 = [0x67452301, 0xEFCDAB89, 0x98BADCFE, 0x10325476, 0xC3D2E1F0];

    /// <summary>
    /// SHA-224 round constants (Ka), also used for SHA-256.
    /// </summary>
    /// <remarks>
    /// These constants are derived from the fractional parts of the cube roots of the first 64 prime numbers.
    /// </remarks>
    public static readonly System.UInt32[] K224 =
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
    public static readonly System.UInt32[] H224 =
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
    public static readonly System.UInt32[] K256 = K224;

    /// <summary>
    /// SHA-256 initial hash values (H0-H7).
    /// These values are used as the starting state of the SHA-256 hash computation.
    /// </summary>
    public static readonly System.UInt32[] H256 =
    [
        0x6A09E667, 0xBB67AE85, 0x3C6EF372, 0xA54FF53A,
        0x510E527F, 0x9B05688C, 0x1F83D9AB, 0x5BE0CD19
    ];

    /// <summary>
    /// SHA-384 initial hash values (H0-H7).
    /// These values are used as the starting state of the SHA-384 hash computation.
    /// </summary>
    public static readonly System.UInt64[] H384 =
    [
        0XCBBB9D5DC1059ED8, 0X629A292A367CD507, 0X9159015A3070DD17, 0X152FECD8F70E5939,
        0X67332667FFC00B31, 0X8EB44A8768581511, 0XDB0C2E0D64F98FA7, 0X47B5481DBEFA4FA4,
    ];

    /// <summary>
    /// SHA-512 round constants (Ka), also used for SHA-512.
    /// </summary>
    /// <remarks>
    /// These constants are derived from the fractional parts of the cube roots of the first 64 prime numbers.
    /// </remarks>
    public static readonly System.UInt64[] K384 =
    [
        0X428A2F98D728AE22UL, 0X7137449123EF65CDUL, 0XB5C0FBCFEC4D3B2FUL, 0XE9B5DBA58189DBBCUL,
        0X3956C25BF348B538UL, 0X59F111F1B605D019UL, 0X923F82A4AF194F9BUL, 0XAB1C5ED5DA6D8118UL,
        0XD807AA98A3030242UL, 0X12835B0145706FBEUL, 0X243185BE4EE4B28CUL, 0X550C7DC3D5FFB4E2UL,
        0X72BE5D74F27B896FUL, 0X80DEB1FE3B1696B1UL, 0X9BDC06A725C71235UL, 0XC19BF174CF692694UL,
        0XE49B69C19EF14AD2UL, 0XEFBE4786384F25E3UL, 0X0FC19DC68B8CD5B5UL, 0X240CA1CC77AC9C65UL,
        0X2DE92C6F592B0275UL, 0X4A7484AA6EA6E483UL, 0X5CB0A9DCBD41FBD4UL, 0X76F988DA831153B5UL,
        0X983E5152EE66DFABUL, 0XA831C66D2DB43210UL, 0XB00327C898FB213FUL, 0XBF597FC7BEEF0EE4UL,
        0XC6E00BF33DA88FC2UL, 0XD5A79147930AA725UL, 0X06CA6351E003826FUL, 0X142929670A0E6E70UL,
        0X27B70A8546D22FFCUL, 0X2E1B21385C26C926UL, 0X4D2C6DFC5AC42AEDUL, 0X53380D139D95B3DFUL,
        0X650A73548BAF63DEUL, 0X766A0ABB3C77B2A8UL, 0X81C2C92E47EDAEE6UL, 0X92722C851482353BUL,
        0XA2BFE8A14CF10364UL, 0XA81A664BBC423001UL, 0XC24B8B70D0F89791UL, 0XC76C51A30654BE30UL,
        0XD192E819D6EF5218UL, 0XD69906245565A910UL, 0XF40E35855771202AUL, 0X106AA07032BBD1B8UL,
        0X19A4C116B8D2D0C8UL, 0X1E376C085141AB53UL, 0X2748774CDF8EEB99UL, 0X34B0BCB5E19B48A8UL,
        0X391C0CB3C5C95A63UL, 0X4ED8AA4AE3418ACBUL, 0X5B9CCA4F7763E373UL, 0X682E6FF3D6B2B8A3UL,
        0X748F82EE5DEFB2FCUL, 0X78A5636F43172F60UL, 0X84C87814A1F0AB72UL, 0X8CC702081A6439ECUL,
        0X90BEFFFA23631E28UL, 0XA4506CEBDE82BDE9UL, 0XBEF9A3F7B2C67915UL, 0XC67178F2E372532BUL,
        0XCA273ECEEA26619CUL, 0XD186B8C721C0C207UL, 0XEADA7DD6CDE0EB1EUL, 0XF57D4F7FEE6ED178UL,
        0X06F067AA72176FBAUL, 0X0A637DC5A2C898A6UL, 0X113F9804BEF90DAEUL, 0X1B710B35131C471BUL,
        0X28DB77F523047D84UL, 0X32CAAB7B40C72493UL, 0X3C9EBE0A15C9BEBCUL, 0X431D67C49C100D4CUL,
        0X4CC5D4BECB3E42B6UL, 0X597F299CFC657E2AUL, 0X5FCB6FAB3AD6FAECUL, 0X6C44198C4A475817UL
    ];

    /// <summary>
    /// SHA-512 round constants (Ka), also used for SHA-512.
    /// </summary>
    /// <remarks>
    /// These constants are derived from the fractional parts of the cube roots of the first 64 prime numbers.
    /// </remarks>
    public static readonly System.UInt64[] K512 = K384;

    /// <summary>
    /// SHA-512 initial hash values (H0-H7).
    /// These values are used as the starting state of the SHA-512 hash computation.
    /// </summary>
    public static readonly System.UInt64[] H512 =
    [
        0X6A09E667F3BCC908UL, 0XBB67AE8584CAA73BUL, 0X3C6EF372FE94F82BUL, 0XA54FF53A5F1D36F1UL,
        0X510E527FADE682D1UL, 0X9B05688C2B3E6C1FUL, 0X1F83D9ABFB41BD6BUL, 0X5BE0CD19137E2179UL
    ];
}
