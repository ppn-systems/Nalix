namespace Nalix.Shared.Tests.Configuration;

/// <summary>
/// Creates a temporary INI file for tests and cleans it up afterwards.
/// </summary>
public sealed class TempIniFile : System.IDisposable
{
    public System.String Path { get; }

    public TempIniFile(System.String content = null)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NalixTests"));
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NalixTests", $"cfg_{System.Guid.NewGuid():N}.ini");
        if (content is not null)
        {
            System.IO.File.WriteAllText(Path, content);
        }
    }

    public void Write(System.String content) => System.IO.File.WriteAllText(Path, content);

    public System.String ReadAll() => System.IO.File.Exists(Path) ? System.IO.File.ReadAllText(Path) : System.String.Empty;

    public void Dispose()
    {
        try { if (System.IO.File.Exists(Path)) { System.IO.File.Delete(Path); } } catch { /* ignore */ }
    }
}
