// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;
using Nalix.Common.Exceptions;
using Nalix.Framework.Serialization;
using Xunit;

namespace Nalix.Framework.Tests.Serialization;

public sealed class LiteSerializerGuardTests
{
    [Fact]
    public void Deserialize_EmptyBuffer_ThrowsSerializationFailureException()
    {
        byte[] empty = [];
        int dummy = default;

        _ = Assert.Throws<SerializationFailureException>(() => LiteSerializer.Deserialize(empty, ref dummy));
    }

    [Fact]
    public void SerializeToProvidedBuffer_NullBuffer_ThrowsArgumentNullException()
    {
        int value = 123;
        _ = Assert.Throws<ArgumentNullException>(() => LiteSerializer.Serialize(in value, null!));
    }
}
