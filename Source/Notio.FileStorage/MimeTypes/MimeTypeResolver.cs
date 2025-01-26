using System;
using System.Collections.Generic;
using System.Linq;

namespace Notio.FileStorage.MimeTypes;

public class MimeTypeResolver : IMimeTypeResolver
{
    private readonly string _defaultMimeType = "application/octet-stream";

    private readonly IList<MimeTypeMapper> _mappings =
    [
        new( "BMP", ".bmp", "image/bmp", new MimeTypePattern([66, 77]) ),
        new( "DOC", ".doc", "application/msword", new MimeTypePattern([208, 207, 17, 224, 161, 177, 26, 225] )),
        new( "EXE_DLL", ".exe", "application/x-msdownload",new MimeTypePattern( [77, 90] )),
        new( "GIF", ".gif", "image/gif", new MimeTypePattern( [71, 73, 70, 56] )),
        new( "ICO", ".ico", "image/x-icon", new MimeTypePattern( [0, 0, 1, 0] )),
        new( "JPG", ".jpg", "image/jpeg", new MimeTypePattern( [255, 216, 255] )),
        new( "MP3", ".mpga", "audio/mpeg", new MimeTypePattern([255, 251, 48] )),
        new( "OGG", ".ogv", "video/ogg", new MimeTypePattern([79, 103, 103, 83, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0] )),
        new( "PDF", ".pdf", "application/pdf", new MimeTypePattern( [37, 80, 68, 70, 45, 49, 46] )),
        new( "PNG", ".png", "image/png", new MimeTypePattern( [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82] )),
        new( "RAR", ".rar", "application/x-rar-compressed",new MimeTypePattern( [82, 97, 114, 33, 26, 7, 0] )),
        new( "SWF", ".swf", "application/x-shockwave-flash",new MimeTypePattern( [70, 87, 83] )),
        new( "TIFF", ".tiff", "image/tiff", new MimeTypePattern( [73, 73, 42, 0] )),
        new( "TORRENT", ".torrent", "application/x-bittorrent", new MimeTypePattern( [100, 56, 58, 97, 110, 110, 111, 117, 110, 99, 101])),
        new( "TTF", ".ttf", "application/x-font-ttf", new MimeTypePattern([0, 1, 0, 0, 0] )),
        new( "WAV_AVI", ".avi", "video/x-msvideo", new MimeTypePattern( [82, 73, 70, 70] )),
        new( "WMV_WMA", ".wma", "audio/x-ms-wma", new MimeTypePattern([48, 38, 178, 117, 142, 102, 207, 17, 166, 217, 0, 170, 0, 98, 206, 108] )),
        new( "ZIP_DOCX", ".zip", "application/x-zip-compressed", new MimeTypePattern([80, 75, 3, 4] )),
        new( "SEVEN_ZIP", ".7z", "application/x-7z-compressed", new MimeTypePattern( [55, 122] )),

        // http://www.garykessler.net/library/file_sigs.html
        // http://string-functions.com/hex-string.aspx
        new( "AVI", ".avi", "video/x-msvideo", new MimeTypePattern("52 49 46 46" )),
        new( "AVI", ".avi",  "video/x-msvideo", new MimeTypePattern("41 56 49 20 4C 49 53 54" )),
        new( "MP4", ".mp4", "video/mp4", new MimeTypePattern( "66 74 79 70 33 67 70 35", 4) ),
        new( "MP4", ".mp4", "video/mp4", new MimeTypePattern( "66 74 79 70 4D 53 4E 56", 4 )),
        new( "MP4", ".mp4", "video/mp4", new MimeTypePattern("00 00 00 14 66 74 79 70 69 73 6F 6D") ),
        new( "MP4", ".mp4", "video/mp4", new MimeTypePattern("00 00 00 18 66 74 79 70 33 67 70 35") ),
        new( "MP4", ".mp4", "video/mp4", new MimeTypePattern("00 00 00 1C 66 74 79 70 4D 53 4E 56 01 29 00 46 4D 53 4E 56 6D 70 34 32" )),
        new( "MP4", ".mp4", "video/mp4", new MimeTypePattern("00 00 00 1C 66 74 79 70 6D 70 34 32 00 00 00 01 6D 70 34 31 6D 70 34 32 69 73 6F 6D 00 00")),  // ftypmp?42mp41?mp42isom | https://en.wikipedia.org/wiki/ISO_base_media_file_format
        new( "MP4", ".mp4", "video/mp4", new MimeTypePattern("66 74 79 70 69 73 6F 6D", 4)),
        new( "MP4", ".mp4", "video/mp4", new MimeTypePattern("66 74 79 70 6D 70 34 32", 4)), // ftypmp42
        new( "MP4", ".mp4", "video/mp4", new MimeTypePattern("00 00 00 20 66 74 79 70 69 73 6F 36 00 00 00 01 6D 70 34 32 69 73 6F 36 61 76 63 31 69 73 6F 6D")), // this is added because of https://github.com/titansgroup/k4l-video-trimmer | ftypiso6 mp42iso6avc1isom
        new( "M4A", ".m4a", "audio/mp4", new MimeTypePattern("66 74 79 70 4D 34 41 20", 4)), // ftypM4A
        new( "MPG", ".mpeg", "video/mpeg", new MimeTypePattern("00 00 01 B3" )),
        new( "MPG", ".mpeg", "video/mpeg", new MimeTypePattern("00 00 01 BA" )),
        new( "FLV", ".flv", "video/x-flv", new MimeTypePattern("46 4C 56 01" )),
        new( "JPG", ".jpg", "image/jpeg", new MimeTypePattern("FF D8 FF E0" )),
        new( "JPG", ".jpg", "image/jpeg", new MimeTypePattern("FF D8 FF E1" )),
        new( "JPG", ".jpg", "image/jpeg", new MimeTypePattern("FF D8 FF E8" )),
        new( "JPG", ".bmp", "image/bmp", new MimeTypePattern("42 4D" )),
        new( "GIF", ".gif", "image/gif", new MimeTypePattern("47 49 46 38 37 61" )),
        new( "GIF", ".gif", "image/gif", new MimeTypePattern("47 49 46 38 39 61" )),
        new( "ASF_WMV", ".wmv", "video/x-ms-wmv", new MimeTypePattern("30 26 B2 75 8E 66 CF 11 A6 D9 00 AA 00 62 CE 6C" )),
    ];

