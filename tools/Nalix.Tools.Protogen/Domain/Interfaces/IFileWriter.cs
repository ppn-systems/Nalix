namespace Nalix.Tools.Protogen.Domain.Interfaces;

public interface IFileWriter
{
    void WriteAllText(string path, string content);
    void CreateDirectory(string path);
}
