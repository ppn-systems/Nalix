using System;
using System.IO;
using System.Linq;

namespace Notio.Infrastructure.Storage.Validators
{
    public class MediaTypeValidator(StorageConfig settings)
    {
        private readonly string[] _allowedExtensions = settings.AllowedFileExtensions;

        public bool IsValidFile(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return _allowedExtensions.Contains(extension);
        }
    }
}