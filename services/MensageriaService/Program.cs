using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Scalar.AspNetCore;
using Vinheria.Shared.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
var app = builder.Build();
var rabbitHost = EnvironmentSettings.Required("RABBITMQ_HOST");
var rabbitUser = EnvironmentSettings.Required("RABBITMQ_USER");
var rabbitPassword = EnvironmentSettings.Required("RABBITMQ_PASSWORD");

app.MapOpenApi();
app.MapScalarApiReference("/scalar");
app.MapGet("/health", () => Results.Ok(new { service = "mensageria", status = "healthy", broker = rabbitHost }));

app.MapPost("/v1/mensagens/pedidos", (OrderEvent request) =>
{
    if (request.OrderId == Guid.Empty || request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.Sku))
    {
        return Results.BadRequest(new { error = "evento de pedido inválido" });
    }

    try
    {
        var factory = new ConnectionFactory { HostName = rabbitHost, UserName = rabbitUser, Password = rabbitPassword };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare("vinheria.events", ExchangeType.Topic, durable: true);
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        channel.BasicPublish("vinheria.events", "pedido.criado", properties, body);
        return Results.Accepted(value: new { published = true, routing_key = "pedido.criado", request.OrderId });
    }
    catch (Exception exception) when (exception is RabbitMQ.Client.Exceptions.BrokerUnreachableException or System.Net.Sockets.SocketException)
    {
        return Results.Problem("broker de mensageria indisponível", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

public sealed record OrderEvent(Guid OrderId, string Sku, int Quantity, string Status);
