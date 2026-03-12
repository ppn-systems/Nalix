# EnvelopeEncryptor — Automatic Object Member Encryption/Decryption

**EnvelopeEncryptor** enables attribute-driven, automatic encryption and decryption of sensitive fields and properties within business objects, protocol packets, records, and data transfer objects in .NET.  
Combined with `[SensitiveData]` attributes and flexible sensitivity levels, it helps enforce defense-in-depth for privacy and compliance (GDPR, HIPAA, PCI DSS, etc.).

- **Namespace:** `Nalix.Shared.Security`
- **Class:** `EnvelopeEncryptor` (static)
- **Core Attribute:** `SensitiveDataAttribute` (level-based tagging)
- **Supported ciphers:** See `CipherSuiteType` (`CHACHA20_POLY1305`, `SALSA20_POLY1305`, ...)

---

## Features

- **Attribute-driven protection** — only members tagged `[SensitiveData]` (and `High`/`Critical`/`Confidential`) are encrypted
- **Recursively applies** — works on nested objects, arrays, and lists, including graphs with arbitrary depth/types
- **Per-level policy** — level-based: `Public/Internal` are *never* encrypted; levels `Confidential` and above enforced
- **Transparent serialization** — compatible with POCOs, packets, protocol messages
- **O(1) performance after first access** — all member scans/caching are done once (reflection-free at runtime)
- **Thread-safe** & stateless for concurrent use (object storage is WeakTable-based)

---

## Quick Usage

### Tagging Data for Security

```csharp
using Nalix.Common.Security.Attributes;

public class User
{
    public string UserName { get; set; }

    [SensitiveData(DataSensitivityLevel.High)] // Ensures field is encrypted in memory/persist/transport
    public string Password { get; set; }

    [SensitiveData(DataSensitivityLevel.Confidential)]
    public string Email { get; set; }
}
```

---

### Encrypting an Object

```csharp
using Nalix.Shared.Security;

User user = ...;
byte[] key = ...; // 32 bytes, CSPRNG
CipherSuiteType suite = CipherSuiteType.CHACHA20_POLY1305;

EnvelopeEncryptor.Encrypt(user, key, suite);
// All [SensitiveData] members are now strongly encrypted at rest and in transit
```

### Decrypting an Object

```csharp
// Same key and suite as above
EnvelopeEncryptor.Decrypt(user, key);
```

> **Note:** For AEAD ciphers, AAD can be supplied for authenticated context.

---

## API Overview

| Method                                         | Description                                                     |
|------------------------------------------------|-----------------------------------------------------------------|
| `Encrypt<T>(obj, key, cipher, aad)`            | Encrypts all marked & eligible members                          |
| `Decrypt<T>(obj, key, aad)`                    | Decrypts all encrypted members                                  |
| `HasSensitiveData<T>()`                        | Checks if T has any `[SensitiveData]` members                   |
| `GetSensitiveDataMembers<T>()`                 | Lists all tagged members with their protection levels           |

---

## Attribute Reference

| Attribute                  | Description                                                       |
|----------------------------|-------------------------------------------------------------------|
| `[SensitiveData]`          | Marks field/property as sensitive (default: High)                 |
| `[SensitiveData(Level)]`   | Sets custom level: Public, Internal, Confidential, High, Critical |
| `Level: Confidential`      | Recommend encrypt, not strictly enforced                          |
| `Level: High/Critical`     | Mandate encryption, tracked for auditing                          |
| `Level: Public/Internal`   | Skipped by encryption process                                     |

---

## Design & Security Notes

- **No tag = no automatic protection:** Only annotate what needs defense-in-depth.
- **Nested object/array/list support**: Encrypts/decrypts recursively.
- **Auto-caching:** All reflection (getter/setter/level) happens once per type and is then cached.
- **Value-types:** Field-level encryption uses base64 encoding under the hood; boxed default values represent encrypted storage while encrypted.
- **Changed object after decryption:** If auth fails for any member, a SecurityException is thrown. The object may be *partially* decrypted; defensive cloning is recommended if atomicity is critical.

---

## Error Handling

- Throws ArgumentNullException if object or key is missing.
- Throws ArgumentException if key/cipher is wrong or not supported.
- Throws SecurityException if decryption fails (per-member).
- Handles null/empty fields safely.

---

## Example: Secure Packet/DTO

```csharp
public class PaymentRequest
{
    [SensitiveData(DataSensitivityLevel.Critical)]
    public string CardNumber { get; set; }
    [SensitiveData]
    public string CVV { get; set; } // Default is "High"
    public string MerchantId { get; set; }
}

// ...

PaymentRequest req = ...;
// Encrypt
EnvelopeEncryptor.Encrypt(req, key, CipherSuiteType.SALSA20_POLY1305);
// Decrypt
EnvelopeEncryptor.Decrypt(req, key);
```

---

## Reference

- [Data sensitivity best practices — Microsoft Security Docs](https://learn.microsoft.com/en-us/azure/security/fundamentals/protect-data-at-rest)
- [GDPR Article 32 — Security of processing](https://gdpr-info.eu/art-32-gdpr/)
- [RFC 7523 — Supplied credentials in object models](https://datatracker.ietf.org/doc/html/rfc7523)

---

## License

Licensed under the Apache License, Version 2.0.
