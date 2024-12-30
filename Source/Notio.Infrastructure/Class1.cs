using Notio.Infrastructure.Identification;
using Notio.Infrastructure.Services;

UniqueId uniqueId = UniqueId.NewId(TypeId.System);
ulong idValue = UniqueId.FromHex(uniqueId.ToHex());
ParsedId _ = UniqueId.Parse(idValue);