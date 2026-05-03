using Nalix.Codec.Extensions;
using Nalix.Codec.Memory;
// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using Nalix.Abstractions.Exceptions;
using Nalix.Framework.Memory.Buffers;
using Nalix.Codec.Serialization;
using Xunit;

namespace Nalix.Codec.Tests.Serialization;

public sealed class FormatterProviderTests
{
    [Fact]
    public void GetWhenCalledRepeatedlyReturnsSameCachedFormatterInstance()
    {
        IFormatter<int> first = FormatterProvider.Get<int>();
        IFormatter<int> second = FormatterProvider.Get<int>();

        Assert.Same(first, second);
    }

    [Fact]
    public void GetResolvesCommonCollectionAndTupleFormatters()
    {
        IFormatter<int[]> arrayFormatter = FormatterProvider.Get<int[]>();
        IFormatter<int?[]> nullableArrayFormatter = FormatterProvider.Get<int?[]>();
        IFormatter<Queue<int>> queueFormatter = FormatterProvider.Get<Queue<int>>();
        IFormatter<Stack<int>> stackFormatter = FormatterProvider.Get<Stack<int>>();
        IFormatter<HashSet<int>> hashSetFormatter = FormatterProvider.Get<HashSet<int>>();
        IFormatter<List<int>> listFormatter = FormatterProvider.Get<List<int>>();
        IFormatter<List<int?>> nullableListFormatter = FormatterProvider.Get<List<int?>>();
        IFormatter<Dictionary<int, int>> dictFormatter = FormatterProvider.Get<Dictionary<int, int>>();
        IFormatter<Memory<int>> memoryFormatter = FormatterProvider.Get<Memory<int>>();
        IFormatter<ReadOnlyMemory<int>> readOnlyMemoryFormatter = FormatterProvider.Get<ReadOnlyMemory<int>>();
        IFormatter<(int, int)> tupleFormatter = FormatterProvider.Get<(int, int)>();
        IFormatter<(int, int, int, int, int)> tuple5Formatter = FormatterProvider.Get<(int, int, int, int, int)>();

        Assert.NotNull(arrayFormatter);
        Assert.NotNull(nullableArrayFormatter);
        Assert.NotNull(queueFormatter);
        Assert.NotNull(stackFormatter);
        Assert.NotNull(hashSetFormatter);
        Assert.NotNull(listFormatter);
        Assert.NotNull(nullableListFormatter);
        Assert.NotNull(dictFormatter);
        Assert.NotNull(memoryFormatter);
        Assert.NotNull(readOnlyMemoryFormatter);
        Assert.NotNull(tupleFormatter);
        Assert.NotNull(tuple5Formatter);
    }

    [Fact]
    public void GetWhenMemoryElementTypeIsManagedThrowsSerializationFailureException()
    {
        Assert.Throws<SerializationFailureException>(() => FormatterProvider.Get<Memory<string>>());
    }

    [Fact]
    public void RegisterOverridesFormatterCacheForTargetType()
    {
        StubFormatter formatter = new();

        FormatterProvider.Register<StubType>(formatter);
        IFormatter<StubType> resolved = FormatterProvider.Get<StubType>();

        Assert.Same(formatter, resolved);
    }

    [Fact]
    public void RegisterComplexWhenTypeIsUnsupportedThrowsSerializationFailureException()
    {
        Assert.Throws<SerializationFailureException>(() =>
            FormatterProvider.RegisterComplex<IUnsupported>(new UnsupportedFormatter()));
    }

    private sealed class StubType
    {
        public int Value { get; set; }
    }

    private sealed class StubFormatter : IFormatter<StubType>
    {
        public StubType Deserialize(ref DataReader reader) => new();
        public void Serialize(ref DataWriter writer, in StubType value) { }
    }

    private interface IUnsupported
    {
    }

    private sealed class UnsupportedFormatter : IFormatter<IUnsupported>
    {
        public IUnsupported Deserialize(ref DataReader reader) => throw new NotSupportedException();
        public void Serialize(ref DataWriter writer, in IUnsupported value) { }
    }
}

















