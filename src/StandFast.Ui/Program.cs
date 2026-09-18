using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using MudBlazor.Services;
using Serilog;
using StandFast.Application.Abstractions;
using StandFast.Application.DependencyInjection;
using StandFast.Infrastructure.DependencyInjection;
using StandFast.Ui.Common;
using StandFast.Ui.Components;
using StandFast.Ui.Configuration;
using StandFast.Ui.Services;

Log.Logger = LoggingSetup.CreateBootstrapLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
    StandFastUiOptions uiOptions = builder.Configuration.GetSection(StandFastUiOptions.SectionName).Get<StandFastUiOptions>() ?? new StandFastUiOptions();

    // Azure Container Apps terminates TLS at its ingress and forwards plain HTTP to the container. Without honouring the forwarded headers the app
    // builds http:// OpenID Connect redirect URIs, which the identity provider rejects. The ingress is the only hop, so the proxy allow-lists are cleared.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    builder.Services.AddOptions<StandFastUiOptions>().Bind(builder.Configuration.GetSection(StandFastUiOptions.SectionName));

    builder.UseStandFastSerilog();

    builder.Services.AddStandFastInfrastructure(builder.Configuration);
    builder.Services.AddStandFastApplication();

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
    builder.Services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();

    builder.AddStandFastAuthentication();

    // Blazor Server keeps circuit state per replica and encrypts it with the Data Protection key ring. Replicas scale in and out, so the key ring
    // must outlive any single one; without shared keys a scaled-out app throws antiforgery and circuit decryption errors as requests land elsewhere.
    if (!string.IsNullOrWhiteSpace(uiOptions.DataProtectionBlobUri))
    {
        builder.Services.AddDataProtection()
            .SetApplicationName(AppInfo.Name)
            .PersistKeysToAzureBlobStorage(new Uri(uiOptions.DataProtectionBlobUri), new DefaultAzureCredential());
    }

    builder.Services.AddMudServices();
    builder.Services.AddRazorComponents().AddInteractiveServerComponents();
    builder.Services.AddHealthChecks();

    WebApplication app = builder.Build();

    app.UseForwardedHeaders();
    app.UseSerilogRequestLogging();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    // Stylesheets, scripts and the Blazor framework files are not secrets, and the fallback policy would otherwise put every one of them behind a
    // sign-in: the browser is sent to the identity provider for each asset, which at best styles the sign-in page wrongly and at worst fails outright
    // because an asset request cannot complete an interactive redirect.
    app.MapStaticAssets().AllowAnonymous();
    app.MapStandFastAuthentication();
    app.MapStandFastBackup();
    app.MapHealthChecks(UiRoutes.HealthCheck).AllowAnonymous();
    app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

    app.Run();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "StandFast terminated unexpectedly during startup.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
