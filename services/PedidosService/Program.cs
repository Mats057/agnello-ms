using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Scalar.AspNetCore;
using Vinheria.Shared.Configuration;
using Vinheria.Shared.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddHttpClient("estoque", client =>
{
    client.BaseAddress = new Uri(EnvironmentSettings.Required("ESTOQUE_URL"));
    client.Timeout = TimeSpan.FromSeconds(3);
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    if (bool.TryParse(Environment.GetEnvironmentVariable("ALLOW_SELF_SIGNED_CERT"), out var allowSelfSigned) && allowSelfSigned)
    {
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }

    return handler;
});
builder.Services.AddHttpClient("mensageria", client =>
{
    client.BaseAddress = new Uri(EnvironmentSettings.Required("MENSAGERIA_URL"));
    client.Timeout = TimeSpan.FromSeconds(3);
});

var app = builder.Build();
var secret = EnvironmentSettings.Required("JWT_SECRET");
var orders = new ConcurrentDictionary<Guid, Order>();

app.MapOpenApi();
app.MapScalarApiReference("/scalar");

app.MapGet("/health", () => Results.Ok(new { service = "pedidos", status = "healthy" }));

app.MapPost("/v1/pedidos", async (CreateOrderRequest request, HttpRequest httpRequest, IHttpClientFactory clients, CancellationToken cancellationToken) =>
{
    if (!JwtToken.IsValid(httpRequest.Headers.Authorization, secret, "user"))
    {
        return Results.Unauthorized();
    }

    if (request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.Sku))
    {
        return Results.BadRequest(new { error = "sku e quantity devem ser válidos" });
    }

    var orderId = Guid.NewGuid();
    using var client = clients.CreateClient("estoque");
    client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(httpRequest.Headers.Authorization.ToString());

    HttpResponseMessage reservation;
    try
    {
        reservation = await client.PostAsJsonAsync("/v1/estoque/reservas", new { request.Sku, request.Quantity }, cancellationToken);
    }
    catch (HttpRequestException)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    if (!reservation.IsSuccessStatusCode)
    {
        return Results.Problem("não foi possível reservar o estoque", statusCode: (int)reservation.StatusCode);
    }

    var order = new Order(orderId, request.Sku, request.Quantity, "CONFIRMADO");
    orders[orderId] = order;
    var eventPublished = false;
    try
    {
        var messaging = clients.CreateClient("mensageria");
        var eventResponse = await messaging.PostAsJsonAsync(
            "/v1/mensagens/pedidos",
            new { OrderId = order.Id, order.Sku, order.Quantity, order.Status },
            cancellationToken);
        eventPublished = eventResponse.IsSuccessStatusCode;
    }
    catch (HttpRequestException)
    {
        eventPublished = false;
    }

    if (!eventPublished)
    {
        order = order with { Status = "CONFIRMADO_EVENTO_PENDENTE" };
        orders[orderId] = order;
    }

    return Results.Created($"/v1/pedidos/{orderId}", order);
});

app.MapGet("/v1/pedidos/{id:guid}", (Guid id, HttpRequest request) =>
{
    if (!JwtToken.IsValid(request.Headers.Authorization, secret, "user"))
    {
        return Results.Unauthorized();
    }

    return orders.TryGetValue(id, out var order) ? Results.Ok(order) : Results.NotFound();
});

app.Run();

public sealed record CreateOrderRequest(string Sku, int Quantity);
public sealed record Order(Guid Id, string Sku, int Quantity, string Status);
