using System.Collections.Specialized;

namespace Nalix.Network.Web.Internal;

internal sealed class LockableNameValueCollection : NameValueCollection
{
    public void MakeReadOnly()
        => IsReadOnly = true;
}