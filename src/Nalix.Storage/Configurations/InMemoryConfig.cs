using Nalix.Storage.Generator;
using Nalix.Storage.Helpers.MimeTypes;
using System;

namespace Nalix.Storage.Configurations;

/// <summary>
/// Configured for in-memory file storage.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="InMemoryConfig"/> class.
/// </remarks>
/// <param name="generator">The file generator to use.</param>
/// <exception cref="ArgumentNullException">Thrown if <paramref name="generator"/> is null.</exception>
public class InMemoryConfig(IFileGenerator generator) : IFileStorageConfig<InMemoryConfig>
{
    /// <summary>
    /// Gets the file generator instance for generating files in memory.
    /// </summary>
    public IFileGenerator Generator { get; private set; } = generator ?? throw new ArgumentNullException(nameof(generator));

    /// <summary>
    /// Gets the MIME type resolver instance.
    /// </summary>
    public IMimeTypeResolver? MimeTypeResolver { get; private set; }

    /// <summary>
    /// Indicates whether file generation is enabled.
    /// </summary>
    public bool IsGenerationEnabled => Generator != null;

    /// <summary>
    /// Indicates whether the MIME type resolver is enabled.
    /// </summary>
    public bool IsMimeTypeResolverEnabled => MimeTypeResolver != null;

    /// <summary>
    /// Configures the file generator.
    /// </summary>
    public InMemoryConfig UseFileGenerator(IFileGenerator generator)
    {
        Generator = generator ?? throw new ArgumentNullException(nameof(generator));
        return this;
    }

    /// <summary>
    /// Configures the MIME type resolver.
    /// </summary>
    public InMemoryConfig UseMimeTypeResolver(IMimeTypeResolver resolver)
    {
        MimeTypeResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        return this;
    }
}
