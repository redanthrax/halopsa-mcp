using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace HaloPsaMcp.Tests;

public class TokenEntryProtectorTests {
    [Fact]
    public void Roundtrip_encrypts_session_entry() {
        var protector = new TokenEntryProtector(DataProtectionProvider.Create("HaloPsaMcp.Tests"));
        var entry = new UserTokenEntry {
            AccessToken = "halo_access",
            RefreshToken = "halo_refresh",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds(),
            McpRefreshToken = "mcr_test"
        };

        var protectedPayload = protector.Protect(entry);
        Assert.DoesNotContain("halo_access", protectedPayload, StringComparison.Ordinal);

        var restored = protector.Unprotect(protectedPayload);
        Assert.NotNull(restored);
        Assert.Equal(entry.AccessToken, restored!.AccessToken);
        Assert.Equal(entry.McpRefreshToken, restored.McpRefreshToken);
    }

    [Fact]
    public void Unprotect_reads_legacy_plaintext_json() {
        var protector = new TokenEntryProtector(DataProtectionProvider.Create("HaloPsaMcp.Tests.Legacy"));
        const string plaintext = """{"access_token":"plain","refresh_token":"","expires_at":999,"mcp_refresh_token":"mcr"}""";
        var restored = protector.Unprotect(plaintext);
        Assert.NotNull(restored);
        Assert.Equal("plain", restored!.AccessToken);
    }
}
