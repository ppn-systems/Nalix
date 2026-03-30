// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;
using Nalix.Common.Exceptions;
using Nalix.Framework.Serialization;
using Xunit;

namespace Nalix.Framework.Tests.Serialization;

public class LiteSerializerGuardsTests
{
    [Fact]
    public void DeserializeEmptyBufferThrowsArgumentException()
    {
        byte[] empty = [];

        int dummy = 0;
        _ = Assert.Throws<SerializationFailureException>(() => LiteSerializer.Deserialize(empty, ref dummy));
    }

    [Fact]
    public void SerializeToProvidedBufferNullBufferThrows()
    {
        int value = 123;
        _ = Assert.Throws<ArgumentNullException>(() => LiteSerializer.Serialize(in value, null));
    }
}
