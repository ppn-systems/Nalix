using System;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Common.Networking.Protocols;
using Nalix.SDK.Transport.Extensions;
using Xunit;

namespace Nalix.SDK.Tests;

public sealed class CipherExtensionsTests
{
    [Fact]
    public async Task UpdateCipherAsync_WhenSuccessful_SwitchesAlgorithm()
    {
        FakeSession session = new(isConnected: true);
        session.Options.Algorithm = CipherSuiteType.Chacha20Poly1305;
        
        // Prepare ACK response
        Control ack = Control.Create();
        ack.Initialize(0, ControlType.CIPHER_UPDATE_ACK, sequenceId: 1, flags: PacketFlags.RELIABLE, reasonCode: ProtocolReason.NONE);
        session.EnqueueNextPacket(ack);

        await session.UpdateCipherAsync(CipherSuiteType.Salsa20Poly1305);
        
        Assert.Equal(CipherSuiteType.Salsa20Poly1305, session.Options.Algorithm);
        Assert.Equal(1, session.SendPacketCallCount);
    }

    [Fact]
    public async Task UpdateCipherAsync_WhenTimeout_RestoresPreviousAlgorithm()
    {
        FakeSession session = new(isConnected: true);
        session.Options.Algorithm = CipherSuiteType.Chacha20Poly1305;
        
        // No response enqueued -> timeout
        
        await Assert.ThrowsAsync<TimeoutException>(async () => 
            await session.UpdateCipherAsync(CipherSuiteType.Salsa20Poly1305, timeoutMs: 10));
            
        // Should be restored to original
        Assert.Equal(CipherSuiteType.Chacha20Poly1305, session.Options.Algorithm);
    }
}
