using Vinheria.Shared.Security;

namespace ServiceTests;

public sealed class JwtTokenTests
{
    private const string Secret = "test-secret";

    [Fact]
    public void AcceptsTokenIssuedWithExpectedSecretAndRole()
    {
        var token = JwtToken.Create(Secret, "demo", "user", TimeSpan.FromMinutes(5));

        Assert.True(JwtToken.IsValid($"Bearer {token}", Secret, "user"));
    }

    [Fact]
    public void RejectsTokenWithDifferentSecret()
    {
        var token = JwtToken.Create(Secret, "demo", "user", TimeSpan.FromMinutes(5));

        Assert.False(JwtToken.IsValid($"Bearer {token}", "different-secret", "user"));
    }

    [Fact]
    public void RejectsTokenWithDifferentRole()
    {
        var token = JwtToken.Create(Secret, "demo", "user", TimeSpan.FromMinutes(5));

        Assert.False(JwtToken.IsValid($"Bearer {token}", Secret, "admin"));
    }

    [Fact]
    public void RejectsMalformedAuthorizationHeader()
    {
        Assert.False(JwtToken.IsValid("Basic credentials", Secret, "user"));
        Assert.False(JwtToken.IsValid(null, Secret, "user"));
    }
}
