namespace Notio.FileStorage.Interfaces;

public interface IFileStorageSetting<T> where T : class
{
    IFileGenerator Generator { get; }

    T UseFileGenerator(IFileGenerator generator);
}