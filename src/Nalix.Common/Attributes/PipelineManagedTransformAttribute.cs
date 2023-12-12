// Copyright (c) 2025 PPN Corporation. All rights reserved.
namespace Nalix.Common.Attributes;

/// <summary>
/// Marks a packet type whose transformations (encryption, compression, etc.)
/// are handled by the transport pipeline rather than the packet's transformer.
/// When applied, the catalog builder will not bind transformer methods for this type.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PipelineManagedTransformAttribute : System.Attribute
{
}
