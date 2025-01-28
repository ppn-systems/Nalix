using Notio.Shared.Configuration;
using Notio.Web.MimeTypes;
using Notio.Web.Utilities;
using System.Collections.Generic;

namespace Notio.Web.Internal;

internal sealed class MimeTypeCustomizer : ConfiguredObject, IMimeTypeCustomizer
{
    private readonly Dictionary<string, string> _customMimeTypes = [];
    private readonly Dictionary<(string, string), bool> _data = [];

    private bool? _defaultPreferCompression;

    public string GetMimeType(string extension)
    {
        _ = _customMimeTypes.TryGetValue(Validate.NotNull(nameof(extension), extension), out string? result);
        return result ?? string.Empty;
    }

    public bool TryDetermineCompression(string mimeType, out bool preferCompression)
    {
        (string type, string subtype) = MimeType.UnsafeSplit(
            Validate.MimeType(nameof(mimeType), mimeType, false));

        if (_data.TryGetValue((type, subtype), out preferCompression))
        {
            return true;
        }

        if (_data.TryGetValue((type, "*"), out preferCompression))
        {
            return true;
        }

        if (!_defaultPreferCompression.HasValue)
        {
            return false;
        }

        preferCompression = _defaultPreferCompression.Value;
        return true;
    }

    public void AddCustomMimeType(string extension, string mimeType)
    {
        EnsureConfigurationNotLocked();
        _customMimeTypes[Validate.NotNullOrEmpty(nameof(extension), extension)]
            = Validate.MimeType(nameof(mimeType), mimeType, false);
    }

    public void PreferCompression(string mimeType, bool preferCompression)
    {
        EnsureConfigurationNotLocked();
        (string type, string subtype) = MimeType.UnsafeSplit(
            Validate.MimeType(nameof(mimeType), mimeType, true));

        if (type == "*")
        {
            _defaultPreferCompression = preferCompression;
        }
        else
        {
            _data[(type, subtype)] = preferCompression;
        }
    }

    public void Lock()
    {
        LockConfiguration();
    }
}