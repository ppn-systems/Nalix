#pragma warning disable IDE0211 // Convert to 'Program.Main' style program
using MudBlazor.Services;
using Nalix.Examples.Dashboard.Application.Abstractions;
using Nalix.Examples.Dashboard.Application.Options;
using Nalix.Examples.Dashboard.Application.Polling;
using Nalix.Examples.Dashboard.Application.State;
using Nalix.Examples.Dashboard.Infrastructure.Security;
using Nalix.Examples.Dashboard.Infrastructure.Tcp;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

_ = builder.WebHost.UseUrls("http://localhost:57207");

_ = builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
_ = builder.Services.AddMudServices();
_ = builder.Services.Configure<DashboardOptions>(builder.Configuration.GetSection("Dashboard"));
_ = builder.Services.AddSingleton<DashboardState>();
_ = builder.Services.AddSingleton<IDashboardStateReader>(sp => sp.GetRequiredService<DashboardState>());
_ = builder.Services.AddSingleton<IDashboardStateWriter>(sp => sp.GetRequiredService<DashboardState>());
_ = builder.Services.AddSingleton<IServerPublicKeyResolver, ServerPublicKeyResolver>();
_ = builder.Services.AddSingleton<IDashboardClient, DashboardTcpClient>();
_ = builder.Services.AddHostedService<DashboardPollingService>();

if (builder.Environment.IsDevelopment())
{
    _ = builder.WebHost.UseStaticWebAssets();
}

WebApplication app = builder.Build();

app.UseStaticFiles();

if (!app.Environment.IsDevelopment())
{
    _ = app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
_ = app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
_ = app.UseAntiforgery();

_ = app.MapStaticAssets();
_ = app.MapRazorComponents<Nalix.Examples.Dashboard.Components.App>()
       .AddInteractiveServerRenderMode();

app.Run();

#pragma warning restore IDE0211 // Convert to 'Program.Main' style program
