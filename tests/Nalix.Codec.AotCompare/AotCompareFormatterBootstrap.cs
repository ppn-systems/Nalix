// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#if NALIX_AOT
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.AotCompare;
using Nalix.Codec.DataFrames;
using Nalix.Environment.Extensions;
using Nalix.Environment.Memory;
using Nalix.Codec.Serialization;

internal static class AotCompareFormatterBootstrap
{
    private static UInt32 AutoMagic<T>()
    {
        ReadOnlySpan<Char> name = typeof(T).FullName.AsSpan();
        Span<Byte> bytes = stackalloc Byte[System.Text.Encoding.UTF8.GetMaxByteCount(name.Length)];
        Int32 written = System.Text.Encoding.UTF8.GetBytes(name, bytes);
        UInt32 hash = 2166136261u;
        foreach (Byte b in bytes[..written])
        {
            hash ^= b;
            hash *= 16777619u;
        }
        return hash;
    }
    public static void Register()
    {
        RegisterPacket(new ComplexCollectionPacketFormatter());
        RegisterPacket(new GraphPacketFormatter());
        RegisterPacket(new LargeDataPacketFormatter());
        RegisterPacket(new NullStressPacketFormatter());
        RegisterPacket(new EnumListPacketFormatter());

        FormatterProvider.Register(new Tuple5Formatter());
        FormatterProvider.Register(new Tuple3Formatter());
        FormatterProvider.Register(new NodeMetaFormatter());
    }

    private static void RegisterPacket<T>(PacketFormatter<T> formatter) where T : PacketBase<T>, new()
    {
        FormatterProvider.RegisterComplex(formatter);
    }

    private abstract class PacketFormatter<T> : IFormatter<T>, IFillableFormatter<T> where T : PacketBase<T>, new()
    {
        public abstract void Serialize(ref DataWriter writer, in T value);
        public abstract T Deserialize(ref DataReader reader);
        public abstract void Fill(ref DataReader reader, T value);
    }

    private sealed class Tuple5Formatter : IFormatter<(Int32, String, Boolean, Double, Int64)>
    {
        public void Serialize(ref DataWriter writer, in (Int32, String, Boolean, Double, Int64) value)
        {
            writer.Write(value.Item1);
            FormatterProvider.Get<String>().Serialize(ref writer, value.Item2);
            writer.Write(value.Item3);
            writer.Write(value.Item4);
            writer.Write(value.Item5);
        }

        public (Int32, String, Boolean, Double, Int64) Deserialize(ref DataReader reader)
            => (reader.ReadInt32(), FormatterProvider.Get<String>().Deserialize(ref reader), reader.ReadBoolean(), reader.ReadDouble(), reader.ReadInt64());
    }

    private sealed class Tuple3Formatter : IFormatter<(Int32, String, Boolean)>
    {
        public void Serialize(ref DataWriter writer, in (Int32, String, Boolean) value)
        {
            writer.Write(value.Item1);
            FormatterProvider.Get<String>().Serialize(ref writer, value.Item2);
            writer.Write(value.Item3);
        }

        public (Int32, String, Boolean) Deserialize(ref DataReader reader)
            => (reader.ReadInt32(), FormatterProvider.Get<String>().Deserialize(ref reader), reader.ReadBoolean());
    }

    private sealed class ListFormatter<T> : IFormatter<List<T>>
    {
        public void Serialize(ref DataWriter writer, in List<T> value)
        {
            if (value is null)
            {
                writer.Write(-1);
                return;
            }

            writer.Write(value.Count);
            if (value.Count == 0)
            {
                return;
            }

            if (typeof(T) == typeof(Int32))
            {
                foreach (T item in value)
                {
                    writer.Write((Int32)(Object)item!);
                }
                return;
            }

            if (typeof(T) == typeof(PacketPriority))
            {
                foreach (T item in value)
                {
                    writer.Write((Byte)(PacketPriority)(Object)item!);
                }
                return;
            }

            IFormatter<T> formatter = FormatterProvider.Get<T>();
            foreach (T item in value)
            {
                formatter.Serialize(ref writer, item);
            }
        }

        public List<T> Deserialize(ref DataReader reader)
        {
            Int32 count = reader.ReadInt32();
            if (count < 0)
            {
                return null!;
            }

            List<T> list = new(count);
            if (count == 0)
            {
                return list;
            }

            if (typeof(T) == typeof(Int32))
            {
                for (Int32 i = 0; i < count; i++)
                {
                    list.Add((T)(Object)reader.ReadInt32());
                }
                return list;
            }

            if (typeof(T) == typeof(PacketPriority))
            {
                for (Int32 i = 0; i < count; i++)
                {
                    list.Add((T)(Object)(PacketPriority)reader.ReadByte());
                }
                return list;
            }

            IFormatter<T> formatter = FormatterProvider.Get<T>();
            for (Int32 i = 0; i < count; i++)
            {
                list.Add(formatter.Deserialize(ref reader));
            }
            return list;
        }
    }

