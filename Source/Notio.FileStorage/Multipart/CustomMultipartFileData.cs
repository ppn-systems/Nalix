using System.Net.Http.Headers;

namespace Notio.FileStorage.Multipart
{
    public class CustomMultipartFileData(HttpContentHeaders headers, string fileName)
    {
        public HttpContentHeaders Headers { get; private set; } = headers;

        public string FileName { get; private set; } = fileName;
    }
}