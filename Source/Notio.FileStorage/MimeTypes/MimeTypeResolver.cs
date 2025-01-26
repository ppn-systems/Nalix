using System;
using System.Collections.Generic;
using System.Linq;

namespace Notio.FileStorage.MimeTypes;

public class MimeTypeResolver : IMimeTypeResolver
{
    private readonly string _defaultMimeType = "application/octet-stream";

    private readonly IList<MimeTypeMapper> _mappings =
    [
        // Ảnh
        new("BMP", ".bmp", "image/bmp", new MimeTypePattern([66, 77])),
        new("GIF", ".gif", "image/gif", new MimeTypePattern([71, 73, 70, 56])),
        new("GIF", ".gif", "image/gif", new MimeTypePattern("47 49 46 38 37 61")),
        new("GIF", ".gif", "image/gif", new MimeTypePattern("47 49 46 38 39 61")),
        new("ICO", ".ico", "image/x-icon", new MimeTypePattern([0, 0, 1, 0])),
        new("JPG", ".jpg", "image/jpeg", new MimeTypePattern([255, 216, 255])),
        new("JPG", ".jpg", "image/jpeg", new MimeTypePattern("FF D8 FF E0")),
        new("JPG", ".jpg", "image/jpeg", new MimeTypePattern("FF D8 FF E1")),
        new("JPG", ".jpg", "image/jpeg", new MimeTypePattern("FF D8 FF E8")),
        new("PNG", ".png", "image/png", new MimeTypePattern([137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82])),
        new("TIFF", ".tiff", "image/tiff", new MimeTypePattern([73, 73, 42, 0])),
        new("SVG", ".svg", "image/svg+xml", new MimeTypePattern("3C 73 76 67")),
        new("SVG", ".svg", "image/svg+xml", new MimeTypePattern("3C 3F 78 6D 6C")),
        new("WebP", ".webp", "image/webp", new MimeTypePattern([0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50])),
        new("PSD", ".psd", "image/vnd.adobe.photoshop", new MimeTypePattern([0x38, 0x42, 0x50, 0x53])),
        new("HEIC", ".heic", "image/heif", new MimeTypePattern("00 00 00 20 66 74 79 70 68 65 69 63")),

        // Video
        new("AVI", ".avi", "video/x-msvideo", new MimeTypePattern([82, 73, 70, 70])),
        new("AVI", ".avi", "video/x-msvideo", new MimeTypePattern("41 56 49 20 4C 49 53 54")),
        new("FLV", ".flv", "video/x-flv", new MimeTypePattern("46 4C 56 01")),
        new("MKV", ".mkv", "video/x-matroska", new MimeTypePattern([26, 69, 75, 42])),
        new("MOV", ".mov", "video/quicktime", new MimeTypePattern([0, 0, 0, 14, 66, 74, 71, 32])),
        new("MP4", ".mp4", "video/mp4", new MimeTypePattern("66 74 79 70 33 67 70 35", 4)),
        new("MP4", ".mp4", "video/mp4", new MimeTypePattern("66 74 79 70 4D 53 4E 56", 4)),
        new("MP4", ".mp4", "video/mp4", new MimeTypePattern("00 00 00 14 66 74 79 70 69 73 6F 6D")),
        new("MP4", ".mp4", "video/mp4", new MimeTypePattern("00 00 00 18 66 74 79 70 33 67 70 35")),
        new("MP4", ".mp4", "video/mp4", new MimeTypePattern("00 00 00 1C 66 74 79 70 4D 53 4E 56 01 29 00 46 4D 53 4E 56 6D 70 34 32")),
        new("MP4", ".mp4", "video/mp4", new MimeTypePattern("00 00 00 20 66 74 79 70 69 73 6F 36 00 00 00 01 6D 70 34 32 69 73 6F 36 61 76 63 31 69 73 6F 6D")),
        new("MPG", ".mpeg", "video/mpeg", new MimeTypePattern("00 00 01 B3")),
        new("MPG", ".mpeg", "video/mpeg", new MimeTypePattern("00 00 01 BA")),
        new("WEBM", ".webm", "video/webm", new MimeTypePattern([1, 23, 41, 50, 53])),
        new("WMV_WMA", ".wma", "audio/x-ms-wma", new MimeTypePattern([48, 38, 178, 117, 142, 102, 207, 17, 166, 217, 0, 170, 0, 98, 206, 108])),
        new("3GP", ".3gp", "video/3gpp", new MimeTypePattern("00 00 00 20 66 74 79 70 33 67 70")),
        new("M4V", ".m4v", "video/x-m4v", new MimeTypePattern([0x66, 0x74, 0x79, 0x70, 0x4D, 0x34, 0x56, 0x20])), // ftypM4V
        new("AVCHD", ".mts", "video/avchd", new MimeTypePattern([0x47, 0x40, 0x00, 0x10])),

        // Âm thanh
        new("FLAC", ".flac", "audio/flac", new MimeTypePattern([102, 76, 97, 67])),
        new("M4A", ".m4a", "audio/mp4", new MimeTypePattern("66 74 79 70 4D 34 41 20", 4)),
        new("MP3", ".mpga", "audio/mpeg", new MimeTypePattern([255, 251, 48])),
        new("OGG_AUDIO", ".ogg", "audio/ogg", new MimeTypePattern([79, 103, 103, 83])),
        new("WAV", ".wav", "audio/wav", new MimeTypePattern([82, 73, 70, 70])),
        new("WMA", ".wma", "audio/x-ms-wma", new MimeTypePattern([48, 38, 178, 117, 142, 102])),
        new("AAC", ".aac", "audio/aac", new MimeTypePattern([0xFF, 0xF1])),
        new("AAC", ".aac", "audio/aac", new MimeTypePattern([0xFF, 0xF9])),
        new("AIFF", ".aiff", "audio/aiff", new MimeTypePattern([0x46, 0x4F, 0x52, 0x4D, 0x00])), // FORM
        new("MIDI", ".mid", "audio/midi", new MimeTypePattern([0x4D, 0x54, 0x68, 0x64])), // MThd
        new("Opus", ".opus", "audio/opus", new MimeTypePattern([0x4F, 0x70, 0x75, 0x73, 0x48, 0x65, 0x61, 0x64])), // OpusHead

        // Tài liệu
        new("MD", ".md", "text/markdown", new MimeTypePattern([35, 32])),
        new("TXT", ".txt", "text/plain", new MimeTypePattern([74, 79, 72, 78, 88, 67])),
        new("CSV", ".csv", "text/csv", new MimeTypePattern([34, 44, 13, 10])),
        new("DOC", ".doc", "application/msword", new MimeTypePattern([208, 207, 17, 224, 161, 177, 26, 225])),
        new("DOCX", ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", new MimeTypePattern([80, 75, 3, 4])),
        new("HTML", ".html", "text/html", new MimeTypePattern([60, 33, 44, 45, 47, 38])),
        new("JSON", ".json", "application/json", new MimeTypePattern([123, 34, 99, 111, 110])),
        new("PDF", ".pdf", "application/pdf", new MimeTypePattern([37, 80, 68, 70, 45, 49, 46])),
        new("PPTX", ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation", new MimeTypePattern([80, 75, 3, 4])),
        new("XLSX", ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", new MimeTypePattern([80, 75, 3, 4])),
        new("XML", ".xml", "application/xml", new MimeTypePattern([60, 63, 120, 109, 108])),
        new("EPUB", ".epub", "application/epub+zip", new MimeTypePattern([0x50, 0x4B, 0x03, 0x04])), // ZIP + require mimetype check
        new("ODT", ".odt", "application/vnd.oasis.opendocument.text", new MimeTypePattern([0x50, 0x4B, 0x03, 0x04])),
        new("RTF", ".rtf", "application/rtf", new MimeTypePattern([0x7B, 0x5C, 0x72, 0x74, 0x66, 0x31])), // {\rtf1
        new("XPS", ".xps", "application/vnd.ms-xpsdocument", new MimeTypePattern([0x50, 0x4B, 0x03, 0x04])),

        // Nén & Archive
        new("7Z", ".7z", "application/x-7z-compressed", new MimeTypePattern([55, 122])),
        new("BZ2", ".bz2", "application/x-bzip2", new MimeTypePattern([66, 90, 104])),
        new("GZ", ".gz", "application/gzip", new MimeTypePattern([31, 139, 8])),
        new("RAR", ".rar", "application/x-rar-compressed", new MimeTypePattern([82, 97, 114, 33, 26, 7, 0])),
        new("TAR", ".tar", "application/x-tar", new MimeTypePattern([75, 10, 30, 12])),
        new("ZIP", ".zip", "application/zip", new MimeTypePattern([80, 75, 3, 4])),
        new("ZIP_DOCX", ".zip", "application/x-zip-compressed", new MimeTypePattern([80, 75, 3, 4])),
        new("CAB", ".cab", "application/vnd.ms-cab-compressed", new MimeTypePattern([0x4D, 0x53, 0x43, 0x46])), // MSCF
        new("DEB", ".deb", "application/x-debian-package", new MimeTypePattern([0x21, 0x3C, 0x61, 0x72, 0x63, 0x68, 0x3E])), // !<arch>
        new("RPM", ".rpm", "application/x-rpm", new MimeTypePattern([0xED, 0xAB, 0xEE, 0xDB])),
        new("Z", ".z", "application/x-compress", new MimeTypePattern([0x1F, 0x9D])),
        new("Z", ".z", "application/x-compress", new MimeTypePattern([0x1F, 0xA0])),

        // Source Code
        new("CPP", ".cpp", "text/x-c++src", new MimeTypePattern([35, 105, 110, 99, 108, 117, 100, 101])),
        new("CS", ".cs", "text/x-csharp", new MimeTypePattern([117, 115, 105, 110, 103])),
        new("DOCKERFILE", "Dockerfile", "text/plain", new MimeTypePattern([70, 82, 79, 77])),
        new("GO", ".go", "text/x-go", new MimeTypePattern([112, 97, 99, 107, 97, 103, 101])),
        new("H", ".h", "text/x-c++header", new MimeTypePattern([35, 112, 114, 97, 103, 109, 97])),
        new("JAVA", ".java", "text/x-java-source", new MimeTypePattern([112, 117, 98, 108, 105, 99, 32, 99, 108, 97, 115, 115])),
        new("JS", ".js", "application/javascript", new MimeTypePattern([102, 117, 110, 99, 116, 105, 111, 110])),
        new("PY", ".py", "text/x-python", new MimeTypePattern([100, 101, 102])),
        new("RUST", ".rs", "text/x-rust", new MimeTypePattern([102, 110])),
        new("TS", ".ts", "application/typescript", new MimeTypePattern([105, 109, 112, 111, 114, 116])),
        new("YAML", ".yaml", "application/x-yaml", new MimeTypePattern([105, 110, 102, 111, 58])),
        new("YML", ".yml", "application/x-yaml", new MimeTypePattern([97, 112, 105, 86, 101, 114, 115, 105, 111, 110])),
        new("PHP", ".php", "application/x-php", new MimeTypePattern([0x3C, 0x3F, 0x70, 0x68, 0x70])), // <?php
        new("Swift", ".swift", "text/x-swift", new MimeTypePattern([0x69, 0x6D, 0x70, 0x6F, 0x72, 0x74])), // import
        new("Kotlin", ".kt", "text/x-kotlin", new MimeTypePattern([0x70, 0x61, 0x63, 0x6B, 0x61, 0x67, 0x65])), // package
        new("Perl", ".pl", "text/x-perl", new MimeTypePattern("23 21 2F 75 73 72 2F 62 69 6E 2F 70 65 72 6C")), // #!/usr/bin/perl

        // Hệ thống & Thực thi
        new("APK", ".apk", "application/vnd.android.package-archive", new MimeTypePattern([0x50, 0x4B, 0x03, 0x04])),
        new("CHM", ".chm", "application/vnd.ms-htmlhelp", new MimeTypePattern([0x49, 0x49, 0x01, 0x00])),
        new("DLL", ".dll", "application/x-msdownload", new MimeTypePattern([77, 90])),
        new("EXE_DLL", ".exe", "application/x-msdownload", new MimeTypePattern([77, 90])),
        new("ISO", ".iso", "application/x-iso9660-image", new MimeTypePattern([0, 1, 0, 0])),
        new("SO", ".so", "application/x-sharedlib", new MimeTypePattern([127, 69, 76, 70])),
        new("DMG", ".dmg", "application/x-apple-diskimage", new MimeTypePattern([0x78, 0x01, 0x73, 0x0D, 0x62, 0x62, 0x60])),
        new("MSI", ".msi", "application/x-msi", new MimeTypePattern([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1])),
        new("SYS", ".sys", "application/octet-stream", new MimeTypePattern([0x4D, 0x5A])), // MZ
        new("SYS", ".sys", "application/octet-stream", new MimeTypePattern([0x55, 0x8B, 0xEC])), // Windows Driver

        // Khác

        new("TTF", ".ttf", "application/x-font-ttf", new MimeTypePattern([0, 1, 0, 0, 0])),
        new("TOML", ".toml", "application/toml", new MimeTypePattern([91, 112, 97, 99, 107, 97, 103, 101])),
        new("ICS", ".ics", "text/calendar", new MimeTypePattern("42 45 47 49 4E 3A 56 43 41 4C 45 4E 44 41 52")), // BEGIN:VCALENDAR
        new("VCF", ".vcf", "text/vcard", new MimeTypePattern("42 45 47 49 4E 3A 56 43 41 52 44")), // BEGIN:VCARD
        new("WOFF", ".woff", "font/woff", new MimeTypePattern([0x77, 0x4F, 0x46, 0x46])), // wOFF
        new("WOFF2", ".woff2", "font/woff2", new MimeTypePattern([0x77, 0x4F, 0x46, 0x32])), // wOF2
        new("PEM", ".pem", "application/x-pem-file", new MimeTypePattern("2D 2D 2D 2D 2D 42 45 47 49 4E")), // -----BEGIN
        new("STL", ".stl", "model/stl", new MimeTypePattern([0x73, 0x6F, 0x6C, 0x69, 0x64])), // "solid" (ASCII)
        new("TORRENT", ".torrent", "application/x-bittorrent", new MimeTypePattern([100, 56, 58, 97, 110, 110, 111, 117, 110, 99, 101])),

        // Cơ sở dữ liệu
        new("SQLite", ".sqlite", "application/x-sqlite3", new MimeTypePattern("53 51 4C 69 74 65 20 66 6F 72 6D 61 74 20 33 00")),
    ];

    /// <inheritdoc />
    public string DefaultMimeType => _defaultMimeType;

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedTypes
    {
        get
        {
            return _mappings
                .Select(x => x.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <inheritdoc />
    public string GetMimeType(byte[] data)
    {
        foreach (var mapping in _mappings)
        {
            if (mapping.Pattern.IsMatch(data))
                return mapping.Mime;
        }

        return DefaultMimeType;
    }

    /// <inheritdoc />
    public string GetExtension(byte[] data)
    {
        foreach (var mapping in _mappings)
        {
            if (mapping.Pattern.IsMatch(data))
                return mapping.Extension;
        }

        return string.Empty;
    }

    /// <inheritdoc />
    public bool IsSupported(string mimeType)
        => _mappings.Any(mapping => string.Equals(mapping.Mime, mimeType, StringComparison.OrdinalIgnoreCase));
}