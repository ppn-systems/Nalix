namespace Notio.FileStorage;

public interface IFile
{
    byte[] Data { get; set; }
    string Name { get; set; }
}