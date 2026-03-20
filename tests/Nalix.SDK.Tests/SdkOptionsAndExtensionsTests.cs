using System;
using Nalix.Common.Networking.Protocols;
using Nalix.SDK.Extensions;
using Nalix.SDK.Options;
using Nalix.SDK.Transport.Extensions;
using Xunit;

namespace Nalix.SDK.Tests;

public sealed class SdkOptionsAndExtensionsTests
{
    [Fact]
    public void RequestOptionsValidate_WhenTimeoutOrRetryNegative_ThrowsArgumentOutOfRangeException()
    {
        RequestOptions negativeTimeout = RequestOptions.Default.WithTimeout(-1);
        RequestOptions negativeRetry = RequestOptions.Default.WithRetry(-1);

        _ = Assert.Throws<ArgumentOutOfRangeException>(negativeTimeout.Validate);
        _ = Assert.Throws<ArgumentOutOfRangeException>(negativeRetry.Validate);
    }

    [Fact]
    public void RequestOptionsFluentMethods_ReturnNewImmutableCopies()
    {
        RequestOptions original = RequestOptions.Default;
        RequestOptions changed = original.WithTimeout(1500).WithRetry(2).WithEncrypt();

        Assert.Equal(RequestOptions.DefaultTimeoutMs, original.TimeoutMs);
        Assert.Equal(0, original.RetryCount);
        Assert.False(original.Encrypt);

        Assert.Equal(1500, changed.TimeoutMs);
        Assert.Equal(2, changed.RetryCount);
        Assert.True(changed.Encrypt);
    }

    [Fact]
    public void ProtocolStringExtensions_ForKnownValues_ReturnExpectedFriendlyText()
    {
        Assert.Equal("Please reconnect.", ProtocolAdvice.RECONNECT.ToDisplayString());
        Assert.Equal("Request timed out.", ProtocolReason.TIMEOUT.ToDisplayString());
    }

    [Fact]
    public void ProtocolStringExtensions_ForUnknownValues_ReturnFallbackText()
    {
        ProtocolAdvice unknownAdvice = (ProtocolAdvice)255;
        ProtocolReason unknownReason = (ProtocolReason)65535;

        Assert.Equal("Unknown action.", unknownAdvice.ToDisplayString());
        Assert.Equal("Unspecified error.", unknownReason.ToDisplayString());
    }

    [Fact]
    public void InlineDispatcherPost_WhenActionIsNull_DoesNothing()
    {
        InlineDispatcher dispatcher = new();

        Exception? ex = Record.Exception(() => dispatcher.Post(null!));

        Assert.Null(ex);
    }

    [Fact]
    public void InlineDispatcherPost_WhenActionExists_ExecutesSynchronously()
    {
        InlineDispatcher dispatcher = new();
        int value = 0;

        dispatcher.Post(() => value = 42);

        Assert.Equal(42, value);
    }

    [Fact]
    public void CompositeSubscriptionAdd_WhenDisposed_DisposesIncomingSubscriptionImmediately()
    {
        CompositeSubscription composite = new();
        composite.Dispose();

        TrackingDisposable tracking = new();
        composite.Add(tracking);

        Assert.True(tracking.Disposed);
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => this.Disposed = true;
    }
}
