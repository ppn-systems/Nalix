namespace Notio.FileStorage.Models;

public class FileMeta(string key, string value)
{
    public string Key { get; private set; } = key;

    public string Value { get; private set; } = value;
}