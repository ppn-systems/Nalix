// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Networking.Rpc;

/// <summary>
/// Marks an interface as an RPC service contract.
/// The Source Generator will automatically create a client proxy for this interface.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
public sealed class RpcServiceAttribute : Attribute
{
}
