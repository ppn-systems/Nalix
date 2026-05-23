using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Application.Reports;
using Nalix.Dashboard.Application.Reports.Buffers;
using Nalix.Dashboard.Application.Reports.ConnectionGuard;
using Nalix.Dashboard.Application.Reports.Connections;
using Nalix.Dashboard.Application.Reports.Dispatch;
using Nalix.Dashboard.Application.Reports.Instances;
using Nalix.Dashboard.Application.Reports.ObjectPools;
using Nalix.Dashboard.Application.Reports.Tasks;
using Nalix.Dashboard.Application.Reports.Listeners;
using Nalix.Dashboard.Application.Reports.Protocols;
using Nalix.Dashboard.Application.Reports.Sessions;
using Nalix.Dashboard.Application.Reports.ConcurrencyGate;
using Nalix.Dashboard.Application.Reports.PolicyRateLimiter;
using Nalix.Dashboard.Application.Reports.TokenBucketLimiter;
using Nalix.Dashboard.Application.Services;
using Nalix.Dashboard.Application.State;
using Nalix.Dashboard.Infrastructure.BrowserStorage;
using Nalix.Dashboard.Infrastructure.Nalix;
using Nalix.Dashboard.Infrastructure.Options;

internal class Program
{
    private static async Task Main(string[] args)
    {
        WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<Nalix.Dashboard.Components.App>("#app");
        builder.RootComponents.Add<Microsoft.AspNetCore.Components.Web.HeadOutlet>("head::after");

        _ = builder.Services.Configure<AdminClientOptions>(builder.Configuration.GetSection("AdminClient"));

        _ = builder.Services.AddSingleton<DashboardState>();
        _ = builder.Services.AddSingleton<IDashboardStateReader>(sp => sp.GetRequiredService<DashboardState>());
        _ = builder.Services.AddSingleton<IDashboardStateWriter>(sp => sp.GetRequiredService<DashboardState>());

        _ = builder.Services.AddSingleton<IAdminSettingsStore, BrowserAdminSettingsStore>();
        _ = builder.Services.AddSingleton<IAdminClient, WebSocketAdminClient>();
        _ = builder.Services.AddSingleton<IReportPollingController, ReportPollingController>();
        _ = builder.Services.AddSingleton<PingKeepAliveService>();

        _ = builder.Services.AddSingleton<IReportParser, DispatchReportParser>();
        _ = builder.Services.AddSingleton<IReportParser, TasksReportParser>();
        _ = builder.Services.AddSingleton<IReportParser, BuffersReportParser>();
        _ = builder.Services.AddSingleton<IReportParser, ConnectionsReportParser>();
        _ = builder.Services.AddSingleton<IReportParser, InstancesReportParser>();
        _ = builder.Services.AddSingleton<IReportParser, ObjectPoolsReportParser>();
        _ = builder.Services.AddSingleton<IReportParser, ConnectionGuardReportParser>();
        _ = builder.Services.AddSingleton<IReportParser, ListenersReportParser>();
        _ = builder.Services.AddSingleton<IReportParser, ProtocolsReportParser>();
        _ = builder.Services.AddSingleton<IReportParser, SessionsReportParser>();
        _ = builder.Services.AddSingleton<IReportParser, ConcurrencyGateReportParser>();
        _ = builder.Services.AddSingleton<IReportParser, PolicyRateLimiterReportParser>();
        _ = builder.Services.AddSingleton<IReportParser, TokenBucketLimiterReportParser>();
        _ = builder.Services.AddSingleton<ReportParserRegistry>();

        _ = builder.Services.AddMudServices();

        WebAssemblyHost host = builder.Build();
        _ = host.Services.GetRequiredService<PingKeepAliveService>();
        await host.RunAsync().ConfigureAwait(false);
    }
}