    private sealed class DictionaryFormatter<TKey, TValue> : IFormatter<Dictionary<TKey, TValue>> where TKey : notnull
    {
        public void Serialize(ref DataWriter writer, in Dictionary<TKey, TValue> value)
        {
            if (value is null)
            {
                writer.Write(-1);
                return;
            }

            writer.Write(value.Count);
            IFormatter<TKey> keyFormatter = FormatterProvider.Get<TKey>();
            IFormatter<TValue> valueFormatter = FormatterProvider.Get<TValue>();
            foreach (KeyValuePair<TKey, TValue> pair in value)
            {
                keyFormatter.Serialize(ref writer, pair.Key);
                valueFormatter.Serialize(ref writer, pair.Value);
            }
        }

        public Dictionary<TKey, TValue> Deserialize(ref DataReader reader)
        {
            Int32 count = reader.ReadInt32();
            if (count < 0)
            {
                return null!;
            }

            Dictionary<TKey, TValue> dictionary = new(count);
            IFormatter<TKey> keyFormatter = FormatterProvider.Get<TKey>();
            IFormatter<TValue> valueFormatter = FormatterProvider.Get<TValue>();
            for (Int32 i = 0; i < count; i++)
            {
                dictionary[keyFormatter.Deserialize(ref reader)] = valueFormatter.Deserialize(ref reader);
            }
            return dictionary;
        }
    }

    private sealed class QueueFormatter<T> : IFormatter<Queue<T>>
    {
        public void Serialize(ref DataWriter writer, in Queue<T> value)
        {
            if (value is null)
            {
                writer.Write(-1);
                return;
            }

            writer.Write(value.Count);
            IFormatter<T> formatter = FormatterProvider.Get<T>();
            foreach (T item in value)
            {
                formatter.Serialize(ref writer, item);
            }
        }

        public Queue<T> Deserialize(ref DataReader reader)
        {
            Int32 count = reader.ReadInt32();
            if (count < 0)
            {
                return null!;
            }

            Queue<T> queue = new(count);
            IFormatter<T> formatter = FormatterProvider.Get<T>();
            for (Int32 i = 0; i < count; i++)
            {
                queue.Enqueue(formatter.Deserialize(ref reader));
            }
            return queue;
        }
    }

    private sealed class HashSetFormatter<T> : IFormatter<HashSet<T>>
    {
        public void Serialize(ref DataWriter writer, in HashSet<T> value)
        {
            if (value is null)
            {
                writer.Write(-1);
                return;
            }

            writer.Write(value.Count);
            if (value.Count == 0)
            {
                return;
            }

            if (typeof(T) == typeof(Single))
            {
                foreach (T item in value)
                {
                    writer.Write((Single)(Object)item!);
                }
                return;
            }

            IFormatter<T> formatter = FormatterProvider.Get<T>();
            foreach (T item in value)
            {
                formatter.Serialize(ref writer, item);
            }
        }

        public HashSet<T> Deserialize(ref DataReader reader)
        {
            Int32 count = reader.ReadInt32();
            if (count < 0)
            {
                return null!;
            }

            HashSet<T> set = new(count);
            if (count == 0)
            {
                return set;
            }

            if (typeof(T) == typeof(Single))
            {
                for (Int32 i = 0; i < count; i++)
                {
                    set.Add((T)(Object)reader.ReadSingle());
                }
                return set;
            }

            IFormatter<T> formatter = FormatterProvider.Get<T>();
            for (Int32 i = 0; i < count; i++)
            {
                set.Add(formatter.Deserialize(ref reader));
            }
            return set;
        }
    }

    private sealed class NodeMetaFormatter : IFormatter<NodeMeta>
    {
        public void Serialize(ref DataWriter writer, in NodeMeta value)
            => writer.Write(value.Id);

        public NodeMeta Deserialize(ref DataReader reader)
            => new() { Id = reader.ReadInt32() };
    }

    private sealed class ComplexCollectionPacketFormatter : PacketFormatter<ComplexCollectionPacket>
    {
        public override void Serialize(ref DataWriter writer, in ComplexCollectionPacket value)
        {
            writer.WriteUnmanaged(value.Header);
            FormatterProvider.Get<List<Int32>>().Serialize(ref writer, value.IntList);
            FormatterProvider.Get<Dictionary<String, Int64>>().Serialize(ref writer, value.StringLongDict);
            FormatterProvider.Get<Queue<String>>().Serialize(ref writer, value.StringQueue);
            FormatterProvider.Get<HashSet<Single>>().Serialize(ref writer, value.FloatSet);
            FormatterProvider.Get<(Int32, String, Boolean)>().Serialize(ref writer, value.Tuple3);
        }

