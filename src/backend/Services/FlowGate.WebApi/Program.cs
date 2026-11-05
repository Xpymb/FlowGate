using FlowGate.Bll.Implementations;
using FlowGate.Dal.Implementations;
using FlowGate.Shared.Bll;
using FlowGate.Shared.Dal;
using FlowGate.Shared.GitHub;
using FlowGate.Shared.Keenetic;
using FlowGate.Shared.Options;
using FlowGate.Shared.WebApi;
using FlowGate.Shared.WebApi.Errors;
using FlowGate.WebApi.Endpoints.Connections;
using FlowGate.WebApi.Endpoints.DnsRoutes;
using FlowGate.WebApi.Endpoints.DomainSources;
using FlowGate.WebApi.Endpoints.Health;
using FlowGate.WebApi.Endpoints.Jobs;
using FlowGate.WebApi.Endpoints.Keenetic;
using FlowGate.WebApi.Endpoints.Servers;
using FlowGate.WebApi.Endpoints.Subscriptions;
using FlowGate.WebApi.Endpoints.Xray;
using FlowGate.WebApi.Hosting;
using FlowGate.WebApi.Jobs;
using FlowGate.WebApi.Serialization;

var builder = WebApplication.CreateSlimBuilder(args);

var dataDirectory = builder.Configuration[$"{FlowGateOptions.SectionName}:{nameof(FlowGateOptions.DataDirectory)}"]
    ?? new FlowGateOptions().DataDirectory;
builder.Configuration
    .AddJsonFile(Path.Combine(Path.GetFullPath(dataDirectory), "appsettings.json"), optional: true, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "FLOWGATE_");

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
    options.SerializerOptions.TypeInfoResolverChain.Insert(1, ErrorJsonContext.Default);
});

builder.Services.AddOpenApi();
builder.Services
    .AddFlowGateOptions(builder.Configuration)
    .AddFlowGateSharedDal()
    .AddFlowGateSharedBll()
    .AddFlowGateDal()
    .AddFlowGateBll();
builder.Services.AddKeeneticRciClient();
builder.Services.AddGitHubReleasesClient();

builder.Services.AddHostedService<XrayLifecycleJob>();
builder.Services.AddHostedService<XrayUpdateCheckJob>();
builder.Services.AddHostedService<SubscriptionRefreshJob>();
builder.Services.AddHostedService<KeeneticReconcileJob>();
builder.Services.AddHostedService<DomainSourceRefreshJob>();

var app = builder.Build();

app.UseFlowGateExceptionHandler();
app.UseFlowGateFrontendFiles();
app.UseRouting();

app.MapOpenApi();

var api = app.MapGroup("/api");
api.MapHealthEndpoints();
api.MapKeeneticEndpoints();
api.MapXrayEndpoints();
api.MapJobEndpoints();
api.MapServerEndpoints();
api.MapSubscriptionEndpoints();
api.MapConnectionEndpoints();
api.MapDomainSourceEndpoints();
api.MapDnsRouteEndpoints();

app.MapFlowGateFrontendFallback();

app.Run();

/// <summary>
///     Точка входа FlowGate. Публичный partial-класс нужен для <c>WebApplicationFactory</c> в интеграционных тестах
/// </summary>
public partial class Program;
