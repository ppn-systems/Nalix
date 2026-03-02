// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Common.Messaging.Protocols;
using Nalix.Framework.Injection;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Messaging.Controls;
using Nalix.Shared.Messaging.Text;
using Control = Nalix.Shared.Messaging.Controls.Control;

namespace DDOS.Helpers;

/// <summary>
/// Helper class để tạo các loại packet từ input của user
/// </summary>
public static class PacketBuilder
{
    /// <summary>
    /// Tạo Text256 packet
    /// </summary>
    public static Text256 CreateText256(String content, ProtocolType protocol = ProtocolType.TCP)
    {
        Text256 packet = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                 .Get<Text256>();
        packet.Initialize(content, protocol);
        return packet;
    }

    /// <summary>
    /// Tạo Text512 packet
    /// </summary>
    public static Text512 CreateText512(String content, ProtocolType protocol = ProtocolType.TCP)
    {
        Text512 packet = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                 .Get<Text512>();
        packet.Initialize(content, protocol);
        return packet;
    }

    /// <summary>
    /// Tạo Text1024 packet
    /// </summary>
    public static Text1024 CreateText1024(String content, ProtocolType protocol = ProtocolType.TCP)
    {
        Text1024 packet = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                  .Get<Text1024>();
        packet.Initialize(content, protocol);
        return packet;
    }

    /// <summary>
    /// Tạo Control packet
    /// </summary>
    public static Control CreateControl(
        ControlType type,
        UInt32 sequenceId = 0,
        ProtocolReason reason = ProtocolReason.NONE,
        ProtocolType protocol = ProtocolType.TCP)
    {
        Control packet = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                 .Get<Control>();
        packet.Initialize(type, sequenceId, reason, protocol);
        return packet;
    }

    /// <summary>
    /// Tạo Directive packet
    /// </summary>
    public static Directive CreateDirective(
        ControlType type,
        ProtocolReason reason,
        ProtocolAdvice action,
        UInt32 sequenceId = 0,
        ControlFlags flags = ControlFlags.NONE)
    {
        Directive packet = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                   .Get<Directive>();
        packet.Initialize(type, reason, action, sequenceId, flags);
        return packet;
    }

    /// <summary>
    /// Tạo Handshake packet
    /// </summary>
    public static Handshake CreateHandshake(Byte[] data, ProtocolType protocol = ProtocolType.TCP)
    {
        Handshake packet = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                   .Get<Handshake>();
        packet.Initialize(data, protocol);
        return packet;
    }
}