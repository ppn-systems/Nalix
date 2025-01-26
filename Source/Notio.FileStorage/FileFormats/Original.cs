using Notio.FileStorage.Generator;

namespace Notio.FileStorage.FileFormats
{
    public class Original : IFileFormat
    {
        public static string FormatName = typeof(Original).Name.ToLowerInvariant();

        public string Name
        { get { return FormatName; } }

        public FileGenerateResponse Generate(byte[] data)
        {
            return new FileGenerateResponse(true, data, string.Empty);
        }
    }
}