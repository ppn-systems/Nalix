namespace Notio.FileStorage.Models;

public class LocalFile(byte[] data, string fileName) : IFile
{
    public byte[] Data { get; set; } = data;
    public string Name { get; set; } = fileName;
}