using System.Net;
using HaloPsaMcp.Modules.HaloPsa.Services;
using Xunit;

namespace HaloPsaMcp.Tests;

public class HaloPsaResponseSanitizerTests {
    [Fact]
    public void SafeFailureMessage_omits_upstream_body() {
        var msg = HaloPsaResponseSanitizer.SafeFailureMessage("API GET /api/Tickets", HttpStatusCode.BadRequest);
        Assert.Equal("API GET /api/Tickets failed (400).", msg);
        Assert.DoesNotContain("administrator", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SqlLogFingerprint_is_stable_and_short() {
        var a = HaloPsaResponseSanitizer.SqlLogFingerprint("SELECT 1");
        var b = HaloPsaResponseSanitizer.SqlLogFingerprint("SELECT 1");
        var c = HaloPsaResponseSanitizer.SqlLogFingerprint("SELECT 2");
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(12, a.Length);
    }
}
