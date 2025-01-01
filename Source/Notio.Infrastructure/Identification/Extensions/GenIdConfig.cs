namespace Notio.Infrastructure.Identification.Extensions;

public class GenIdConfig
{
    // Constants for bit lengths
    public const int TYPE_BITS = 4;

    public const int MACHINE_BITS = 12;
    public const int TIMESTAMP_BITS = 32;
    public const int SEQUENCE_BITS = 16;

    // Bit masks
    public const ulong TYPE_MASK = (1UL << TYPE_BITS) - 1;

    public const ulong MACHINE_MASK = (1UL << MACHINE_BITS) - 1;
    public const ulong TIMESTAMP_MASK = (1UL << TIMESTAMP_BITS) - 1;
    public const ulong SEQUENCE_MASK = (1UL << SEQUENCE_BITS) - 1;

    // Bit positions
    public const int TYPE_SHIFT = 60;

    public const int MACHINE_SHIFT = 48;
    public const int TIMESTAMP_SHIFT = 16;
}