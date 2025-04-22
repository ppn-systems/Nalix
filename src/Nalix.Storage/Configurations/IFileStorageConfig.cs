using Nalix.Storage.Generator;

namespace Nalix.Storage.Configurations;

/// <summary>
/// Defines configuration options for file storage settings.
/// </summary>
/// <typeparam name="TConfig">The type of the configuration object.</typeparam>
public interface IFileStorageConfig<out TConfig> where TConfig : class
{
    /// <summary>
    /// Gets the file generator used in the configuration.
    /// </summary>
    IFileGenerator Generator { get; }

    /// <summary>
    /// Sets the file generator for the configuration.
    /// </summary>
    /// <param name="generator">The file generator to be used.</param>
    /// <returns>The configuration object with the updated file generator.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="generator"/> is null.</exception>
    TConfig UseFileGenerator(IFileGenerator generator);
}
