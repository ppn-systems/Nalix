using Notio.FileStorage.Models;
using System.Collections.Generic;
using System.IO;

namespace Notio.FileStorage.Interfaces;

public interface IFileStorage
{
    void Upload(string fileName, byte[] data, IEnumerable<FileMeta> metaInfo, string format = "original");

    IFile Download(string fileName, string format = "original");

    string GetFileUri(string fileName, string format = "original");

    bool FileExists(string fileName, string format = "original");

    Stream GetStream(string fileName, IEnumerable<FileMeta> metaInfo, string format = "original");

    void Delete(string fileName);
}