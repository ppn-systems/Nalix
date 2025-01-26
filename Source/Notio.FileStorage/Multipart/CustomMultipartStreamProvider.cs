using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Notio.FileStorage.FileFormats;
using Notio.FileStorage.Interfaces;
using Notio.FileStorage.Models;
using Notio.Shared.Extensions;

namespace Notio.FileStorage.Multipart;

public class CustomMultipartStreamProvider : MultipartStreamProvider
{
    private readonly IFileStorageAsync repository;
    private readonly IFileGenerator generator;
    private readonly NameValueCollection formData = [];
    private readonly List<CustomMultipartFileData> fileContents = [];
    private readonly Collection<bool> isFormData = [];

    public CustomMultipartStreamProvider(IFileStorageAsync repository, IFileGenerator generator)
    {
        if (repository is null == true) throw new ArgumentNullException(nameof(repository));
        if (generator is null == true) throw new ArgumentNullException(nameof(generator));

        this.repository = repository;
        this.generator = generator;
    }

    public NameValueCollection FormData
    {
        get { return formData; }
    }

    public List<CustomMultipartFileData> FileData
    {
        get { return fileContents; }
    }

    public override Stream GetStream(HttpContent parent, HttpContentHeaders headers)
    {
        ContentDispositionHeaderValue contentDisposition = headers.ContentDisposition;
        if (contentDisposition != null)
        {
            isFormData.Add(string.IsNullOrEmpty(contentDisposition.FileName));

            return new MemoryStream();
        }
        throw new InvalidOperationException("Did not find required 'Content-Disposition' header field in MIME multipart body part.");
    }

    public override async Task ExecutePostProcessingAsync()
    {
        for (int index = 0; index < Contents.Count; index++)
        {
            if (isFormData[index])
            {
                HttpContent formContent = Contents[index];

                ContentDispositionHeaderValue contentDisposition = formContent.Headers.ContentDisposition;
                string formFieldName = UnquoteToken(contentDisposition.Name) ?? string.Empty;

                string formFieldValue = await formContent.ReadAsStringAsync();
                FormData.Add(formFieldName, formFieldValue);
            }
            else
            {
                HttpContent formContent = Contents[index];

                ContentDispositionHeaderValue contentDisposition = formContent.Headers.ContentDisposition;
                contentDisposition.FileName = Guid.NewGuid().ToString();

                var stream = formContent.ReadAsStreamAsync().Result;

                var original = stream.ToByteArray();

                await repository.UploadAsync(contentDisposition.FileName, original, [], Original.FormatName);

                foreach (var format in generator.Formats)
                {
                    if (format.Name == Original.FormatName)
                        continue;

                    var file = format.Generate(original);
                    await repository.UploadAsync(contentDisposition.FileName, file.Data, [new FileMeta("SourceName", contentDisposition.FileName)], format.Name);
                }

                fileContents.Add(new CustomMultipartFileData(formContent.Headers, contentDisposition.FileName));
            }
        }
    }

    private static string UnquoteToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        if (token.StartsWith('"') && token.EndsWith('"') && token.Length > 1)
        {
            return token[1..^1];
        }

        return token;
    }
}