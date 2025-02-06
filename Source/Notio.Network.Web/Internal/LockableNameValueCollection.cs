using System.Collections.Specialized;

namespace Notio.Network.Web.Internal;

internal sealed class LockableNameValueCollection : NameValueCollection
{
    public void MakeReadOnly()
        => IsReadOnly = true;
}