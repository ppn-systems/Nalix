namespace NotioIO.Net.Internal
{
    internal partial class HttpConnection
    {
        private enum LineState
        {
            None,
            Cr,
            Lf,
        }
    }
}