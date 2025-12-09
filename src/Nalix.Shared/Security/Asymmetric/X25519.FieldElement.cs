// Copyright (c) 2025 PPN Corporation. All rights reserved.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Security.Asymmetric;

// Just a note:
// IEndpointKey  tried to keep this library as close as IEndpointKey  can to Golang one. Even IEndpointKey  tired to implement some
// constant time algorithms. But IEndpointKey  don't grantee this to be constant time.

/// <summary>
/// fieldElement represents an element of the field GF(2^255 - 19). An element
/// t, entries t[0]...t[9], represents the integer t[0]+2^26 t[1]+2^51 t[2]+2^77
/// t[3]+2^102 t[4]+...+2^230 t[9]. Bounds on each t[i] vary depending on
/// context.
/// </summary>
internal class FieldElement
{
    /// <summary>
    /// In Golang, field element type is just only [10]int array
    /// </summary>
    private readonly System.Int32[] _elements = new System.Int32[10];

    /// <summary>
    /// Generate field with empty elements
    /// </summary>
    public FieldElement()
    {
    }

    /// <summary>
    /// Generate field element from byte array
    /// </summary>
    /// <remarks>
    /// This is identical to feFromBytes in Golang
    /// </remarks>
    /// <param name="src"></param>
    public FieldElement(System.Byte[] src)
    {
        System.Int64 h0 = Load4(SubArray(src, 0, 4));
        System.Int64 h1 = Load3(SubArray(src, 4, 3)) << 6;
        System.Int64 h2 = Load3(SubArray(src, 7, 3)) << 5;
        System.Int64 h3 = Load3(SubArray(src, 10, 3)) << 3;
        System.Int64 h4 = Load3(SubArray(src, 13, 3)) << 2;
        System.Int64 h5 = Load4(SubArray(src, 16, 4));
        System.Int64 h6 = Load3(SubArray(src, 20, 3)) << 7;
        System.Int64 h7 = Load3(SubArray(src, 23, 3)) << 5;
        System.Int64 h8 = Load3(SubArray(src, 26, 3)) << 4;
        System.Int64 h9 = (Load3(SubArray(src, 29, 3)) & 0x7fffff) << 2;

        var carry = new System.Int64[10];
        carry[9] = h9 + (1 << 24) >> 25;
        h0 += carry[9] * 19;
        h9 -= carry[9] << 25;
        carry[1] = h1 + (1 << 24) >> 25;
        h2 += carry[1];
        h1 -= carry[1] << 25;
        carry[3] = h3 + (1 << 24) >> 25;
        h4 += carry[3];
        h3 -= carry[3] << 25;
        carry[5] = h5 + (1 << 24) >> 25;
        h6 += carry[5];
        h5 -= carry[5] << 25;
        carry[7] = h7 + (1 << 24) >> 25;
        h8 += carry[7];
        h7 -= carry[7] << 25;

        carry[0] = h0 + (1 << 25) >> 26;
        h1 += carry[0];
        h0 -= carry[0] << 26;
        carry[2] = h2 + (1 << 25) >> 26;
        h3 += carry[2];
        h2 -= carry[2] << 26;
        carry[4] = h4 + (1 << 25) >> 26;
        h5 += carry[4];
        h4 -= carry[4] << 26;
        carry[6] = h6 + (1 << 25) >> 26;
        h7 += carry[6];
        h6 -= carry[6] << 26;
        carry[8] = h8 + (1 << 25) >> 26;
        h9 += carry[8];
        h8 -= carry[8] << 26;

        _elements[0] = (System.Int32)h0;
        _elements[1] = (System.Int32)h1;
        _elements[2] = (System.Int32)h2;
        _elements[3] = (System.Int32)h3;
        _elements[4] = (System.Int32)h4;
        _elements[5] = (System.Int32)h5;
        _elements[6] = (System.Int32)h6;
        _elements[7] = (System.Int32)h7;
        _elements[8] = (System.Int32)h8;
        _elements[9] = (System.Int32)h9;
    }

    /// <summary>
    /// Directly overwrites elements in field with given array
    /// </summary>
    /// <param name="src"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetElementsDirect(System.Int32[] src)
    {
        // Security: Validate input
        System.ArgumentNullException.ThrowIfNull(src);

        if (src.Length < 10)
        {
            throw new System.ArgumentException("Source array must have at least 10 elements", nameof(src));
        }

        // Efficiency: Unrolled for maximum performance
        _elements[0] = src[0];
        _elements[1] = src[1];
        _elements[2] = src[2];
        _elements[3] = src[3];
        _elements[4] = src[4];
        _elements[5] = src[5];
        _elements[6] = src[6];
        _elements[7] = src[7];
        _elements[8] = src[8];
        _elements[9] = src[9];
    }

