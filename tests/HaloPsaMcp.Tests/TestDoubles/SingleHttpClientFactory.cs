namespace HaloPsaMcp.Tests.TestDoubles;

internal sealed class SingleHttpClientFactory : IHttpClientFactory {
    private readonly HttpClient _client;

    public SingleHttpClientFactory(HttpClient client) => _client = client;

    public HttpClient CreateClient(string name) => _client;
}
