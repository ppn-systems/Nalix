// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Internal.Security;

/// <summary>
/// Provides security utilities for log sanitization and sensitive data detection.
/// </summary>
/// <remarks>
/// This class helps prevent log injection attacks and protects sensitive data
/// from being logged inadvertently.
/// </remarks>
internal static class LogSecurity
{
    #region Fields

    // Common patterns for sensitive data
    private static readonly System.Text.RegularExpressions.Regex s_creditCardPattern =
        new(@"\b\d{4}[\s\-]?\d{4}[\s\-]?\d{4}[\s\-]?\d{4}\b",
            System.Text.RegularExpressions.RegexOptions.Compiled |
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static readonly System.Text.RegularExpressions.Regex s_ssnPattern =
        new(@"\b\d{3}[\s\-]?\d{2}[\s\-]?\d{4}\b",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex s_emailPattern =
        new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex s_ipAddressPattern =
        new(@"\b(?:\d{1,3}\.){3}\d{1,3}\b",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    // Keywords that often appear near sensitive data
    private static readonly System.String[] s_sensitiveKeywords =
    [
        "password", "pwd", "secret", "token", "key", "api_key", "apikey",
        "auth", "authorization", "credential", "ssn", "credit", "card",
        "cvv", "pin", "private", "nonce", "salt"
    ];

    private const System.String RedactedText = "[REDACTED]";

    #endregion Fields

    #region Public Methods

    /// <summary>
    /// Sanitizes a log message to prevent log injection attacks.
    /// </summary>
    /// <param name="message">The message to sanitize.</param>
    /// <returns>The sanitized message.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.String SanitizeLogMessage(System.String? message)
    {
        if (System.String.IsNullOrEmpty(message))
        {
            return System.String.Empty;
        }

        // Use span-based approach for better performance
        System.Span<System.Char> buffer = message.Length <= 512
            ? stackalloc System.Char[message.Length]
            : new System.Char[message.Length];

        System.MemoryExtensions.AsSpan(message).CopyTo(buffer);

        System.Int32 writeIndex = 0;

        for (System.Int32 i = 0; i < buffer.Length; i++)
        {
            System.Char c = buffer[i];

            // Remove control characters except common whitespace
            if (System.Char.IsControl(c))
            {
                // Allow: \t (tab), \n (newline), \r (carriage return)
                if (c != '\t' && c != '\n' && c != '\r')
                {
                    continue; // Skip control character
                }
            }

            buffer[writeIndex++] = c;
        }

        return new System.String(buffer[..writeIndex]);
    }

    /// <summary>
    /// Detects and redacts sensitive information from a log message.
    /// </summary>
    /// <param name="message">The message to check for sensitive data.</param>
    /// <returns>The message with sensitive data redacted.</returns>
    public static System.String RedactSensitiveData(System.String message)
    {
        if (System.String.IsNullOrEmpty(message))
        {
            return System.String.Empty;
        }

        // Redact credit card numbers
        message = s_creditCardPattern.Replace(message, RedactedText);

        // Redact SSNs
        message = s_ssnPattern.Replace(message, RedactedText);

        // Partial redaction for email addresses (keep domain visible for debugging)
        message = s_emailPattern.Replace(message, m =>
        {
            var email = m.Value;
            var atIndex = email.IndexOf('@');
            if (atIndex > 0)
            {
                // Use span-based operations for better performance
                return $"{email[0]}***{email[atIndex..]}";
            }
            return RedactedText;
        });

        // Check for sensitive keywords and redact nearby values
        message = RedactSensitiveKeywords(message);

        return message;
    }

    /// <summary>
    /// Determines if a message contains potentially sensitive information.
    /// </summary>
    /// <param name="message">The message to check.</param>
    /// <returns>True if the message may contain sensitive data; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean ContainsSensitiveData(System.ReadOnlySpan<System.Char> message)
    {
        if (message.IsEmpty)
        {
            return false;
        }

        // Check for sensitive keywords (case-insensitive)
        foreach (var keyword in s_sensitiveKeywords)
        {
            if (System.MemoryExtensions.Contains(message, keyword, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Convert to string for regex checks (only if keywords found)
        var messageStr = message.ToString();

        // Check for credit card patterns
        if (s_creditCardPattern.IsMatch(messageStr))
        {
            return true;
        }

        // Check for SSN patterns
        if (s_ssnPattern.IsMatch(messageStr))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Validates input to ensure it's safe for logging.
    /// </summary>
    /// <param name="input">The input to validate.</param>
    /// <returns>True if the input is safe; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean IsInputSafe(System.String? input)
    {
        if (System.String.IsNullOrEmpty(input))
        {
            return true;
        }

        // Check for common injection patterns
        System.ReadOnlySpan<System.Char> span = System.MemoryExtensions.AsSpan(input);

        // Check for null bytes (often used in log injection)
        if (System.MemoryExtensions.Contains(span, '\0'))
        {
            return false;
        }

        // Check for excessive control characters
        System.Int32 controlCharCount = 0;
        foreach (System.Char c in span)
        {
            if (System.Char.IsControl(c) && c != '\t' && c != '\n' && c != '\r')
            {
                controlCharCount++;
                if (controlCharCount > 5) // Allow up to 5 control chars
                {
                    return false;
                }
            }
        }

        return true;
    }

    #endregion Public Methods

    #region Private Methods

    private static System.String RedactSensitiveKeywords(System.String message)
    {
        foreach (var keyword in s_sensitiveKeywords)
        {
            // Look for patterns like "password=value" or "password: value"
            var pattern = $@"({keyword})\s*[=:]\s*([^\s,;}}]+)";
            var regex = new System.Text.RegularExpressions.Regex(
                pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            message = regex.Replace(message, $"$1={RedactedText}");
        }

        return message;
    }

    #endregion Private Methods
}
