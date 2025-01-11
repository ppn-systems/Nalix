using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
