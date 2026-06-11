using System.Net;

namespace HaloPsaMcp.Tests.TestDoubles;

internal sealed class StubHttpHandler : HttpMessageHandler {
    public Func<HttpRequestMessage, HttpResponseMessage>? Responder { get; set; }
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) {
        LastRequest = request;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var response = Responder?.Invoke(request)
            ?? new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };
        return response;
    }
}
