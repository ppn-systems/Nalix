// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Logging.Internal.Security;
using Xunit;

namespace Nalix.Logging.Tests.Security;

/// <summary>
/// Unit tests for LogSecurity class.
/// </summary>
public sealed class LogSecurityTests
{
    [Fact]
    public void SanitizeLogMessage_RemovesControlCharacters()
    {
        // Arrange
        var message = "Hello\x00\x01World\x02";

        // Act
        var sanitized = LogSecurity.SanitizeLogMessage(message);

        // Assert
        Assert.Equal("HelloWorld", sanitized);
    }

    [Fact]
    public void SanitizeLogMessage_PreservesWhitespace()
    {
        // Arrange
        var message = "Hello\t\n\rWorld";

        // Act
        var sanitized = LogSecurity.SanitizeLogMessage(message);

        // Assert
        Assert.Contains('\t', sanitized);
        Assert.Contains('\n', sanitized);
        Assert.Contains('\r', sanitized);
    }

    [Fact]
    public void RedactSensitiveData_RedactsCreditCardNumbers()
    {
        // Arrange
        var message = "Card: 1234-5678-9012-3456";

        // Act
        var redacted = LogSecurity.RedactSensitiveData(message);

        // Assert
        Assert.DoesNotContain("1234-5678-9012-3456", redacted);
        Assert.Contains("[REDACTED]", redacted);
    }

    [Fact]
    public void RedactSensitiveData_RedactsSSN()
    {
        // Arrange
        var message = "SSN: 123-45-6789";

        // Act
        var redacted = LogSecurity.RedactSensitiveData(message);

        // Assert
        Assert.DoesNotContain("123-45-6789", redacted);
        Assert.Contains("[REDACTED]", redacted);
    }

    [Fact]
    public void RedactSensitiveData_PartiallyRedactsEmail()
    {
        // Arrange
        var message = "Email: user@example.com";

        // Act
        var redacted = LogSecurity.RedactSensitiveData(message);

        // Assert
        Assert.DoesNotContain("user@example.com", redacted);
        Assert.Contains("@example.com", redacted); // Domain is kept
    }

    [Fact]
    public void RedactSensitiveData_RedactsPasswordValues()
    {
        // Arrange
        var message = "password=secret123";

        // Act
        var redacted = LogSecurity.RedactSensitiveData(message);

        // Assert
        Assert.DoesNotContain("secret123", redacted);
        Assert.Contains("password=[REDACTED]", redacted);
    }

    [Fact]
    public void ContainsSensitiveData_DetectsKeywords()
    {
        // Arrange
        var message = "User password is secret";

        // Act
        var contains = LogSecurity.ContainsSensitiveData(System.MemoryExtensions.AsSpan(message));

        // Assert
        Assert.True(contains);
    }

    [Fact]
    public void ContainsSensitiveData_DetectsCreditCards()
    {
        // Arrange
        var message = "Payment with 1234567890123456";

        // Act
        var contains = LogSecurity.ContainsSensitiveData(System.MemoryExtensions.AsSpan(message));

        // Assert
        Assert.True(contains);
    }

    [Fact]
    public void IsInputSafe_ReturnsTrueForSafeInput()
    {
        // Arrange
        var input = "Hello World!";

        // Act
        var isSafe = LogSecurity.IsInputSafe(input);

        // Assert
        Assert.True(isSafe);
    }

    [Fact]
    public void IsInputSafe_ReturnsFalseForNullBytes()
    {
        // Arrange
        var input = "Hello\x00World";

        // Act
        var isSafe = LogSecurity.IsInputSafe(input);

        // Assert
        Assert.False(isSafe);
    }

    [Fact]
    public void IsInputSafe_ReturnsFalseForExcessiveControlChars()
    {
        // Arrange
        var input = "Hello\x01\x02\x03\x04\x05\x06World";

        // Act
        var isSafe = LogSecurity.IsInputSafe(input);

        // Assert
        Assert.False(isSafe);
    }
}
