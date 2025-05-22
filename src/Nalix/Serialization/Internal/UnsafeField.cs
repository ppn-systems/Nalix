using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Nalix.Serialization.Internal;

internal class UnsafeField
{
    public static unsafe Action<T, TField> Setter<T, TField>(FieldInfo field)
        where T : unmanaged
        where TField : unmanaged
    {
        int offset = (int)Marshal.OffsetOf<T>(field.Name);

        return (T obj, TField value) =>
        {
            TField* pField = (TField*)(((byte*)&obj) + offset);
            *pField = value;
        };
    }

    public static unsafe Func<T, TField> Getter<T, TField>(FieldInfo field)
        where T : unmanaged
        where TField : unmanaged
    {
        int offset = (int)Marshal.OffsetOf<T>(field.Name);

        return (T obj) =>
        {
            TField* pField = (TField*)(((byte*)&obj) + offset);
            return *pField;
        };
    }
}
