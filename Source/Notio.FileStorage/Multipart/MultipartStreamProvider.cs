using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Notio.FileStorage.Multipart;

public class MultipartStreamProvider
{
    public List<HttpContent> Contents;

    public virtual Stream GetStream(HttpContent parent, HttpContentHeaders headers)
    {
        throw new NotImplementedException();
    }

    public virtual Task ExecutePostProcessingAsync()
    {
        return Task.CompletedTask;
    }
}