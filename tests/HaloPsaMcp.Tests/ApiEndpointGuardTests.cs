using HaloPsaMcp.Modules.HaloPsa.Services;
using Xunit;

namespace HaloPsaMcp.Tests;

public class ApiEndpointGuardTests {
    [Theory]
    [InlineData("/api/Tickets", "GET")]
    [InlineData("/api/Agent/me", "POST")]
    [InlineData("/api/Invoice/123", "PUT")]
    public void Validate_accepts_allowed_paths(string endpoint, string method) {
        ApiEndpointGuard.Validate(endpoint, method);
    }

    [Theory]
    [InlineData("/admin/users", "GET")]
    [InlineData("https://evil.example/api/Tickets", "GET")]
    [InlineData("/api/../etc/passwd", "GET")]
    [InlineData("/api/Tickets", "DELETE")]
    public void Validate_rejects_unsafe_paths(string endpoint, string method) {
        Assert.ThrowsAny<ArgumentException>(() => ApiEndpointGuard.Validate(endpoint, method));
    }
}
