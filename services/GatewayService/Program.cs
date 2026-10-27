using Yarp.ReverseProxy.Configuration;
using System.Text.Json.Nodes;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();

var services = new[]
{
    new GatewayService("auth", "auth-service", "Autenticação", "Crie usuários e emita tokens JWT para consumir as APIs protegidas."),
    new GatewayService("pedidos", "pedidos-service", "Pedidos", "Crie e consulte pedidos; a reserva de estoque e o evento são integrados."),
    new GatewayService("estoque", "estoque-service", "Estoque", "Consulte produtos e reserve unidades para um pedido."),
    new GatewayService("mensageria", "mensageria-service", "Mensageria", "Publique eventos de pedido no RabbitMQ.")
};

static Dictionary<string, string>[] PrefixTransform(string prefix) =>
    new[] { new Dictionary<string, string> { ["PathRemovePrefix"] = prefix }, new Dictionary<string, string> { ["RequestHeader"] = "X-Forwarded-Prefix", ["Set"] = prefix } };

builder.Services.AddReverseProxy().LoadFromMemory(
    services.Select(service => new RouteConfig
    {
        RouteId = service.Prefix,
        ClusterId = service.ClusterId,
        Match = new RouteMatch { Path = $"/{service.Prefix}/{{**catch-all}}" },
        Transforms = PrefixTransform($"/{service.Prefix}")
    }).ToArray(),
    services.Select(service => new ClusterConfig
    {
        ClusterId = service.ClusterId,
        Destinations = new Dictionary<string, DestinationConfig>
        {
            [service.Prefix] = new() { Address = service.InternalAddress }
        }
    }).ToArray());
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

foreach (var service in services)
{
    app.MapGet($"/{service.Prefix}/openapi/v1.json", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
    {
        using var response = await httpClientFactory.CreateClient().GetAsync(service.OpenApiAddress, cancellationToken);
        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        if (!response.IsSuccessStatusCode || document is null)
        {
            return Results.Problem($"Não foi possível carregar o OpenAPI de {service.Prefix}.", statusCode: (int)response.StatusCode);
        }

        var forwardedProto = httpContext.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? httpContext.Request.Scheme;
        var forwardedHost = httpContext.Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? httpContext.Request.Host.Value;
        document["servers"] = new JsonArray
        {
            new JsonObject { ["url"] = $"{forwardedProto}://{forwardedHost}/{service.Prefix}" }
        };

        return Results.Content(document.ToJsonString(), "application/json");
    });
}

app.MapGet("/catalog", (HttpRequest request) => Results.Ok(new
{
    gateway = new { name = "Vinheria Agnello API Gateway", version = "1.0", baseUrl = $"{request.Scheme}://{request.Host}" },
    services = services.Select(service => new
    {
        service.Prefix,
        name = service.DisplayName,
        service.Description,
        apiBase = $"/{service.Prefix}",
        docs = $"/{service.Prefix}/scalar",
        openApi = $"/{service.Prefix}/openapi/v1.json",
        health = $"/{service.Prefix}/health"
    })
}));
foreach (var service in services)
{
    app.MapGet($"/docs/{service.Prefix}", () => Results.Redirect($"/{service.Prefix}/scalar"));
}
app.MapGet("/health", () => Results.Ok(new { service = "gateway", status = "healthy" }));
app.MapReverseProxy();

app.Run();

internal sealed record GatewayService(string Prefix, string Host, string DisplayName, string Description)
{
    public string ClusterId => $"{Prefix}-cluster";
    public string InternalAddress => $"http://{Host}:8080";
    public string OpenApiAddress => $"{InternalAddress}/openapi/v1.json";
}
