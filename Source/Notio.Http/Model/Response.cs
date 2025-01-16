using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Notio.Http.Configuration;
using Notio.Http.Cookie;
using Notio.Http.Exceptions;
using Notio.Http.Interfaces;
using Notio.Http.Utils;

namespace Notio.Http.Model;

/// <inheritdoc />
public class Response : IResponse
{
    private readonly NotioCall _call;
    private readonly Lazy<IReadOnlyNameValueList<string>> _headers;
    private readonly Lazy<IReadOnlyList<NotioCookie>> _cookies;
    private object _capturedBody = null;
    private bool _streamRead = false;
    private ISerializer _serializer = null;

    /// <inheritdoc />
    public IReadOnlyNameValueList<string> Headers => _headers.Value;

    /// <inheritdoc />
    public IReadOnlyList<NotioCookie> Cookies => _cookies.Value;

    /// <inheritdoc />
    public HttpResponseMessage ResponseMessage => _call.HttpResponseMessage;

    /// <inheritdoc />
    public int StatusCode => (int)ResponseMessage.StatusCode;

    /// <summary>
    /// Creates a new Response that wraps the give HttpResponseMessage.
    /// </summary>
    public Response(NotioCall call, CookieJar cookieJar = null)
    {
        _call = call;
        _headers = new Lazy<IReadOnlyNameValueList<string>>(LoadHeaders);
        _cookies = new Lazy<IReadOnlyList<NotioCookie>>(LoadCookies);
        LoadCookieJar(cookieJar);
    }

    private IReadOnlyNameValueList<string> LoadHeaders()
    {
        var result = new NameValueList<string>(false);

        foreach (var h in ResponseMessage.Headers)
            foreach (var v in h.Value)
                result.Add(h.Key, v);

        if (ResponseMessage.Content?.Headers == null)
            return result;

        foreach (var h in ResponseMessage.Content.Headers)
            foreach (var v in h.Value)
                result.Add(h.Key, v);

        return result;
    }

    private IReadOnlyList<NotioCookie> LoadCookies()
    {
        var url = ResponseMessage.RequestMessage.RequestUri.AbsoluteUri;
        return ResponseMessage.Headers.TryGetValues("Set-Cookie", out var headerValues) ?
            headerValues.Select(hv => CookieCutter.ParseResponseHeader(hv, url)).ToList() :
            new List<NotioCookie>();
    }

    private void LoadCookieJar(CookieJar jar)
    {
        if (jar == null) return;
        foreach (var cookie in Cookies)
            jar.TryAddOrReplace(cookie, out _); // not added if cookie fails validation
    }

    /// <inheritdoc />
    public async Task<T> GetJsonAsync<T>()
    {
        if (_streamRead)
        {
            if (_capturedBody == null) return default;
            if (_capturedBody is T body) return body;
        }

        _serializer ??= _call.Request.Settings.JsonSerializer;

        try
        {
            if (_streamRead)
            {
                // Stream was read but captured as a different type than T. If it was captured as a string,
                // we should be in good shape. If it was deserialized to a different type, the best we can
                // do is serialize it and then deserialize to T, and we could lose data. But that's a very
                // uncommon scenario, hopefully. https://github.com/tmenier/Flurl/issues/571#issuecomment-881712479
                var s = _capturedBody as string ?? _serializer.Serialize(_capturedBody);
                _capturedBody = _serializer.Deserialize<T>(s);
            }
            else
            {
                using var stream = await ResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false);
                _capturedBody = _serializer.Deserialize<T>(stream);
            }
            return (T)_capturedBody;
        }
        catch (Exception ex)
        {
            _serializer = null;
            _capturedBody = await ResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
            _streamRead = true;
            await NotioClient.HandleExceptionAsync(_call, new ParsingException(_call, "JSON", ex), CancellationToken.None).ConfigureAwait(false);
            return default;
        }
        finally
        {
            _streamRead = true;
        }
    }

    /// <inheritdoc />
    public async Task<string> GetStringAsync()
    {
        if (_streamRead)
        {
            return
                _capturedBody == null ? null :
                // if GetJsonAsync<T> was called, we streamed the response directly to a T (for memory efficiency)
                // without first capturing a string. it's too late to get it, so the best we can do is serialize the T
                _serializer != null ? _serializer.Serialize(_capturedBody) :
                _capturedBody?.ToString();
        }

        // fixes #606. also verified that HttpClient.GetStringAsync returns empty string when Content is null.
        if (ResponseMessage.Content == null)
            return "";

#if NETSTANDARD2_0
			// https://stackoverflow.com/questions/46119872/encoding-issues-with-net-core-2 (#86)
			System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
#endif
        // strip quotes from charset so .NET doesn't choke on them
        // https://github.com/dotnet/corefx/issues/5014
        // https://github.com/tmenier/Flurl/pull/76
        var ct = ResponseMessage.Content.Headers?.ContentType;
        if (ct?.CharSet != null)
            ct.CharSet = ct.CharSet.StripQuotes();

        _capturedBody = await ResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
        _streamRead = true;
        return (string)_capturedBody;
    }

    /// <inheritdoc />
    public Task<Stream> GetStreamAsync()
    {
        _streamRead = true;
        return ResponseMessage.Content.ReadAsStreamAsync();
    }

    /// <inheritdoc />
    public async Task<byte[]> GetBytesAsync()
    {
        if (_streamRead)
            return _capturedBody as byte[];

        _capturedBody = await ResponseMessage.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        _streamRead = true;
        return (byte[])_capturedBody;
    }

    /// <summary>
    /// Disposes the underlying HttpResponseMessage.
    /// </summary>
    public void Dispose() => ResponseMessage.Dispose();
}
