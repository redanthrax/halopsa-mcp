using HaloPsaMcp.Modules.Authentication.Services;
using Xunit;

namespace HaloPsaMcp.Tests;

public class RedirectUriNormalizerTests {
    [Theory]
    [InlineData("https://Example.COM/cb/", "https://example.com/cb")]
    [InlineData("https://example.com:443/cb", "https://example.com/cb")]
    [InlineData("http://127.0.0.1:80/cb/", "http://127.0.0.1/cb")]
    [InlineData("https://example.com/cb", "https://example.com/cb")]
    public void Normalize_canonicalizes_host_port_and_trailing_slash(string input, string expected) {
        Assert.Equal(expected, RedirectUriNormalizer.Normalize(input));
    }
}
