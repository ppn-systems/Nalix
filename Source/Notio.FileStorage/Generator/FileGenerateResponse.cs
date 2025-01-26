namespace Notio.FileStorage.Generator;

public class FileGenerateResponse(bool success, byte[] data, string error)
{
    public bool Success { get; private set; } = success;
    public byte[] Data { get; private set; } = data;
    public string Error { get; private set; } = error;
}