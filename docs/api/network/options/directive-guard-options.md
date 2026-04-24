# Directive Guard Options

`DirectiveGuardOptions` configures inbound directive anti-spam cooldown behavior in `Nalix.Network.Pipeline`.

## Source Mapping

- `src/Nalix.Network.Pipeline/Options/DirectiveGuardOptions.cs`

## Properties and Validation

| Property | Default | Validation | Runtime effect |
|---|---:|---|---|
| `DefaultCooldownMs` | `200` | `0..60000` | Minimum cooldown, in milliseconds, between repeated inbound directives of the same category per connection. `0` disables suppression. |

## Validation Notes

`Validate()` uses data-annotation validation and does not add cross-field rules beyond the range shown above.

## Related APIs

- [Network Options](./options.md)
- [Token Bucket Options](./token-bucket-options.md)
