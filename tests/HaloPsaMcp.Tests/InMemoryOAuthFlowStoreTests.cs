using HaloPsaMcp.Modules.Authentication.Models;
using HaloPsaMcp.Modules.Authentication.Services;
using Xunit;

namespace HaloPsaMcp.Tests;

[Collection("OAuthFlow")]
public class InMemoryOAuthFlowStoreTests {
    private readonly InMemoryOAuthFlowStore _store = new();

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
            _store.AddPending($"k{i}", Make(1_000_000_000_000L + i));
        }
        _store.AddPending("freshest", Make(long.MaxValue));

        Assert.True(_store.HasPending("freshest"));
        Assert.False(_store.HasPending("k0"));
    }

    [Fact]
    public void CleanExpiredEntries_removes_only_expired() {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _store.AddPending("live", Make(now + 60_000));
        _store.AddPending("dead", Make(now - 60_000));

        _store.CleanExpiredEntries();

        Assert.True(_store.HasPending("live"));
        Assert.False(_store.HasPending("dead"));
    }
}
