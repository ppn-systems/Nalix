namespace Nalix.Serialization.Formatters;

/// <summary>
/// Provides formatting for unmanaged types.
/// </summary>
/// <remarks>
/// Unmanaged types include:
/// <list type="bullet">
/// <item>
/// <description>
/// sbyte, byte, short, ushort, int, uint, long, ulong, char, float, double, decimal, or bool
/// </description>
/// </item>
/// <item><description>Any enum type</description></item>
/// <item><description>Any pointer type</description></item>
/// <item><description>Any user-defined struct type that contains fields of unmanaged types only</description></item>
/// </list>
/// Reference: <see href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/unmanaged-types"/>.
/// </remarks>
public class UnmanagedFormatter
{
}