    /// <summary>
    /// Converts to a 32 byte array
    /// </summary>
    /// <remarks>
    /// <para>
    /// Identical to feToBytes in Golang
    ///   feToBytes marshals h to s.
    /// Preconditions:
    /// |h| bounded by 1.1*2^25,1.1*2^24,1.1*2^25,1.1*2^24,etc.
    /// </para>
    /// <para>
    /// Write p=2^255-19; q=floor(h/p).
    /// Basic claim: q = floor(2^(-255)(h + 19 2^(-25)h9 + 2^(-1))).
    /// </para>
    /// <para>
    /// Proof:
    /// Have |h|&lt;=p so |q|&lt;=1 so |19^2 2^(-255) q|&lt;1/4.
    /// Also have |h-2^230 h9|&lt;2^230 so |19 2^(-255)(h-2^230 h9)|&lt;1/4.
    /// </para>
    /// <para>
    /// Write y=2^(-1)-19^2 2^(-255)q-19 2^(-255)(h-2^230 h9).
    /// Then 0&lt;y&lt;1.
    /// </para>
    /// <para>
    /// Write r=h-pq.
    /// Have 0&lt;=r&lt;=p-1=2^255-20.
    /// Thus 0&lt;=r+19(2^-255)r&lt;r+19(2^-255)2^255&lt;=2^255-1.
    /// </para>
    /// <para>
    /// Write x=r+19(2^-255)r+y.
    /// Then 0&lt;x&lt;2^255 so floor(2^(-255)x) = 0 so floor(q+2^(-255)x) = q.
    /// </para>
    /// <para>
    /// Have q+2^(-255)x = 2^(-255)(h + 19 2^(-25) h9 + 2^(-1))
    /// so floor(2^(-255)(h + 19 2^(-25) h9 + 2^(-1))) = q.
    /// </para>
    /// </remarks>
    /// <returns></returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] ToBytes()
    {
        var carry = new System.Int32[10];

        System.Int32 q = 19 * _elements[9] + (1 << 24) >> 25;
        q = _elements[0] + q >> 26;
        q = _elements[1] + q >> 25;
        q = _elements[2] + q >> 26;
        q = _elements[3] + q >> 25;
        q = _elements[4] + q >> 26;
        q = _elements[5] + q >> 25;
        q = _elements[6] + q >> 26;
        q = _elements[7] + q >> 25;
        q = _elements[8] + q >> 26;
        q = _elements[9] + q >> 25;

        // Goal: Output h-(2^255-19)q, which is between 0 and 2^255-20.
        _elements[0] += 19 * q;
        // Goal: Output h-2^255 q, which is between 0 and 2^255-20.

        carry[0] = _elements[0] >> 26;
        _elements[1] += carry[0];
        _elements[0] -= carry[0] << 26;
        carry[1] = _elements[1] >> 25;
        _elements[2] += carry[1];
        _elements[1] -= carry[1] << 25;
        carry[2] = _elements[2] >> 26;
        _elements[3] += carry[2];
        _elements[2] -= carry[2] << 26;
        carry[3] = _elements[3] >> 25;
        _elements[4] += carry[3];
        _elements[3] -= carry[3] << 25;
        carry[4] = _elements[4] >> 26;
        _elements[5] += carry[4];
        _elements[4] -= carry[4] << 26;
        carry[5] = _elements[5] >> 25;
        _elements[6] += carry[5];
        _elements[5] -= carry[5] << 25;
        carry[6] = _elements[6] >> 26;
        _elements[7] += carry[6];
        _elements[6] -= carry[6] << 26;
        carry[7] = _elements[7] >> 25;
        _elements[8] += carry[7];
        _elements[7] -= carry[7] << 25;
        carry[8] = _elements[8] >> 26;
        _elements[9] += carry[8];
        _elements[8] -= carry[8] << 26;
        carry[9] = _elements[9] >> 25;
        _elements[9] -= carry[9] << 25;
        // h10 = carry9

        // Goal: Output h[0]+...+2^255 h10-2^255 q, which is between 0 and 2^255-20.
        // Have h[0]+...+2^230 h[9] between 0 and 2^255-1;
        // evidently 2^255 h10-2^255 q = 0.
        // Goal: Output h[0]+...+2^230 h[9].

        var s = new System.Byte[32];
        s[0] = (System.Byte)(_elements[0] >> 0);
        s[1] = (System.Byte)(_elements[0] >> 8);
        s[2] = (System.Byte)(_elements[0] >> 16);
        s[3] = (System.Byte)(_elements[0] >> 24 | _elements[1] << 2);
        s[4] = (System.Byte)(_elements[1] >> 6);
        s[5] = (System.Byte)(_elements[1] >> 14);
        s[6] = (System.Byte)(_elements[1] >> 22 | _elements[2] << 3);
        s[7] = (System.Byte)(_elements[2] >> 5);
        s[8] = (System.Byte)(_elements[2] >> 13);
        s[9] = (System.Byte)(_elements[2] >> 21 | _elements[3] << 5);
        s[10] = (System.Byte)(_elements[3] >> 3);
        s[11] = (System.Byte)(_elements[3] >> 11);
        s[12] = (System.Byte)(_elements[3] >> 19 | _elements[4] << 6);
        s[13] = (System.Byte)(_elements[4] >> 2);
        s[14] = (System.Byte)(_elements[4] >> 10);
        s[15] = (System.Byte)(_elements[4] >> 18);
        s[16] = (System.Byte)(_elements[5] >> 0);
        s[17] = (System.Byte)(_elements[5] >> 8);
        s[18] = (System.Byte)(_elements[5] >> 16);
        s[19] = (System.Byte)(_elements[5] >> 24 | _elements[6] << 1);
        s[20] = (System.Byte)(_elements[6] >> 7);
        s[21] = (System.Byte)(_elements[6] >> 15);
        s[22] = (System.Byte)(_elements[6] >> 23 | _elements[7] << 3);
        s[23] = (System.Byte)(_elements[7] >> 5);
        s[24] = (System.Byte)(_elements[7] >> 13);
        s[25] = (System.Byte)(_elements[7] >> 21 | _elements[8] << 4);
        s[26] = (System.Byte)(_elements[8] >> 4);
        s[27] = (System.Byte)(_elements[8] >> 12);
        s[28] = (System.Byte)(_elements[8] >> 20 | _elements[9] << 6);
        s[29] = (System.Byte)(_elements[9] >> 2);
        s[30] = (System.Byte)(_elements[9] >> 10);
        s[31] = (System.Byte)(_elements[9] >> 18);
        return s;
    }

