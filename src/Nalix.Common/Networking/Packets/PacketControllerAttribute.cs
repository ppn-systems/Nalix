// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Specifies that the target class is a packet controller responsible for handling packet commands.
/// </summary>
/// <remarks>
/// This attribute is applied to classes containing methods that handle incoming packet commands.
/// The methods in the target class must adhere to the following requirements to be compatible with the handler compiler:
///
/// <para><b>Method Parameter Requirements:</b></para>
/// <list type="number">
/// <item>
/// <description>The method must have either <b>2</b> or <b>3</b> parameters.</description>
/// </item>
/// <item>
/// <description>
/// The first parameter must implement <see cref="IPacket"/>.
/// This parameter represents the packet being handled.
/// </description>
/// </item>
/// <item>
/// <description>
/// The second parameter must implement <see cref="IConnection"/>.
/// This parameter represents the connection on which the packet was received.
/// </description>
/// </item>
/// <item>
/// <description>
/// The third parameter (if present) must be of type <see cref="CancellationToken"/>.
/// This parameter is used for cooperative cancellation of the handler execution.
/// </description>
/// </item>
/// </list>
///
/// <para><b>Return Type:</b></para>
/// <list type="bullet">
/// <item>
/// <description>
/// The method may return one of the following:
/// <list type="number">
/// <item><description><see langword="void"/>.</description></item>
/// <item><description><see cref="Task"/> (for asynchronous methods).</description></item>
/// <item><description><see cref="ValueTask"/> (for lightweight asynchronous methods).</description></item>
/// <item>
/// <description>
/// A generic task (<see cref="Task{TResult}"/> or
/// <see cref="ValueTask{TResult}"/>), where TResult is the return type.
/// </description>
/// </item>
/// </list>
/// </description>
/// </item>
/// </list>
///
/// <para><b>Access Modifiers:</b></para>
/// <list type="bullet">
/// <item>
/// <description>The method must be <b>public</b>.</description>
/// </item>
/// <item>
/// <description>The method may be either <b>static</b> or <b>instance-based</b>, depending on the controller's design.</description>
/// </item>
/// </list>
///
/// <para><b>Error Handling:</b></para>
/// If the method does not meet any of the above requirements, the following exceptions may be thrown during the controller scanning process:
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="InvalidOperationException"/>: Thrown if the parameter count, types, or return type are invalid.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="System.Reflection.AmbiguousMatchException"/>: Thrown if multiple methods match the same operation code.
/// </description>
/// </item>
/// </list>
///
/// <example>
/// Example of a valid controller with the required handler methods:
/// <code>
/// [PacketController(name: "ExampleController", isActive: true, version: "1.1")]
/// public class ExampleController
/// {
///     // A handler with 2 parameters
///     public void HandleLogin(LoginPacket packet, IConnection connection)
///     {
///         // Handle the login logic here.
///     }
///
///     // A handler with 3 parameters
///     public ValueTask ProcessData(DataPacket packet, IConnection connection, CancellationToken token)
///     {
///         // Handle data processing logic here asynchronously.
///         return ValueTask.CompletedTask;
///     }
/// }
/// </code>
/// </example>
///
/// </remarks>
/// <param name="name">
/// The human-readable name of the packet controller.
/// Defaults to <c>"NONE"</c> if not provided.
/// </param>
/// <param name="isActive">
/// Indicates whether the controller is active and should handle packets.
/// Defaults to <c>true</c>.
/// </param>
/// <param name="version">
/// The version identifier for the packet controller.
/// Defaults to <c>"1.0"</c>.
/// </param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PacketControllerAttribute(
    string name = "NONE",
    bool isActive = true,
    string version = "1.0") : Attribute
{
    /// <summary>
    /// Gets the name of the packet controller.
    /// Used primarily for logging and debugging purposes.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the version string of the packet controller.
    /// </summary>
    public string Version { get; } = version;

    /// <summary>
    /// Gets a value indicating whether the packet controller is active.
    /// </summary>
    public bool IsActive { get; } = isActive;
}
