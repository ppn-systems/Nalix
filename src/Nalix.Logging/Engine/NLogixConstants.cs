// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Logging.Engine;

/// <summary>
/// Contains constant values used for logging.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class NLogixConstants
{
    /// <summary>
    /// The opening bracket character used in log formatting.
    /// </summary>
    public const char LogBracketOpen = '[';

    /// <summary>
    /// The closing bracket character used in log formatting.
    /// </summary>
    public const char LogBracketClose = ']';

    /// <summary>
    /// The default space separator used in log messages.
    /// </summary>
    public const char LogSpaceSeparator = ' ';

    /// <summary>
    /// The default dash separator used in log messages.
    /// </summary>
    public const char LogDashSeparator = '-';

    /// <summary>
    /// The default buffer size for logging operations.
    /// </summary>
    public const int DefaultLogBufferSize = 60;
}
