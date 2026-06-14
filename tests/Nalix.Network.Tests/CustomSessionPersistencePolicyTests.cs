// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Runtime.Sessions;
using NSubstitute;
using Xunit;

namespace Nalix.Network.Tests;

public sealed class CustomSessionPersistencePolicyTests
{
    private sealed class CustomPolicy : ISessionPersistencePolicy
    {
        public bool ShouldPersist(IConnection connection)
        {
            return connection.Attributes.ContainsKey(AttributeKey.FromName("should_save"));
        }
    }

    [Fact]
    public async Task SessionService_WithCustomPolicy_RespectsCustomLogic()
    {
        // Arrange
        var mockConnection = Substitute.For<IConnection>();
        var attributes = Nalix.Framework.Memory.Objects.ObjectMap<AttributeKey, object>.Rent();
        mockConnection.Attributes.Returns(attributes);
        mockConnection.IsDisposed.Returns(false);

        var policy = new CustomPolicy();
        var store = new InMemorySessionStore();
        var service = new SessionService(store: store, policy: policy);

        // Act & Assert (Should not persist because "should_save" is missing)
        await service.SaveSessionAsync(mockConnection);
        var report1 = service.GenerateReport();
        report1.Should().Contain("Total Stores Rejected   : 1");

        // Act & Assert (Should persist because "should_save" is present)
        attributes[AttributeKey.FromName("should_save")] = true;
        await service.SaveSessionAsync(mockConnection);
        var report2 = service.GenerateReport();
        report2.Should().Contain("Total Stores Succeeded  : 1");

        attributes.Return();
    }

    [Fact]
    public void DefaultSessionPersistencePolicy_Fails_WhenHandshakeNotEstablished()
    {
        // Arrange
        var mockConnection = Substitute.For<IConnection>();
        var attributes = Nalix.Framework.Memory.Objects.ObjectMap<AttributeKey, object>.Rent();
        mockConnection.Attributes.Returns(attributes);

        var policy = new DefaultSessionPersistencePolicy();

        // Act & Assert
        policy.ShouldPersist(mockConnection).Should().BeFalse();

        attributes.Return();
    }

    [Fact]
    public void DefaultSessionPersistencePolicy_Succeeds_WhenHandshakeEstablishedAndEnoughAttributes()
    {
        // Arrange
        var mockConnection = Substitute.For<IConnection>();
        var attributes = Nalix.Framework.Memory.Objects.ObjectMap<AttributeKey, object>.Rent();
        attributes[ConnectionAttributes.HandshakeEstablished] = true;
        for (int i = 0; i < 20; i++)
        {
            attributes[AttributeKey.FromName($"key_{i}")] = i;
        }
        mockConnection.Attributes.Returns(attributes);

        var policy = new DefaultSessionPersistencePolicy();

        // Act & Assert
        policy.ShouldPersist(mockConnection).Should().BeTrue();

        attributes.Return();
    }
}
