namespace Notio.Common.Security;

/// <summary>
/// Chosen SIMD mode
/// </summary>
public enum SimdMode
{
    /// <summary>
    /// Autodetect
    /// </summary>
    AutoDetect = 0,

    /// <summary>
    /// 128 bit SIMD
    /// </summary>
    V128,

    /// <summary>
    /// 256 bit SIMD
    /// </summary>
    V256,

    /// <summary>
    /// 512 bit SIMD
    /// </summary>
    V512,

    /// <summary>
    /// No SIMD
    /// </summary>
    None
}
