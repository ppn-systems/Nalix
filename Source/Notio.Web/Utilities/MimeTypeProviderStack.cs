using Notio.Web.MimeTypes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Notio.Web.Utilities;

/// <summary>
/// <para>Manages a stack of MIME type providers.</para>
/// <para>This API supports the Notio infrastructure and is not intended to be used directly from your code.</para>
/// </summary>
/// <seealso cref="IMimeTypeProvider" />
public sealed class MimeTypeProviderStack : IMimeTypeProvider
{
    private readonly Stack<IMimeTypeProvider> _providers = new();

    /// <summary>
    /// <para>Pushes the specified MIME type provider on the stack.</para>
    /// <para>This API supports the Notio infrastructure and is not intended to be used directly from your code.</para>
    /// </summary>
    /// <param name="provider">The <see cref="IMimeTypeProvider"/> interface to push on the stack.</param>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/>is <see langword="null"/>.</exception>
    public void Push(IMimeTypeProvider provider)
    {
        _providers.Push(Validate.NotNull(nameof(provider), provider));
    }

    /// <summary>
    /// <para>Removes the most recently added MIME type provider from the stack.</para>
    /// <para>This API supports the Notio infrastructure and is not intended to be used directly from your code.</para>
    /// </summary>
    public void Pop()
    {
        _ = _providers.Pop();
    }

    /// <inheritdoc />
    public string GetMimeType(string extension)
    {
        string? result = _providers.Select(p => p.GetMimeType(extension))
            .FirstOrDefault(m => m != null);

        if (result == null)
        {
            _ = MimeType.Associations.TryGetValue(extension, out result);
        }

        return result ?? string.Empty;
    }

    /// <inheritdoc />
    public bool TryDetermineCompression(string mimeType, out bool preferCompression)
    {
        foreach (IMimeTypeProvider provider in _providers)
        {
            if (provider.TryDetermineCompression(mimeType, out preferCompression))
            {
                return true;
            }
        }

        preferCompression = default;
        return false;
    }
}