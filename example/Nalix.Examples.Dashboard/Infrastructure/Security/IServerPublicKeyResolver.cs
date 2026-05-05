using Nalix.Examples.Dashboard.Application.Options;

namespace Nalix.Examples.Dashboard.Infrastructure.Security;

internal interface IServerPublicKeyResolver
{
    string Resolve(DashboardOptions options);
}
