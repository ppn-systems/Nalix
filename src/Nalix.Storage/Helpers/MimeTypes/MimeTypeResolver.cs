using System;
using System.Collections.Generic;
using System.Linq;

namespace Nalix.Storage.Helpers.MimeTypes;

/// <summary>
/// Provides MIME type resolution based on file extensions.
/// </summary>
public class MimeTypeResolver : IMimeTypeResolver
{
    private const string MimeTypeDefault = "application/octet-stream";

    private readonly IList<MimeTypeMapper> _mappings =
    [
        // Ảnh
        new("BMP", ".bmp", "image/bmp", new MimeTypePattern([66, 77])),
        new("JPG", ".jpg", "image/jpeg", new MimeTypePattern("FF D8 FF E0")),
        new("JPG", ".jpg", "image/jpeg", new MimeTypePattern("FF D8 FF E1")),
        new("JPG", ".jpg", "image/jpeg", new MimeTypePattern("FF D8 FF E8")),
        new("ICO", ".ico", "image/x-icon", new MimeTypePattern([0, 0, 1, 0])),
        new("GIF", ".gif", "image/gif", new MimeTypePattern([71, 73, 70, 56])),
        new("JPG", ".jpg", "image/jpeg", new MimeTypePattern([255, 216, 255])),
        new("SVG", ".svg", "image/svg+xml", new MimeTypePattern("3C 73 76 67")),
        new("TIFF", ".tiff", "image/tiff", new MimeTypePattern([73, 73, 42, 0])),
        new("GIF", ".gif", "image/gif", new MimeTypePattern("47 49 46 38 37 61")),
        new("GIF", ".gif", "image/gif", new MimeTypePattern("47 49 46 38 39 61")),
        new("SVG", ".svg", "image/svg+xml", new MimeTypePattern("3C 3F 78 6D 6C")),
        new("XCF", ".xcf", "image/x-xcf", new MimeTypePattern("67 69 6D 70 20 78 63 66")), // "gimp xcf"
        new("HEIC", ".heic", "image/heif", new MimeTypePattern("00 00 00 20 66 74 79 70 68 65 69 63")),
        new("PSD", ".psd", "image/vnd.adobe.photoshop", new MimeTypePattern([0x38, 0x42, 0x50, 0x53])),
        new("JP2", ".jp2", "image/jp2", new MimeTypePattern([0x00, 0x00, 0x00, 0x0C, 0x6A, 0x50, 0x20, 0x20])),
        new("CR2", ".cr2", "image/x-canon-cr2", new MimeTypePattern([0x49, 0x49, 0x2A, 0x00, 0x10, 0xFB, 0x86, 0x01])),
        new("PNG", ".png", "image/png", new MimeTypePattern([137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82])),
        new("DNG", ".dng", "image/x-adobe-dng", new MimeTypePattern([0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, 0x43, 0x52])),
        new("WebP", ".webp", "image/webp", new MimeTypePattern([0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50])),

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
        new("M2TS", ".m2ts", "video/mp2t", new MimeTypePattern([0x47, 0x40, 0x00, 0x10])),
        new("DIVX", ".divx", "video/x-divx", new MimeTypePattern("44 49 56 58")), // DIVX
        new("RM", ".rm", "application/vnd.rn-realmedia", new MimeTypePattern([0x2E, 0x52, 0x4D, 0x46])),
        new("MOV", ".mov", "video/quicktime", new MimeTypePattern([0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70, 0x70, 0x72, 0x6F, 0x72])),

        // Âm thanh
        new("AC3", ".ac3", "audio/ac3", new MimeTypePattern([0x0B, 0x77])),
        new("AAC", ".aac", "audio/aac", new MimeTypePattern([0xFF, 0xF1])),
        new("AAC", ".aac", "audio/aac", new MimeTypePattern([0xFF, 0xF9])),
        new("MP3", ".mpga", "audio/mpeg", new MimeTypePattern([255, 251, 48])),
        new("WAV", ".wav", "audio/wav", new MimeTypePattern([82, 73, 70, 70])),
        new("AMR", ".amr", "audio/amr", new MimeTypePattern("23 21 41 4D 52")), // #!AMR
        new("FLAC", ".flac", "audio/flac", new MimeTypePattern([102, 76, 97, 67])),
        new("OGG_AUDIO", ".ogg", "audio/ogg", new MimeTypePattern([79, 103, 103, 83])),
        new("AU", ".au", "audio/basic", new MimeTypePattern([0x2E, 0x73, 0x6E, 0x64])),
        new("MIDI", ".mid", "audio/midi", new MimeTypePattern([0x4D, 0x54, 0x68, 0x64])), // MThd
        new("M4A", ".m4a", "audio/mp4", new MimeTypePattern("66 74 79 70 4D 34 41 20", 4)),
        new("AIFF", ".aiff", "audio/aiff", new MimeTypePattern([0x46, 0x4F, 0x52, 0x4D, 0x00])), // FORM
        new("WMA", ".wma", "audio/x-ms-wma", new MimeTypePattern([48, 38, 178, 117, 142, 102])),
        new("Opus", ".opus", "audio/opus", new MimeTypePattern([0x4F, 0x70, 0x75, 0x73, 0x48, 0x65, 0x61, 0x64])), // OpusHead

        // Tài liệu
        new("MD", ".md", "text/markdown", new MimeTypePattern([35, 32])),
        new("CSV", ".csv", "text/csv", new MimeTypePattern([34, 44, 13, 10])),
        new("TXT", ".txt", "text/plain", new MimeTypePattern([74, 79, 72, 78, 88, 67])),
        new("HTML", ".html", "text/html", new MimeTypePattern([60, 33, 44, 45, 47, 38])),
        new("XML", ".xml", "application/xml", new MimeTypePattern([60, 63, 120, 109, 108])),
        new("JSON", ".json", "application/json", new MimeTypePattern([123, 34, 99, 111, 110])),
        new("PDF", ".pdf", "application/pdf", new MimeTypePattern([37, 80, 68, 70, 45, 49, 46])),
        new("EPUB", ".epub", "application/epub+zip", new MimeTypePattern([0x50, 0x4B, 0x03, 0x04])), // ZIP + require mimetype check
        new("RTF", ".rtf", "application/rtf", new MimeTypePattern([0x7B, 0x5C, 0x72, 0x74, 0x66, 0x31])), // {\rtf1
        new("XPS", ".xps", "application/vnd.ms-xpsdocument", new MimeTypePattern([0x50, 0x4B, 0x03, 0x04])),
        new("PAGES", ".pages", "application/vnd.apple.pages", new MimeTypePattern([0x50, 0x4B, 0x03, 0x04])),
        new("DOC", ".doc", "application/msword", new MimeTypePattern([208, 207, 17, 224, 161, 177, 26, 225])),
        new("FB2", ".fb2", "application/x-fictionbook", new MimeTypePattern("3C 46 69 63 74 69 6F 6E 42 6F 6F 6B")), // <FictionBook
        new("ODT", ".odt", "application/vnd.oasis.opendocument.text", new MimeTypePattern([0x50, 0x4B, 0x03, 0x04])),
        new("ODS", ".ods", "application/vnd.oasis.opendocument.spreadsheet", new MimeTypePattern([0x50, 0x4B, 0x03, 0x04])),
        new("ODP", ".odp", "application/vnd.oasis.opendocument.presentation", new MimeTypePattern([0x50, 0x4B, 0x03, 0x04])),
        new("XLSX", ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", new MimeTypePattern([80, 75, 3, 4])),
        new("DOCX", ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", new MimeTypePattern([80, 75, 3, 4])),
        new("PPTX", ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation", new MimeTypePattern([80, 75, 3, 4])),

        // Nén & Archive
        new("GZ", ".gz", "application/gzip", new MimeTypePattern([31, 139, 8])),
        new("ARJ", ".arj", "application/x-arj", new MimeTypePattern([0x60, 0xEA])),
        new("ZIP", ".zip", "application/zip", new MimeTypePattern([80, 75, 3, 4])),
        new("Z", ".z", "application/x-compress", new MimeTypePattern([0x1F, 0x9D])),
        new("BZ2", ".bz2", "application/x-bzip2", new MimeTypePattern([66, 90, 104])),
        new("TAR", ".tar", "application/x-tar", new MimeTypePattern([75, 10, 30, 12])),
        new("7Z", ".7z", "application/x-7z-compressed", new MimeTypePattern([55, 122])),
        new("RPM", ".rpm", "application/x-rpm", new MimeTypePattern([0xED, 0xAB, 0xEE, 0xDB])),
        new("LZH", ".lzh", "application/x-lzh-compressed", new MimeTypePattern([0x2D, 0x6C, 0x68])),
        new("ZIP_DOCX", ".zip", "application/x-zip-compressed", new MimeTypePattern([80, 75, 3, 4])),
        new("XZ", ".xz", "application/x-xz", new MimeTypePattern([0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00])),
        new("RAR", ".rar", "application/x-rar-compressed", new MimeTypePattern([82, 97, 114, 33, 26, 7, 0])),
        new("CAB", ".cab", "application/vnd.ms-cab-compressed", new MimeTypePattern([0x4D, 0x53, 0x43, 0x46])), // MSCF
        new("DEB", ".deb", "application/x-debian-package", new MimeTypePattern([0x21, 0x3C, 0x61, 0x72, 0x63, 0x68, 0x3E])), // !<arch>

        // Source Code
        new("RUST", ".rs", "text/x-rust", new MimeTypePattern([102, 110])),
        new("PY", ".py", "text/x-python", new MimeTypePattern([100, 101, 102])),
        new("CS", ".cs", "text/x-csharp", new MimeTypePattern([117, 115, 105, 110, 103])),
        new("DOCKERFILE", "Dockerfile", "text/plain", new MimeTypePattern([70, 82, 79, 77])),
        new("GO", ".go", "text/x-go", new MimeTypePattern([112, 97, 99, 107, 97, 103, 101])),
        new("H", ".h", "text/x-c++header", new MimeTypePattern([35, 112, 114, 97, 103, 109, 97])),
        new("YAML", ".yaml", "application/x-yaml", new MimeTypePattern([105, 110, 102, 111, 58])),
        new("PHP", ".php", "application/x-php", new MimeTypePattern([0x3C, 0x3F, 0x70, 0x68, 0x70])), // <?php
        new("TS", ".ts", "application/typescript", new MimeTypePattern([105, 109, 112, 111, 114, 116])),
        new("CPP", ".cpp", "text/x-c++src", new MimeTypePattern([35, 105, 110, 99, 108, 117, 100, 101])),
        new("Swift", ".swift", "text/x-swift", new MimeTypePattern([0x69, 0x6D, 0x70, 0x6F, 0x72, 0x74])), // import
        new("DART", ".dart", "application/dart", new MimeTypePattern([0x69, 0x6D, 0x70, 0x6F, 0x72, 0x74])), // import
        new("Perl", ".pl", "text/x-perl", new MimeTypePattern("23 21 2F 75 73 72 2F 62 69 6E 2F 70 65 72 6C")), // #!/usr/bin/perl
        new("Kotlin", ".kt", "text/x-kotlin", new MimeTypePattern([0x70, 0x61, 0x63, 0x6B, 0x61, 0x67, 0x65])), // package
        new("LUA", ".lua", "text/x-lua", new MimeTypePattern([0x66, 0x75, 0x6E, 0x63, 0x74, 0x69, 0x6F, 0x6E])), // "function"
        new("JS", ".js", "application/javascript", new MimeTypePattern([102, 117, 110, 99, 116, 105, 111, 110])),
        new("YML", ".yml", "application/x-yaml", new MimeTypePattern([97, 112, 105, 86, 101, 114, 115, 105, 111, 110])),
        new("JAVA", ".java", "text/x-java-source", new MimeTypePattern([112, 117, 98, 108, 105, 99, 32, 99, 108, 97, 115, 115])),
        new("PS1", ".ps1", "application/x-powershell", new MimeTypePattern([0x23, 0x52, 0x65, 0x71, 0x75, 0x69, 0x72, 0x65, 0x73])), // #Requires
        new("RB", ".rb", "text/x-ruby", new MimeTypePattern([0x23, 0x21, 0x2F, 0x75, 0x73, 0x72, 0x2F, 0x62, 0x69, 0x6E, 0x2F, 0x72, 0x75, 0x62, 0x79])), // #!/usr/bin/ruby

        // Hệ thống & Thực thi
        new("DLL", ".dll", "application/x-msdownload", new MimeTypePattern([77, 90])),
        new("SYS", ".sys", "application/octet-stream", new MimeTypePattern([0x4D, 0x5A])), // MZ
        new("EXE_DLL", ".exe", "application/x-msdownload", new MimeTypePattern([77, 90])),
        new("SO", ".so", "application/x-sharedlib", new MimeTypePattern([127, 69, 76, 70])),
        new("VXD", ".vxd", "application/x-msdos-program", new MimeTypePattern([0x4D, 0x5A])),
        new("ISO", ".iso", "application/x-iso9660-image", new MimeTypePattern([0, 1, 0, 0])),
        new("SYS", ".sys", "application/octet-stream", new MimeTypePattern([0x55, 0x8B, 0xEC])), // Windows Driver
        new("ELF", ".elf", "application/x-executable", new MimeTypePattern([0x7F, 0x45, 0x4C, 0x46])), // ELF
        new("CHM", ".chm", "application/vnd.ms-htmlhelp", new MimeTypePattern([0x49, 0x49, 0x01, 0x00])),
        new("MACHO", ".macho", "application/x-mach-binary", new MimeTypePattern([0xFE, 0xED, 0xFA, 0xCE])), // Mach-O
        new("APK", ".apk", "application/vnd.android.package-archive", new MimeTypePattern([0x50, 0x4B, 0x03, 0x04])),
        new("MSI", ".msi", "application/x-msi", new MimeTypePattern([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1])),
        new("DMG", ".dmg", "application/x-apple-diskimage", new MimeTypePattern([0x78, 0x01, 0x73, 0x0D, 0x62, 0x62, 0x60])),

        // Cơ sở dữ liệu
        new("SQLite", ".sqlite", "application/x-sqlite3", new MimeTypePattern("53 51 4C 69 74 65 20 66 6F 72 6D 61 74 20 33 00")),
        new("MDB", ".mdb", "application/x-msaccess", new MimeTypePattern([0x00, 0x01, 0x00, 0x00, 0x53, 0x74, 0x61, 0x6E, 0x64, 0x61, 0x72, 0x64, 0x20, 0x4A, 0x65, 0x74])),
        new("SQL", ".sql", "application/sql", new MimeTypePattern([0x2D, 0x2D, 0x20, 0x53, 0x51, 0x4C])), // -- SQL

        // Font chữ
        new("OTF", ".otf", "font/otf", new MimeTypePattern([0x4F, 0x54, 0x54, 0x4F])), // OTTO
         new("WOFF", ".woff", "font/woff", new MimeTypePattern([0x77, 0x4F, 0x46, 0x46])), // wOFF
        new("TTF", ".ttf", "application/x-font-ttf", new MimeTypePattern([0, 1, 0, 0, 0])),
        new("WOFF2", ".woff2", "font/woff2", new MimeTypePattern([0x77, 0x4F, 0x46, 0x32])), // wOF2
        new("EOT", ".eot", "application/vnd.ms-fontobject", new MimeTypePattern([0x4C, 0x50, 0x00])),

        // ĐỊNH DẠNG KHÁC
        new("STL", ".stl", "model/stl", new MimeTypePattern([0x73, 0x6F, 0x6C, 0x69, 0x64])), // "solid" (ASCII)
        new("VHD", ".vhd", "application/x-vhd", new MimeTypePattern("63 6F 6E 65 63 74 69 78")), // conectix
        new("VCF", ".vcf", "text/vcard", new MimeTypePattern("42 45 47 49 4E 3A 56 43 41 52 44")), // BEGIN:VCARD
        new("PEM", ".pem", "application/x-pem-file", new MimeTypePattern("2D 2D 2D 2D 2D 42 45 47 49 4E")), // -----BEGIN
        new("TOML", ".toml", "application/toml", new MimeTypePattern([91, 112, 97, 99, 107, 97, 103, 101])),
        new("INI", ".ini", "text/plain", new MimeTypePattern([0x5B, 0x53, 0x65, 0x63, 0x74, 0x69, 0x6F, 0x6E])), // [Section
        new("ICS", ".ics", "text/calendar", new MimeTypePattern("42 45 47 49 4E 3A 56 43 41 4C 45 4E 44 41 52")), // BEGIN:VCALENDAR
        new("MSG", ".msg", "application/vnd.ms-outlook", new MimeTypePattern([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1])),
        new("TORRENT", ".torrent", "application/x-bittorrent", new MimeTypePattern([100, 56, 58, 97, 110, 110, 111, 117, 110, 99, 101])),
        new("FBX", ".fbx", "application/octet-stream", new MimeTypePattern([0x4B, 0x61, 0x79, 0x64, 0x61, 0x72, 0x61, 0x20, 0x46, 0x42, 0x58])), // Kaydara FBX
    ];

    /// <inheritdoc />
    public string DefaultMimeType => MimeTypeDefault;

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
        var matches = _mappings
            .Select(m => new { m.Mime, Score = m.Pattern.MatchScore(data), PatternLength = m.Pattern.Bytes.Length })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.PatternLength)
            .ThenByDescending(x => x.Score)
            .ThenBy(x => x.Mime);

        return matches.FirstOrDefault()?.Mime ?? DefaultMimeType;
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
