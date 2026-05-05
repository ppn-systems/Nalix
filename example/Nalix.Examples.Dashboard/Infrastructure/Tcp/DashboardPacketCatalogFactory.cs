using Nalix.Codec.DataFrames;
using Nalix.Examples.Contracts.Packets;

namespace Nalix.Examples.Dashboard.Infrastructure.Tcp;

internal static class DashboardPacketCatalogFactory
{
    public static PacketRegistry Create()
        => new PacketRegistryFactory()
            .RegisterPacket<AuthorityGrant>()
            .RegisterPacket<GenerationReport>()
            .CreateCatalog();
}
