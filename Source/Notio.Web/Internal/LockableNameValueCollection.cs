using System.Collections.Specialized;

namespace Notio.Web.Internal;

internal sealed class LockableNameValueCollection : NameValueCollection
{
    public void MakeReadOnly()
    {
        IsReadOnly = true;
    }
}