    /// <summary>
    /// Calculates this * g
    /// </summary>
    /// <param name="g"></param>
    /// <remarks>
    /// <para>Can overlap h with f or g.</para>
    /// <para>
    /// Preconditions:
    /// |f| bounded by 1.1*2^26,1.1*2^25,1.1*2^26,1.1*2^25,etc.
    /// |g| bounded by 1.1*2^26,1.1*2^25,1.1*2^26,1.1*2^25,etc.
    /// </para>
    /// <para>
    /// Postconditions:
    /// |h| bounded by 1.1*2^25,1.1*2^24,1.1*2^25,1.1*2^24,etc.
    /// </para>
    /// <para>Notes on implementation strategy:</para>
    /// <para>
    /// Using schoolbook multiplication.
    /// Karatsuba would save a little in some cost models.
    /// </para>
    /// <para>
    /// Most multiplications by 2 and 19 are 32-bit precomputations;
    /// cheaper than 64-bit postcomputations.
    /// </para>
    /// <para>
    /// There is one remaining multiplication by 19 in the carry chain;
    /// one *19 precomputation can be merged into this,
    /// but the resulting data flow is considerably less clean.
    /// </para>
    /// <para>
    /// There are 12 carries below.
    /// 10 of them are 2-way parallelizable and vectorizable.
    /// Can get away with 11 carries, but then data flow is much deeper.
    /// </para>
    /// <para>With tighter constraints on inputs can squeeze carries into int32.</para>
    /// </remarks>
    /// <returns></returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public FieldElement Multiply(FieldElement g)
    {
        System.Int32 f0 = _elements[0];
        System.Int32 f1 = _elements[1];
        System.Int32 f2 = _elements[2];
        System.Int32 f3 = _elements[3];
        System.Int32 f4 = _elements[4];
        System.Int32 f5 = _elements[5];
        System.Int32 f6 = _elements[6];
        System.Int32 f7 = _elements[7];
        System.Int32 f8 = _elements[8];
        System.Int32 f9 = _elements[9];
        System.Int32 g0 = g[0];
        System.Int32 g1 = g[1];
        System.Int32 g2 = g[2];
        System.Int32 g3 = g[3];
        System.Int32 g4 = g[4];
        System.Int32 g5 = g[5];
        System.Int32 g6 = g[6];
        System.Int32 g7 = g[7];
        System.Int32 g8 = g[8];
        System.Int32 g9 = g[9];
        System.Int32 g1_19 = 19 * g1; // 1.4*2^29
        System.Int32 g2_19 = 19 * g2; // 1.4*2^30; still ok
        System.Int32 g3_19 = 19 * g3;
        System.Int32 g4_19 = 19 * g4;
        System.Int32 g5_19 = 19 * g5;
        System.Int32 g6_19 = 19 * g6;
        System.Int32 g7_19 = 19 * g7;
        System.Int32 g8_19 = 19 * g8;
        System.Int32 g9_19 = 19 * g9;
        System.Int32 f1_2 = 2 * f1;
        System.Int32 f3_2 = 2 * f3;
        System.Int32 f5_2 = 2 * f5;
        System.Int32 f7_2 = 2 * f7;
        System.Int32 f9_2 = 2 * f9;
        System.Int64 f0g0 = (System.Int64)f0 * g0;
        System.Int64 f0g1 = f0 * (System.Int64)g1;
        System.Int64 f0g2 = f0 * (System.Int64)g2;
        System.Int64 f0g3 = f0 * (System.Int64)g3;
        System.Int64 f0g4 = f0 * (System.Int64)g4;
        System.Int64 f0g5 = f0 * (System.Int64)g5;
        System.Int64 f0g6 = f0 * (System.Int64)g6;
        System.Int64 f0g7 = f0 * (System.Int64)g7;
        System.Int64 f0g8 = f0 * (System.Int64)g8;
        System.Int64 f0g9 = f0 * (System.Int64)g9;
        System.Int64 f1g0 = f1 * (System.Int64)g0;
        System.Int64 f1g1_2 = f1_2 * (System.Int64)g1;
        System.Int64 f1g2 = f1 * (System.Int64)g2;
        System.Int64 f1g3_2 = f1_2 * (System.Int64)g3;
        System.Int64 f1g4 = f1 * (System.Int64)g4;
        System.Int64 f1g5_2 = f1_2 * (System.Int64)g5;
        System.Int64 f1g6 = f1 * (System.Int64)g6;
        System.Int64 f1g7_2 = f1_2 * (System.Int64)g7;
        System.Int64 f1g8 = f1 * (System.Int64)g8;
        System.Int64 f1g9_38 = f1_2 * (System.Int64)g9_19;
        System.Int64 f2g0 = f2 * (System.Int64)g0;
        System.Int64 f2g1 = f2 * (System.Int64)g1;
        System.Int64 f2g2 = f2 * (System.Int64)g2;
        System.Int64 f2g3 = f2 * (System.Int64)g3;
        System.Int64 f2g4 = f2 * (System.Int64)g4;
        System.Int64 f2g5 = f2 * (System.Int64)g5;
        System.Int64 f2g6 = f2 * (System.Int64)g6;
        System.Int64 f2g7 = f2 * (System.Int64)g7;
        System.Int64 f2g8_19 = f2 * (System.Int64)g8_19;
        System.Int64 f2g9_19 = f2 * (System.Int64)g9_19;
        System.Int64 f3g0 = f3 * (System.Int64)g0;
        System.Int64 f3g1_2 = f3_2 * (System.Int64)g1;
        System.Int64 f3g2 = f3 * (System.Int64)g2;
        System.Int64 f3g3_2 = f3_2 * (System.Int64)g3;
        System.Int64 f3g4 = f3 * (System.Int64)g4;
        System.Int64 f3g5_2 = f3_2 * (System.Int64)g5;
        System.Int64 f3g6 = f3 * (System.Int64)g6;
        System.Int64 f3g7_38 = f3_2 * (System.Int64)g7_19;
        System.Int64 f3g8_19 = f3 * (System.Int64)g8_19;
        System.Int64 f3g9_38 = f3_2 * (System.Int64)g9_19;
        System.Int64 f4g0 = f4 * (System.Int64)g0;
        System.Int64 f4g1 = f4 * (System.Int64)g1;
        System.Int64 f4g2 = f4 * (System.Int64)g2;
        System.Int64 f4g3 = f4 * (System.Int64)g3;
        System.Int64 f4g4 = f4 * (System.Int64)g4;
        System.Int64 f4g5 = f4 * (System.Int64)g5;
        System.Int64 f4g6_19 = f4 * (System.Int64)g6_19;
        System.Int64 f4g7_19 = f4 * (System.Int64)g7_19;
        System.Int64 f4g8_19 = f4 * (System.Int64)g8_19;
        System.Int64 f4g9_19 = f4 * (System.Int64)g9_19;
        System.Int64 f5g0 = f5 * (System.Int64)g0;
        System.Int64 f5g1_2 = f5_2 * (System.Int64)g1;
        System.Int64 f5g2 = f5 * (System.Int64)g2;
        System.Int64 f5g3_2 = f5_2 * (System.Int64)g3;
        System.Int64 f5g4 = f5 * (System.Int64)g4;
        System.Int64 f5g5_38 = f5_2 * (System.Int64)g5_19;
        System.Int64 f5g6_19 = f5 * (System.Int64)g6_19;
        System.Int64 f5g7_38 = f5_2 * (System.Int64)g7_19;
        System.Int64 f5g8_19 = f5 * (System.Int64)g8_19;
        System.Int64 f5g9_38 = f5_2 * (System.Int64)g9_19;
        System.Int64 f6g0 = f6 * (System.Int64)g0;
        System.Int64 f6g1 = f6 * (System.Int64)g1;
        System.Int64 f6g2 = f6 * (System.Int64)g2;
        System.Int64 f6g3 = f6 * (System.Int64)g3;
        System.Int64 f6g4_19 = f6 * (System.Int64)g4_19;
        System.Int64 f6g5_19 = f6 * (System.Int64)g5_19;
        System.Int64 f6g6_19 = f6 * (System.Int64)g6_19;
        System.Int64 f6g7_19 = f6 * (System.Int64)g7_19;
        System.Int64 f6g8_19 = f6 * (System.Int64)g8_19;
        System.Int64 f6g9_19 = f6 * (System.Int64)g9_19;
        System.Int64 f7g0 = f7 * (System.Int64)g0;
        System.Int64 f7g1_2 = f7_2 * (System.Int64)g1;
        System.Int64 f7g2 = f7 * (System.Int64)g2;
        System.Int64 f7g3_38 = f7_2 * (System.Int64)g3_19;
        System.Int64 f7g4_19 = f7 * (System.Int64)g4_19;
        System.Int64 f7g5_38 = f7_2 * (System.Int64)g5_19;
        System.Int64 f7g6_19 = f7 * (System.Int64)g6_19;
        System.Int64 f7g7_38 = f7_2 * (System.Int64)g7_19;
        System.Int64 f7g8_19 = f7 * (System.Int64)g8_19;
        System.Int64 f7g9_38 = f7_2 * (System.Int64)g9_19;
        System.Int64 f8g0 = f8 * (System.Int64)g0;
        System.Int64 f8g1 = f8 * (System.Int64)g1;
        System.Int64 f8g2_19 = f8 * (System.Int64)g2_19;
        System.Int64 f8g3_19 = f8 * (System.Int64)g3_19;
        System.Int64 f8g4_19 = f8 * (System.Int64)g4_19;
        System.Int64 f8g5_19 = f8 * (System.Int64)g5_19;
        System.Int64 f8g6_19 = f8 * (System.Int64)g6_19;
        System.Int64 f8g7_19 = f8 * (System.Int64)g7_19;
        System.Int64 f8g8_19 = f8 * (System.Int64)g8_19;
        System.Int64 f8g9_19 = f8 * (System.Int64)g9_19;
        System.Int64 f9g0 = f9 * (System.Int64)g0;
        System.Int64 f9g1_38 = f9_2 * (System.Int64)g1_19;
        System.Int64 f9g2_19 = f9 * (System.Int64)g2_19;
        System.Int64 f9g3_38 = f9_2 * (System.Int64)g3_19;
        System.Int64 f9g4_19 = f9 * (System.Int64)g4_19;
        System.Int64 f9g5_38 = f9_2 * (System.Int64)g5_19;
        System.Int64 f9g6_19 = f9 * (System.Int64)g6_19;
        System.Int64 f9g7_38 = f9_2 * (System.Int64)g7_19;
        System.Int64 f9g8_19 = f9 * (System.Int64)g8_19;
        System.Int64 f9g9_38 = f9_2 * (System.Int64)g9_19;
        System.Int64 h0 = f0g0 + f1g9_38 + f2g8_19 + f3g7_38 + f4g6_19 + f5g5_38 + f6g4_19 + f7g3_38 + f8g2_19 + f9g1_38;
        System.Int64 h1 = f0g1 + f1g0 + f2g9_19 + f3g8_19 + f4g7_19 + f5g6_19 + f6g5_19 + f7g4_19 + f8g3_19 + f9g2_19;
        System.Int64 h2 = f0g2 + f1g1_2 + f2g0 + f3g9_38 + f4g8_19 + f5g7_38 + f6g6_19 + f7g5_38 + f8g4_19 + f9g3_38;
        System.Int64 h3 = f0g3 + f1g2 + f2g1 + f3g0 + f4g9_19 + f5g8_19 + f6g7_19 + f7g6_19 + f8g5_19 + f9g4_19;
        System.Int64 h4 = f0g4 + f1g3_2 + f2g2 + f3g1_2 + f4g0 + f5g9_38 + f6g8_19 + f7g7_38 + f8g6_19 + f9g5_38;
        System.Int64 h5 = f0g5 + f1g4 + f2g3 + f3g2 + f4g1 + f5g0 + f6g9_19 + f7g8_19 + f8g7_19 + f9g6_19;
        System.Int64 h6 = f0g6 + f1g5_2 + f2g4 + f3g3_2 + f4g2 + f5g1_2 + f6g0 + f7g9_38 + f8g8_19 + f9g7_38;
        System.Int64 h7 = f0g7 + f1g6 + f2g5 + f3g4 + f4g3 + f5g2 + f6g1 + f7g0 + f8g9_19 + f9g8_19;
        System.Int64 h8 = f0g8 + f1g7_2 + f2g6 + f3g5_2 + f4g4 + f5g3_2 + f6g2 + f7g1_2 + f8g0 + f9g9_38;
        System.Int64 h9 = f0g9 + f1g8 + f2g7 + f3g6 + f4g5 + f5g4 + f6g3 + f7g2 + f8g1 + f9g0;
        var carry = new System.Int64[10];

        // |h0| <= (1.1*1.1*2^52*(1+19+19+19+19)+1.1*1.1*2^50*(38+38+38+38+38))
        //   i.e. |h0| <= 1.2*2^59; narrower ranges for h2, h4, h6, h8
        // |h1| <= (1.1*1.1*2^51*(1+1+19+19+19+19+19+19+19+19))
        //   i.e. |h1| <= 1.5*2^58; narrower ranges for h3, h5, h7, h9

        carry[0] = h0 + (1 << 25) >> 26;
        h1 += carry[0];
        h0 -= carry[0] << 26;
        carry[4] = h4 + (1 << 25) >> 26;
        h5 += carry[4];
        h4 -= carry[4] << 26;
        // |h0| <= 2^25
        // |h4| <= 2^25
        // |h1| <= 1.51*2^58
        // |h5| <= 1.51*2^58

        carry[1] = h1 + (1 << 24) >> 25;
        h2 += carry[1];
        h1 -= carry[1] << 25;
        carry[5] = h5 + (1 << 24) >> 25;
        h6 += carry[5];
        h5 -= carry[5] << 25;
        // |h1| <= 2^24; from now on fits into int32
        // |h5| <= 2^24; from now on fits into int32
        // |h2| <= 1.21*2^59
        // |h6| <= 1.21*2^59

        carry[2] = h2 + (1 << 25) >> 26;
        h3 += carry[2];
        h2 -= carry[2] << 26;
        carry[6] = h6 + (1 << 25) >> 26;
        h7 += carry[6];
        h6 -= carry[6] << 26;
        // |h2| <= 2^25; from now on fits into int32 unchanged
        // |h6| <= 2^25; from now on fits into int32 unchanged
        // |h3| <= 1.51*2^58
        // |h7| <= 1.51*2^58

        carry[3] = h3 + (1 << 24) >> 25;
        h4 += carry[3];
        h3 -= carry[3] << 25;
        carry[7] = h7 + (1 << 24) >> 25;
        h8 += carry[7];
        h7 -= carry[7] << 25;
        // |h3| <= 2^24; from now on fits into int32 unchanged
        // |h7| <= 2^24; from now on fits into int32 unchanged
        // |h4| <= 1.52*2^33
        // |h8| <= 1.52*2^33

        carry[4] = h4 + (1 << 25) >> 26;
        h5 += carry[4];
        h4 -= carry[4] << 26;
        carry[8] = h8 + (1 << 25) >> 26;
        h9 += carry[8];
        h8 -= carry[8] << 26;
        // |h4| <= 2^25; from now on fits into int32 unchanged
        // |h8| <= 2^25; from now on fits into int32 unchanged
        // |h5| <= 1.01*2^24
        // |h9| <= 1.51*2^58

        carry[9] = h9 + (1 << 24) >> 25;
        h0 += carry[9] * 19;
        h9 -= carry[9] << 25;
        // |h9| <= 2^24; from now on fits into int32 unchanged
        // |h0| <= 1.8*2^37

        carry[0] = h0 + (1 << 25) >> 26;
        h1 += carry[0];
        h0 -= carry[0] << 26;
        // |h0| <= 2^25; from now on fits into int32 unchanged
        // |h1| <= 1.01*2^24

        System.Int32[] h =
        [
            (System.Int32)h0,
            (System.Int32)h1,
            (System.Int32)h2,
            (System.Int32)h3,
            (System.Int32)h4,
            (System.Int32)h5,
            (System.Int32)h6,
            (System.Int32)h7,
            (System.Int32)h8,
            (System.Int32)h9,
        ];
        var final = new FieldElement();
        final.SetElementsDirect(h);
        return final;
    }
    /// <summary>
    /// Calculates f*f. Can overlap h with f.
    /// </summary>
    /// <returns></returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public FieldElement Square()
    {
        System.Int32 f0 = _elements[0];
        System.Int32 f1 = _elements[1];
        System.Int32 f2 = _elements[2];
        System.Int32 f3 = _elements[3];
        System.Int32 f4 = _elements[4];
        System.Int32 f5 = _elements[5];
        System.Int32 f6 = _elements[6];
        System.Int32 f7 = _elements[7];
        System.Int32 f8 = _elements[8];
        System.Int32 f9 = _elements[9];
        System.Int32 f0_2 = 2 * f0;
        System.Int32 f1_2 = 2 * f1;
        System.Int32 f2_2 = 2 * f2;
        System.Int32 f3_2 = 2 * f3;
        System.Int32 f4_2 = 2 * f4;
        System.Int32 f5_2 = 2 * f5;
        System.Int32 f6_2 = 2 * f6;
        System.Int32 f7_2 = 2 * f7;
        System.Int32 f5_38 = 38 * f5; // 1.31*2^30
        System.Int32 f6_19 = 19 * f6; // 1.31*2^30
        System.Int32 f7_38 = 38 * f7; // 1.31*2^30
        System.Int32 f8_19 = 19 * f8; // 1.31*2^30
        System.Int32 f9_38 = 38 * f9; // 1.31*2^30
        System.Int64 f0f0 = f0 * (System.Int64)f0;
        System.Int64 f0f1_2 = f0_2 * (System.Int64)f1;
        System.Int64 f0f2_2 = f0_2 * (System.Int64)f2;
        System.Int64 f0f3_2 = f0_2 * (System.Int64)f3;
        System.Int64 f0f4_2 = f0_2 * (System.Int64)f4;
        System.Int64 f0f5_2 = f0_2 * (System.Int64)f5;
        System.Int64 f0f6_2 = f0_2 * (System.Int64)f6;
        System.Int64 f0f7_2 = f0_2 * (System.Int64)f7;
        System.Int64 f0f8_2 = f0_2 * (System.Int64)f8;
        System.Int64 f0f9_2 = f0_2 * (System.Int64)f9;
        System.Int64 f1f1_2 = f1_2 * (System.Int64)f1;
        System.Int64 f1f2_2 = f1_2 * (System.Int64)f2;
        System.Int64 f1f3_4 = f1_2 * (System.Int64)f3_2;
        System.Int64 f1f4_2 = f1_2 * (System.Int64)f4;
        System.Int64 f1f5_4 = f1_2 * (System.Int64)f5_2;
        System.Int64 f1f6_2 = f1_2 * (System.Int64)f6;
        System.Int64 f1f7_4 = f1_2 * (System.Int64)f7_2;
        System.Int64 f1f8_2 = f1_2 * (System.Int64)f8;
        System.Int64 f1f9_76 = f1_2 * (System.Int64)f9_38;
        System.Int64 f2f2 = f2 * (System.Int64)f2;
        System.Int64 f2f3_2 = f2_2 * (System.Int64)f3;
        System.Int64 f2f4_2 = f2_2 * (System.Int64)f4;
        System.Int64 f2f5_2 = f2_2 * (System.Int64)f5;
        System.Int64 f2f6_2 = f2_2 * (System.Int64)f6;
        System.Int64 f2f7_2 = f2_2 * (System.Int64)f7;
        System.Int64 f2f8_38 = f2_2 * (System.Int64)f8_19;
        System.Int64 f2f9_38 = f2 * (System.Int64)f9_38;
        System.Int64 f3f3_2 = f3_2 * (System.Int64)f3;
        System.Int64 f3f4_2 = f3_2 * (System.Int64)f4;
        System.Int64 f3f5_4 = f3_2 * (System.Int64)f5_2;
        System.Int64 f3f6_2 = f3_2 * (System.Int64)f6;
        System.Int64 f3f7_76 = f3_2 * (System.Int64)f7_38;
        System.Int64 f3f8_38 = f3_2 * (System.Int64)f8_19;
        System.Int64 f3f9_76 = f3_2 * (System.Int64)f9_38;
        System.Int64 f4f4 = f4 * (System.Int64)f4;
        System.Int64 f4f5_2 = f4_2 * (System.Int64)f5;
        System.Int64 f4f6_38 = f4_2 * (System.Int64)f6_19;
        System.Int64 f4f7_38 = f4 * (System.Int64)f7_38;
        System.Int64 f4f8_38 = f4_2 * (System.Int64)f8_19;
        System.Int64 f4f9_38 = f4 * (System.Int64)f9_38;
        System.Int64 f5f5_38 = f5 * (System.Int64)f5_38;
        System.Int64 f5f6_38 = f5_2 * (System.Int64)f6_19;
        System.Int64 f5f7_76 = f5_2 * (System.Int64)f7_38;
        System.Int64 f5f8_38 = f5_2 * (System.Int64)f8_19;
        System.Int64 f5f9_76 = f5_2 * (System.Int64)f9_38;
        System.Int64 f6f6_19 = f6 * (System.Int64)f6_19;
        System.Int64 f6f7_38 = f6 * (System.Int64)f7_38;
        System.Int64 f6f8_38 = f6_2 * (System.Int64)f8_19;
        System.Int64 f6f9_38 = f6 * (System.Int64)f9_38;
        System.Int64 f7f7_38 = f7 * (System.Int64)f7_38;
        System.Int64 f7f8_38 = f7_2 * (System.Int64)f8_19;
        System.Int64 f7f9_76 = f7_2 * (System.Int64)f9_38;
        System.Int64 f8f8_19 = f8 * (System.Int64)f8_19;
        System.Int64 f8f9_38 = f8 * (System.Int64)f9_38;
        System.Int64 f9f9_38 = f9 * (System.Int64)f9_38;
        System.Int64 h0 = f0f0 + f1f9_76 + f2f8_38 + f3f7_76 + f4f6_38 + f5f5_38;
        System.Int64 h1 = f0f1_2 + f2f9_38 + f3f8_38 + f4f7_38 + f5f6_38;
        System.Int64 h2 = f0f2_2 + f1f1_2 + f3f9_76 + f4f8_38 + f5f7_76 + f6f6_19;
        System.Int64 h3 = f0f3_2 + f1f2_2 + f4f9_38 + f5f8_38 + f6f7_38;
        System.Int64 h4 = f0f4_2 + f1f3_4 + f2f2 + f5f9_76 + f6f8_38 + f7f7_38;
        System.Int64 h5 = f0f5_2 + f1f4_2 + f2f3_2 + f6f9_38 + f7f8_38;
        System.Int64 h6 = f0f6_2 + f1f5_4 + f2f4_2 + f3f3_2 + f7f9_76 + f8f8_19;
        System.Int64 h7 = f0f7_2 + f1f6_2 + f2f5_2 + f3f4_2 + f8f9_38;
        System.Int64 h8 = f0f8_2 + f1f7_4 + f2f6_2 + f3f5_4 + f4f4 + f9f9_38;
        System.Int64 h9 = f0f9_2 + f1f8_2 + f2f7_2 + f3f6_2 + f4f5_2;
        var carry = new System.Int64[10];

        carry[0] = h0 + (1 << 25) >> 26;
        h1 += carry[0];
        h0 -= carry[0] << 26;
        carry[4] = h4 + (1 << 25) >> 26;
        h5 += carry[4];
        h4 -= carry[4] << 26;

        carry[1] = h1 + (1 << 24) >> 25;
        h2 += carry[1];
        h1 -= carry[1] << 25;
        carry[5] = h5 + (1 << 24) >> 25;
        h6 += carry[5];
        h5 -= carry[5] << 25;

        carry[2] = h2 + (1 << 25) >> 26;
        h3 += carry[2];
        h2 -= carry[2] << 26;
        carry[6] = h6 + (1 << 25) >> 26;
        h7 += carry[6];
        h6 -= carry[6] << 26;

        carry[3] = h3 + (1 << 24) >> 25;
        h4 += carry[3];
        h3 -= carry[3] << 25;
        carry[7] = h7 + (1 << 24) >> 25;
        h8 += carry[7];
        h7 -= carry[7] << 25;

        carry[4] = h4 + (1 << 25) >> 26;
        h5 += carry[4];
        h4 -= carry[4] << 26;
        carry[8] = h8 + (1 << 25) >> 26;
        h9 += carry[8];
        h8 -= carry[8] << 26;

        carry[9] = h9 + (1 << 24) >> 25;
        h0 += carry[9] * 19;
        h9 -= carry[9] << 25;

        carry[0] = h0 + (1 << 25) >> 26;
        h1 += carry[0];
        h0 -= carry[0] << 26;

        var final = new FieldElement();
        var h = new System.Int32[10];
        h[0] = (System.Int32)h0;
        h[1] = (System.Int32)h1;
        h[2] = (System.Int32)h2;
        h[3] = (System.Int32)h3;
        h[4] = (System.Int32)h4;
        h[5] = (System.Int32)h5;
        h[6] = (System.Int32)h6;
        h[7] = (System.Int32)h7;
        h[8] = (System.Int32)h8;
        h[9] = (System.Int32)h9;
        final.SetElementsDirect(h);
        return final;
    }
    /// <summary>
    /// Calculates h = f * 121666. Can overlap h with f; IEndpointKey  have no clue why this is a thing
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Preconditions:
    /// |f| bounded by 1.1*2^26,1.1*2^25,1.1*2^26,1.1*2^25,etc.
    /// </para>
    /// <para>
    /// Postconditions:
    /// |h| bounded by 1.1*2^25,1.1*2^24,1.1*2^25,1.1*2^24,etc.
    /// </para>
    /// </remarks>
    /// <returns></returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public FieldElement Mul121666()
    {
        System.Int64 h0 = (System.Int64)_elements[0] * 121666;
        System.Int64 h1 = (System.Int64)_elements[1] * 121666;
        System.Int64 h2 = (System.Int64)_elements[2] * 121666;
        System.Int64 h3 = (System.Int64)_elements[3] * 121666;
        System.Int64 h4 = (System.Int64)_elements[4] * 121666;
        System.Int64 h5 = (System.Int64)_elements[5] * 121666;
        System.Int64 h6 = (System.Int64)_elements[6] * 121666;
        System.Int64 h7 = (System.Int64)_elements[7] * 121666;
        System.Int64 h8 = (System.Int64)_elements[8] * 121666;
        System.Int64 h9 = (System.Int64)_elements[9] * 121666;
        var carry = new System.Int64[10];

        carry[9] = h9 + (1 << 24) >> 25;
        h0 += carry[9] * 19;
        h9 -= carry[9] << 25;
        carry[1] = h1 + (1 << 24) >> 25;
        h2 += carry[1];
        h1 -= carry[1] << 25;
        carry[3] = h3 + (1 << 24) >> 25;
        h4 += carry[3];
        h3 -= carry[3] << 25;
        carry[5] = h5 + (1 << 24) >> 25;
        h6 += carry[5];
        h5 -= carry[5] << 25;
        carry[7] = h7 + (1 << 24) >> 25;
        h8 += carry[7];
        h7 -= carry[7] << 25;

        carry[0] = h0 + (1 << 25) >> 26;
        h1 += carry[0];
        h0 -= carry[0] << 26;
        carry[2] = h2 + (1 << 25) >> 26;
        h3 += carry[2];
        h2 -= carry[2] << 26;
        carry[4] = h4 + (1 << 25) >> 26;
        h5 += carry[4];
        h4 -= carry[4] << 26;
        carry[6] = h6 + (1 << 25) >> 26;
        h7 += carry[6];
        h6 -= carry[6] << 26;
        carry[8] = h8 + (1 << 25) >> 26;
        h9 += carry[8];
        h8 -= carry[8] << 26;

        var final = new FieldElement();
        var h = new System.Int32[10];
        h[0] = (System.Int32)h0;
        h[1] = (System.Int32)h1;
        h[2] = (System.Int32)h2;
        h[3] = (System.Int32)h3;
        h[4] = (System.Int32)h4;
        h[5] = (System.Int32)h5;
        h[6] = (System.Int32)h6;
        h[7] = (System.Int32)h7;
        h[8] = (System.Int32)h8;
        h[9] = (System.Int32)h9;
        final.SetElementsDirect(h);
        return final;
    }
    /// <summary>
    /// Calculates this ^(-1)
    /// </summary>
    /// <returns></returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public FieldElement Invert()
    {
        FieldElement t0, t1, t2, t3;

        // Initial square
        t0 = Square();

        // 2 squares: t1 = t0^4
        t1 = t0.Square();
        t1 = t1.Square();

        // Multiply operations
        t1 = Multiply(t1);
        t0 = t0.Multiply(t1);

        // Single square
        t2 = t0.Square();

        t1 = t1.Multiply(t2);

        // 5 squares: unrolled
        t2 = t1.Square();
        t2 = t2.Square(); // i=1
        t2 = t2.Square(); // i=2  
        t2 = t2.Square(); // i=3
        t2 = t2.Square(); // i=4

        t1 = t2.Multiply(t1);

        // 10 squares: unrolled
        t2 = t1.Square();
        t2 = t2.Square(); // i=1
        t2 = t2.Square(); // i=2
        t2 = t2.Square(); // i=3
        t2 = t2.Square(); // i=4
        t2 = t2.Square(); // i=5
        t2 = t2.Square(); // i=6
        t2 = t2.Square(); // i=7
        t2 = t2.Square(); // i=8
        t2 = t2.Square(); // i=9

        t2 = t2.Multiply(t1);

        // 20 squares: keep loop (too many to unroll efficiently)
        t3 = t2.Square();
        for (System.Int32 i = 1; i < 20; i++)
        {
            t3 = t3.Square();
        }

        t2 = t3.Multiply(t2);

        // 10 squares: unrolled
        t2 = t2.Square();
        t2 = t2.Square(); // i=1
        t2 = t2.Square(); // i=2
        t2 = t2.Square(); // i=3
        t2 = t2.Square(); // i=4
        t2 = t2.Square(); // i=5
        t2 = t2.Square(); // i=6
        t2 = t2.Square(); // i=7
        t2 = t2.Square(); // i=8
        t2 = t2.Square(); // i=9

        t1 = t2.Multiply(t1);

        // 50 squares: keep loop
        t2 = t1.Square();
        for (System.Int32 i = 1; i < 50; i++)
        {
            t2 = t2.Square();
        }

        t2 = t2.Multiply(t1);

        // 100 squares: keep loop
        t3 = t2.Square();
        for (System.Int32 i = 1; i < 100; i++)
        {
            t3 = t3.Square();
        }

        t2 = t3.Multiply(t2);

        // 50 squares: keep loop
        t2 = t2.Square();
        for (System.Int32 i = 1; i < 50; i++)
        {
            t2 = t2.Square();
        }

        t1 = t2.Multiply(t1);

        // 5 squares: unrolled
        t1 = t1.Square();
        t1 = t1.Square(); // i=1
        t1 = t1.Square(); // i=2
        t1 = t1.Square(); // i=3
        t1 = t1.Square(); // i=4

        return t1.Multiply(t0);
    }

