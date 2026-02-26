// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Abstractions;

/// <summary>
/// Marks a type or method as permitted to use reserved OpCodes (0x0000-0x00FF), 
/// which are normally blocked for application-level use.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ReservedOpcodePermittedAttribute : Attribute { }
