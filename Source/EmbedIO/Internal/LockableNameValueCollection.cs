using System.Collections.Specialized;

namespace NotioIO.Internal
{
    internal sealed class LockableNameValueCollection : NameValueCollection
    {
        public void MakeReadOnly() => IsReadOnly = true;
    }
}