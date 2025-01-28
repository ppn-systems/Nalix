using System.Linq;

namespace Notio.Lite;

public static class CompositeHashCode
{
    //
    // Summary:
    //     Computes a hash code, taking into consideration the values of the specified fields
    //     and/oror properties as part of an object's state. See the example.
    //
    // Parameters:
    //   fields:
    //     The values of the fields and/or properties.
    //
    // Returns:
    //     The computed has code.
    public static int Using(params object[] fields)
    {
        return fields.Where((f) => f != null).Aggregate(17, (current, field) => 29 * current + field.GetHashCode());
    }
}