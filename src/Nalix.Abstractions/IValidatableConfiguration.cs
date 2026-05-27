// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions;

/// <summary>
/// Defines an AOT-friendly interface for configuration classes to provide custom validation logic.
/// </summary>
public interface IValidatableConfiguration
{
    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    void Validate();
}
