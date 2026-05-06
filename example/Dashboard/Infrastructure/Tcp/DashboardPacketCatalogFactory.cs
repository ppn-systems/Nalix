using Nalix.Codec.DataFrames;
using Contracts;

namespace Dashboard.Infrastructure.Tcp;

internal static class DashboardPacketCatalogFactory
{
    public static PacketRegistry Create()
        => new PacketRegistryFactory()
            .RegisterPacket<AuthorityGrant>()
            .RegisterPacket<GenerationReport>()
            .CreateCatalog();
}
