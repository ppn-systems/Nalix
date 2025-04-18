using Notio.Network.Dispatcher.Core.Dto;
using System.Text.Json.Serialization;

namespace Notio.Network.Dispatcher.Dto;

/// <summary>
/// Provides a JSON serialization context for types in the Notio network dispatcher.
/// </summary>
[JsonSerializable(typeof(PingInfoDto))]
[JsonSerializable(typeof(ConnectionStatusDto))]
public partial class NotioJsonContext : JsonSerializerContext { }
