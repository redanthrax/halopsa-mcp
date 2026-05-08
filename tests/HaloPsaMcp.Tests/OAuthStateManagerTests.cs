using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using Xunit;

namespace HaloPsaMcp.Tests;

[Collection("OAuthState")]
public class OAuthStateManagerTests {
    public OAuthStateManagerTests() {
        OAuthStateManager.PendingAuths.Clear();
        OAuthStateManager.CompletedAuths.Clear();
    }

    private static PendingAuth Make(long expires) => new() {
        HaloPsaVerifier = "v",
        ClientRedirectUri = "https://example.com/cb",
        ClientCodeChallenge = "c",
        ClientCode = "code",
        Expires = expires,
    };

    [Fact]
    public void Eviction_drops_oldest_expiring_first() {
        for (var i = 0; i < 10_000; i++) {
            OAuthStateManager.PendingAuths[$"k{i}"] = Make(1_000_000_000_000 + i);
        }
        OAuthStateManager.AddPending("freshest", Make(long.MaxValue));

        Assert.True(OAuthStateManager.PendingAuths.ContainsKey("freshest"));
        Assert.False(OAuthStateManager.PendingAuths.ContainsKey("k0"));
    }

    [Fact]
    public void CleanExpiredEntries_removes_only_expired() {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        OAuthStateManager.PendingAuths["live"] = Make(now + 60_000);
        OAuthStateManager.PendingAuths["dead"] = Make(now - 60_000);

        OAuthStateManager.CleanExpiredEntries();

        Assert.True(OAuthStateManager.PendingAuths.ContainsKey("live"));
        Assert.False(OAuthStateManager.PendingAuths.ContainsKey("dead"));
    }
}
