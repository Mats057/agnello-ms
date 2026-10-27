namespace Vinheria.Shared.Configuration;

public static class EnvironmentSettings
{
    public static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"A variável de ambiente {name} é obrigatória.");
}