    /// <summary>
    /// Directly gets or sets the _elements
    /// </summary>
    /// <param name="i">The index. Note that the max value is always 10</param>
    /// <returns></returns>
    public System.Int32 this[System.Int32 i]
    {
        get => _elements[i];
        set => _elements[i] = value;
    }
    /// <summary>
    /// Sets all values of field to zero except that Element[0] = 1
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void One()
    {
        // Modern approach with Span
        System.MemoryExtensions.AsSpan(_elements).Clear(); // Zero out all
        _elements[0] = 1;                                  // Set identity
    }


    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static FieldElement operator +(FieldElement f1, FieldElement f2)
    {
        FieldElement res = new();

        res[0] = f1[0] + f2[0];
        res[1] = f1[1] + f2[1];
        res[2] = f1[2] + f2[2];
        res[3] = f1[3] + f2[3];
        res[4] = f1[4] + f2[4];
        res[5] = f1[5] + f2[5];
        res[6] = f1[6] + f2[6];
        res[7] = f1[7] + f2[7];
        res[8] = f1[8] + f2[8];
        res[9] = f1[9] + f2[9];

        return res;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static FieldElement operator -(FieldElement f1, FieldElement f2)
    {
        FieldElement res = new();

        res[0] = f1[0] - f2[0];
        res[1] = f1[1] - f2[1];
        res[2] = f1[2] - f2[2];
        res[3] = f1[3] - f2[3];
        res[4] = f1[4] - f2[4];
        res[5] = f1[5] - f2[5];
        res[6] = f1[6] - f2[6];
        res[7] = f1[7] - f2[7];
        res[8] = f1[8] - f2[8];
        res[9] = f1[9] - f2[9];
        return res;
    }

    /// <summary>
    /// Replaces (f,g) with (g,f) if b == 1; replaces (f,g) with (f,g) if b == 0.
    /// </summary>
    /// <param name="f">f</param>
    /// <param name="g">g</param>
    /// <param name="b"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe void CSwap(ref FieldElement f, ref FieldElement g, System.Int32 b)
    {
        b = -b;
        fixed (System.Int32* fPtr = f._elements)
        {
            fixed (System.Int32* gPtr = g._elements)
            {
                for (System.Int32 i = 0; i < 10; i++)
                {
                    System.Int32 t = b & (fPtr[i] ^ gPtr[i]);
                    fPtr[i] ^= t;
                    gPtr[i] ^= t;
                }
            }
        }
    }
    /// <summary>
    /// Copies one FieldElement to another FieldElement
    /// </summary>
    /// <param name="dst">Where the src must be copied to</param>
    /// <param name="src">What to copy</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Copy(ref FieldElement dst, FieldElement src)
    {
        dst[0] = src[0];
        dst[1] = src[1];
        dst[2] = src[2];
        dst[3] = src[3];
        dst[4] = src[4];
        dst[5] = src[5];
        dst[6] = src[6];
        dst[7] = src[7];
        dst[8] = src[8];
        dst[9] = src[9];
    }

    /// <summary>
    /// load3 reads a 24-bit, little-endian value from in
    /// </summary>
    /// <param name="bytes">Input</param>
    /// <returns></returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe System.Int64 Load3(System.Byte[] bytes)
    {
        fixed (System.Byte* ptr = bytes)
        {
            return ptr[0] | ptr[1] << 8 | ptr[2] << 16;
        }
    }

    /// <summary>
    /// load4 reads a 32-bit, little-endian value from in.
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe System.Int64 Load4(System.Byte[] bytes)
    {
        fixed (System.Byte* ptr = bytes)
        {
            return *(System.UInt32*)ptr;
        }
    }

    /// <summary>
    /// Generates a slice of array
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data">Data to get slice from</param>
    /// <param name="index">The start index</param>
    /// <param name="length">The length of sub array</param>
    /// <returns></returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static T[] SubArray<T>(T[] data, System.Int32 index, System.Int32 length)
    {
        T[] result = new T[length];
        System.Array.Copy(data, index, result, 0, length);
        return result;
    }
}