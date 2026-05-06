// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Codec.DataFrames.Internal;

internal enum DynamicWireKind : byte
{
    None = 0,
    Memory,
    String,
    StringArray,
    ByteArray,
    Packet,
    Nullable,
    NullableArray,
    UnmanagedList,
    UnmanagedArray,
    GenericCollection,
    Other
}
