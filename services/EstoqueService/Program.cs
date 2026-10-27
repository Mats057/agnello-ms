using System.Collections.Concurrent;
using Scalar.AspNetCore;
using Vinheria.Shared.Configuration;
using Vinheria.Shared.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
var app = builder.Build();
var secret = EnvironmentSettings.Required("JWT_SECRET");
var stock = new ConcurrentDictionary<string, int>(new[]
{
    new KeyValuePair<string, int>("vinho-tinto", 10),
    new KeyValuePair<string, int>("vinho-branco", 8)
});

app.MapOpenApi();
app.MapScalarApiReference("/scalar");

app.MapGet("/health", () => Results.Ok(new { service = "estoque", status = "healthy" }));

app.MapGet("/v1/estoque", (HttpRequest request) =>
{
    if (!JwtToken.IsValid(request.Headers.Authorization, secret, "user"))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(stock);
});

app.MapPost("/v1/estoque/reservas", (ReserveRequest request, HttpRequest httpRequest) =>
{
    if (!JwtToken.IsValid(httpRequest.Headers.Authorization, secret, "user"))
    {
        return Results.Unauthorized();
    }

    if (request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.Sku))
    {
        return Results.BadRequest(new { error = "sku e quantity devem ser válidos" });
    }

    lock (stock)
    {
        if (!stock.TryGetValue(request.Sku, out var available))
        {
            return Results.NotFound(new { error = "sku não encontrado" });
        }

        if (available < request.Quantity)
        {
            return Results.Conflict(new { error = "estoque insuficiente", available });
        }

        stock[request.Sku] = available - request.Quantity;
        return Results.Ok(new { reserved = true, request.Sku, request.Quantity, remaining = stock[request.Sku] });
    }
});

app.Run();

public sealed record ReserveRequest(string Sku, int Quantity);