    public string DefaultMimeType => _defaultMimeType;

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

    public string GetMimeType(byte[] data)
    {
        foreach (var mapping in _mappings)
        {
            if (mapping.Pattern.IsMatch(data))
                return mapping.Mime;
        }

        return DefaultMimeType;
    }

    public string GetExtension(byte[] data)
    {
        foreach (var mapping in _mappings)
        {
            if (mapping.Pattern.IsMatch(data))
                return mapping.Extension;
        }

        return string.Empty;
    }

    private class MimeTypeMapper(string name, string extension, string mime, MimeTypePattern pattern)
    {
        public string Name { get; private set; } = name;
        public MimeTypePattern Pattern { get; private set; } = pattern;
        public string Mime { get; private set; } = mime;
        public string Extension { get; private set; } = extension;
    }

    private class MimeTypePattern(byte[] pattern, ushort offset = 0)
    {
        public MimeTypePattern(string hexPattern, ushort offset = 0)
            : this(StringToByteArray(hexPattern), offset)
        { }

        public byte[] Bytes { get; private set; } = pattern;
        public ushort Offset { get; private set; } = offset;

        private static byte[] StringToByteArray(string hex)
        {
            hex = hex.Replace(" ", "");
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        public bool IsMatch(byte[] data)
        {
            if (data.Length >= Bytes.Length + Offset &&
                data.Skip(Offset).Take(Bytes.Length).SequenceEqual(Bytes))
                return true;

            return false;
        }
    }
}