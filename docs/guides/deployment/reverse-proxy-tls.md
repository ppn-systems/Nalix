# Reverse Proxy TLS (nginx / Caddy)

Nalix does not terminate TLS itself. The WebSocket listener binds an
`http://` prefix via `HttpListener` — there is no `wss://` support in
process, on any OS. Application data is already protected in transit by the
built-in X25519 handshake + ChaCha20-Poly1305 session encryption (see
[Handshake](../../api/security/handshake.md)), but that does not give you
`wss://`, valid browser certificates, or SNI/cert rotation. For that, put a
reverse proxy in front and let it own TLS.

This guide covers the two proxies most commonly paired with Nalix:
nginx (manual certs via certbot) and Caddy (automatic certs, zero config
for renewal).

## What the proxy must do

1. Terminate TLS on `:443` and speak plain WebSocket to Nalix on the
   configured `NetworkWebSocketOptions.Port` (default `57207`).
2. Preserve the `Upgrade` / `Connection` headers for the WebSocket handshake.
3. Forward the real client IP so `ConnectionGuard` and rate limiting see
   actual clients, not the proxy's IP.
4. Send an `Origin` header that matches `NetworkWebSocketOptions.AllowedOrigins`
   if you use CSWSH origin checking (see [WebSocket Options](../../api/options/network/websocket-options.md)).

## nginx

```nginx
server {
    listen 443 ssl http2;
    server_name example.com;

    ssl_certificate     /etc/letsencrypt/live/example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/example.com/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;

    location /ws/ {
        proxy_pass http://127.0.0.1:57207;
        proxy_http_version 1.1;

        # WebSocket upgrade
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";

        # Real client IP + origin
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Host $host;

        # WebSocket connections are long-lived; don't let nginx kill them early
        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;
    }
}

server {
    listen 80;
    server_name example.com;
    return 301 https://$host$request_uri;
}
```

Cert renewal (certbot, cron/systemd timer already installed by the package):

```bash
sudo certbot --nginx -d example.com
sudo systemctl status certbot.timer   # auto-renewal, runs twice daily
```

## Caddy

Caddy gets you `wss://` with automatic Let's Encrypt certs and renewal,
no certbot, no cron job:

```caddyfile
example.com {
    reverse_proxy /ws/* 127.0.0.1:57207 {
        header_up X-Real-IP {remote_host}
        header_up X-Forwarded-For {remote_host}
    }
}
```

That's the whole config — Caddy issues, installs, and renews the
certificate automatically on first request.

## Nalix-side configuration

Match the proxy setup on the Nalix side:

```ini
[NetworkWebSocket]
Host = 127.0.0.1        ; only listen on loopback, proxy is the public edge
Port = 57207
AllowedOrigins = https://example.com
AllowMissingOrigin = false

[ForwardedHeaders]
Enabled = true           ; trust X-Forwarded-For / X-Real-IP / CF-Connecting-IP
RequireTrustedProxy = true
```

`RequireTrustedProxy = true` means forwarded-IP headers are only honored
from IPs listed in `trusted_proxies.txt` (`TrustedProxyOptions.StoreFileName`,
loaded from the data directory) — add your nginx/Caddy box's IP (or
`127.0.0.1` if it's local) to that file. Without this, any client could
spoof `X-Forwarded-For` and bypass IP-based rate limiting.

See [Trusted Proxy Options](../../api/options/network/trusted-proxy-options.md)
for the full option reference. `ForwardedHeadersOptions` itself
(`src/Nalix.Network/Options/ForwardedHeadersOptions.cs`) has just the two
fields shown above: `Enabled` and `RequireTrustedProxy`.

## Checklist

- [ ] Nalix `Host` bound to `127.0.0.1` or an internal interface, not `*`, once a proxy is in front.
- [ ] `ForwardedHeaders.Enabled = true`, `RequireTrustedProxy = true`.
- [ ] Proxy IP added to `trusted_proxies.txt`.
- [ ] `AllowedOrigins` set to your real origin(s); `AllowMissingOrigin = false` for browser-only deployments.
- [ ] Proxy `proxy_read_timeout` / equivalent is longer than your idle timeout expectations for long-lived WebSocket connections.
- [ ] Client connects to `wss://example.com/ws/`, not the raw Nalix port.

## Recommended Next Pages

- [Production Checklist](./production-checklist.md)
- [WebSocket Options](../../api/options/network/websocket-options.md)
- [Trusted Proxy Options](../../api/options/network/trusted-proxy-options.md)
- [Handshake](../../api/security/handshake.md)