        public override ComplexCollectionPacket Deserialize(ref DataReader reader)
        {
            ComplexCollectionPacket value = new();
            Fill(ref reader, value);
            return value;
        }

        public override void Fill(ref DataReader reader, ComplexCollectionPacket value)
        {
            value.Header = reader.ReadUnmanaged<PacketHeader>();
            value.IntList = FormatterProvider.Get<List<Int32>>().Deserialize(ref reader);
            value.StringLongDict = FormatterProvider.Get<Dictionary<String, Int64>>().Deserialize(ref reader);
            value.StringQueue = FormatterProvider.Get<Queue<String>>().Deserialize(ref reader);
            value.FloatSet = FormatterProvider.Get<HashSet<Single>>().Deserialize(ref reader);
            value.Tuple3 = FormatterProvider.Get<(Int32, String, Boolean)>().Deserialize(ref reader);
        }
    }

    private sealed class GraphPacketFormatter : PacketFormatter<GraphPacket>
    {
        public override void Serialize(ref DataWriter writer, in GraphPacket value)
        {
            writer.WriteUnmanaged(value.Header);
            FormatterProvider.Get<String>().Serialize(ref writer, value.Name);
            FormatterProvider.Get<List<GraphPacket>>().Serialize(ref writer, value.Nodes);
            FormatterProvider.Get<NodeMeta>().Serialize(ref writer, value.Meta);
        }

        public override GraphPacket Deserialize(ref DataReader reader)
        {
            GraphPacket value = new();
            Fill(ref reader, value);
            return value;
        }

        public override void Fill(ref DataReader reader, GraphPacket value)
        {
            if (reader.BytesRemaining >= PacketHeader.Size)
            {
                UInt32 magic = BinaryPrimitives.ReadUInt32LittleEndian(
                    MemoryMarshal.CreateReadOnlySpan(ref reader.GetSpanReference(sizeof(UInt32)), sizeof(UInt32)));
                if (magic == AutoMagic<GraphPacket>())
                {
                    value.Header = reader.ReadUnmanaged<PacketHeader>();
                }
            }

            value.Name = FormatterProvider.Get<String>().Deserialize(ref reader);
            value.Nodes = FormatterProvider.Get<List<GraphPacket>>().Deserialize(ref reader);
            value.Meta = FormatterProvider.Get<NodeMeta>().Deserialize(ref reader);
        }
    }

    private sealed class LargeDataPacketFormatter : PacketFormatter<LargeDataPacket>
    {
        public override void Serialize(ref DataWriter writer, in LargeDataPacket value)
        {
            writer.WriteUnmanaged(value.Header);
            FormatterProvider.Get<List<String>>().Serialize(ref writer, value.Payload);
        }

        public override LargeDataPacket Deserialize(ref DataReader reader)
        {
            LargeDataPacket value = new();
            Fill(ref reader, value);
            return value;
        }

        public override void Fill(ref DataReader reader, LargeDataPacket value)
        {
            value.Header = reader.ReadUnmanaged<PacketHeader>();
            value.Payload = FormatterProvider.Get<List<String>>().Deserialize(ref reader);
        }
    }

    private sealed class NullStressPacketFormatter : PacketFormatter<NullStressPacket>
    {
        public override void Serialize(ref DataWriter writer, in NullStressPacket value)
        {
            writer.WriteUnmanaged(value.Header);
            FormatterProvider.Get<List<String>>().Serialize(ref writer, value.Items);
        }

        public override NullStressPacket Deserialize(ref DataReader reader)
        {
            NullStressPacket value = new();
            Fill(ref reader, value);
            return value;
        }

        public override void Fill(ref DataReader reader, NullStressPacket value)
        {
            value.Header = reader.ReadUnmanaged<PacketHeader>();
            value.Items = FormatterProvider.Get<List<String>>().Deserialize(ref reader);
        }
    }

    private sealed class EnumListPacketFormatter : PacketFormatter<EnumListPacket>
    {
        public override void Serialize(ref DataWriter writer, in EnumListPacket value)
        {
            writer.WriteUnmanaged(value.Header);
            FormatterProvider.Get<List<PacketPriority>>().Serialize(ref writer, value.Priorities);
        }

        public override EnumListPacket Deserialize(ref DataReader reader)
        {
            EnumListPacket value = new();
            Fill(ref reader, value);
            return value;
        }

        public override void Fill(ref DataReader reader, EnumListPacket value)
        {
            value.Header = reader.ReadUnmanaged<PacketHeader>();
            value.Priorities = FormatterProvider.Get<List<PacketPriority>>().Deserialize(ref reader);
        }
    }
}
#endif
