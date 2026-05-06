using Nalix.Codec.DataFrames;
using Nalix.Examples.Contracts;

namespace Nalix.Examples.Dashboard.Infrastructure.Tcp;

internal static class DashboardPacketCatalogFactory
{
    public static PacketRegistry Create()
        => new PacketRegistryFactory()
            .RegisterPacket<AuthorityGrant>()
            .RegisterPacket<GenerationReport>()
            .CreateCatalog();
}
