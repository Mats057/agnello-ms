using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Scalar.AspNetCore;
using Vinheria.Shared.Configuration;
using Vinheria.Shared.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
var app = builder.Build();
var secret = EnvironmentSettings.Required("JWT_SECRET");
var users = new ConcurrentDictionary<string, User>(StringComparer.OrdinalIgnoreCase);
users["demo"] = new User("demo", Hash("vinheria-demo"));

app.MapOpenApi();
app.MapScalarApiReference("/scalar");
app.MapGet("/health", () => Results.Ok(new { service = "auth", status = "healthy" }));

app.MapPost("/v1/auth/register", (RegisterRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) ||
        string.IsNullOrWhiteSpace(request.Password) ||
        request.Password.Length < 6)
    {
        return Results.BadRequest(new { error = "username obrigatório e senha com pelo menos 6 caracteres" });
    }

    var user = new User(request.Username.Trim(), Hash(request.Password));
    return users.TryAdd(user.Username, user)
        ? Results.Created($"/v1/auth/users/{user.Username}", new { user.Username })
        : Results.Conflict(new { error = "usuário já existe" });
});

app.MapPost("/v1/auth/token", (TokenRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) ||
        string.IsNullOrWhiteSpace(request.Password) ||
        !users.TryGetValue(request.Username, out var user) ||
        !CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(user.PasswordHash),
            Convert.FromHexString(Hash(request.Password))))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new
    {
        access_token = JwtToken.Create(secret, user.Username, "user", TimeSpan.FromMinutes(15)),
        token_type = "Bearer",
        expires_in = 900
    });
});

app.Run();

static string Hash(string value) =>
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

public sealed record RegisterRequest(string Username, string Password);
public sealed record TokenRequest(string Username, string Password);
public sealed record User(string Username, string PasswordHash);
