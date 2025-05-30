namespace Nalix.Interop.Internal;

[System.Runtime.InteropServices.StructLayout(
    System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct SmallRect(
    System.Int16 left, System.Int16 top,
    System.Int16 right, System.Int16 bottom)
{
    public System.Int16 Left = left;
    public System.Int16 Top = top;
    public System.Int16 Right = right;
    public System.Int16 Bottom = bottom;
}
