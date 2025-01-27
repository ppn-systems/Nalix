namespace NotioIO.Net.Internal
{
    internal partial class HttpConnection
    {
        private enum InputState
        {
            RequestLine,
            Headers,
        }
    }
}