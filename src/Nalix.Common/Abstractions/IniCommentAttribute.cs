// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;

namespace Nalix.Common.Abstractions;

/// <summary>
/// Specifies a comment to be written above a section or key in the INI file.
/// Can be applied to both classes (section comment) and properties (key comment).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class IniCommentAttribute : Attribute
{
    /// <summary>
    /// Gets the comment text. Supports multi-line via \n.
    /// </summary>
    public string Comment { get; }

    /// <summary>
    /// Ini comment attribute constructor.
    /// </summary>
    /// <param name="comment">
    /// The comment text to write above the target section or key.
    /// </param>
    public IniCommentAttribute(string comment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(comment, nameof(comment));
        this.Comment = comment;
    }
}
