namespace Nalix.Network.Listeners.Udp;

public abstract partial class UdpListenerBase
{
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.String EllipseLeft(System.String value, System.Int32 maxLen)
    {
        if (System.String.IsNullOrEmpty(value) || value.Length <= maxLen)
        {
            return value;
        }

        if (maxLen <= 3)
        {
            return new System.String('.', maxLen);
        }

        return $"...{System.MemoryExtensions.AsSpan(value, value.Length - (maxLen - 3))}";
    }
}
