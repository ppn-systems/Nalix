using System.Text.Json.Serialization;

namespace Nalix.Common.Connection.Contracts;

/// <summary>
/// Provides a JSON serialization context for types in the Notio network dispatcher.
/// </summary>
[JsonSerializable(typeof(PingInfoDto))]
[JsonSerializable(typeof(ConnInfoDto))]
public partial class NetJsonCxt : JsonSerializerContext { }
