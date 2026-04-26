using System.IO;
using Nalix.Tools.Protogen.Domain.Interfaces;

namespace Nalix.Tools.Protogen.Infrastructure.IO;

public class LocalFileWriter : IFileWriter
{
    public void WriteAllText(string path, string content)
    {
        File.WriteAllText(path, content);
    }

    public void CreateDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}
