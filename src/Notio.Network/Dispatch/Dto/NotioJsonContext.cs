using Notio.Network.Dispatch.Core.Dto;
using System.Text.Json.Serialization;

namespace Notio.Network.Dispatch.Dto;

/// <summary>
/// Provides a JSON serialization context for types in the Notio network dispatcher.
/// </summary>
[JsonSerializable(typeof(PingInfoDto))]
[JsonSerializable(typeof(ConnectionStatusDto))]
public partial class NotioJsonContext : JsonSerializerContext { }
