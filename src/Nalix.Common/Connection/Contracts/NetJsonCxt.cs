namespace Nalix.Common.Connection.Contracts;

/// <summary>
/// Provides a JSON serialization context for types in the Notio network dispatcher.
/// </summary>
[System.Text.Json.Serialization.JsonSerializable(typeof(PingInfoDto))]
[System.Text.Json.Serialization.JsonSerializable(typeof(ConnInfoDto))]
public partial class NetJsonCxt : System.Text.Json.Serialization.JsonSerializerContext
{ }
