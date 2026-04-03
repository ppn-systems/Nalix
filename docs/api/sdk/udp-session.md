# UdpSession

`UdpSession` is not present in the current `Nalix.SDK` source tree.

This page is kept only as a compatibility note for older docs and sample code that may still mention it. The current client package exposes `TransportSession` and `TcpSession` instead.

## What changed

- `TransportSession` is the shared abstract transport contract.
- `TcpSession` is the concrete client transport currently implemented in source.
- the current SDK docs should not be treated as evidence that a UDP client transport exists.

## Related APIs

- [SDK Overview](./index.md)
- [Transport Session](./transport-session.md)
- [TCP Session](./tcp-session.md)
