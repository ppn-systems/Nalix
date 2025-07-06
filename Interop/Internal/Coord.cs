namespace Nalix.Interop.Internal;

[System.Runtime.InteropServices.StructLayout(
    System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct Coord(System.Int16 x, System.Int16 y)
{
    public System.Int16 X = x;
    public System.Int16 Y = y;
}
