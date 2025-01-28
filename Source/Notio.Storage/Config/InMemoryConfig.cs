using Notio.Storage.Generator;
using Notio.Storage.MimeTypes;
using System;

namespace Notio.Storage.Config;

/// <summary>
/// Configured for in-memory file storage.
/// </summary>
public class InMemoryConfig : IFileStorageConfig<InMemoryConfig>
{
    /// <summary>
    /// Gets the file generator instance for generating files in memory.
    /// </summary>
    public IFileGenerator Generator { get; private set; }

    /// <summary>
    /// Gets the MIME type resolver instance.
    /// </summary>
    public IMimeTypeResolver MimeTypeResolver { get; private set; }

    /// <summary>
    /// Indicates whether file generation is enabled in this configuration.
    /// </summary>
    public bool IsGenerationEnabled => Generator is not null;

    /// <summary>
    /// Indicates whether the MIME type resolver is enabled in this configuration.
    /// </summary>
    public bool IsMimeTypeResolverEnabled => MimeTypeResolver is not null;

    /// <summary>
    /// Configures the file generator to use for this in-memory storage configuration.
    /// </summary>
    /// <param name="generator">The file generator to use.</param>
    /// <returns>The updated <see cref="InMemoryConfig"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="generator"/> is null.</exception>
    public InMemoryConfig UseFileGenerator(IFileGenerator generator)
    {
        Generator = generator ?? throw new ArgumentNullException(nameof(generator));
        return this;
    }

    /// <summary>
    /// Configures the MIME type resolver to use for this in-memory storage configuration.
    /// </summary>
    /// <param name="resolver">The MIME type resolver to use.</param>
    /// <returns>The updated <see cref="InMemoryConfig"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="resolver"/> is null.</exception>
    public InMemoryConfig UseMimeTypeResolver(IMimeTypeResolver resolver)
    {
        MimeTypeResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        return this;
    }
}