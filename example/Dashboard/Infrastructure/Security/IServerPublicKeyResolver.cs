using Dashboard.Application.Options;

namespace Dashboard.Infrastructure.Security;

internal interface IServerPublicKeyResolver
{
    string Resolve(DashboardOptions options);
}

