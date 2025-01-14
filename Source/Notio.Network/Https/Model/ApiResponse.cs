namespace Notio.Network.Https;

public class ApiResponse
{
    public object? Data { get; set; }
    public string? Error { get; set; }
    public bool Success => Error == null;
}