using System.Diagnostics;

namespace HaloPsaMcp.Modules.Common.Middleware;

/// <summary>
/// Logs every inbound HTTP request with method, path, request body size,
/// response status, response body size, and elapsed time.
/// Used in HTTP mode only for context measurement and diagnostics.
/// </summary>
internal class RequestLoggingMiddleware {
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger) {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context) {
        var sw = Stopwatch.StartNew();

        // Measure request body size without consuming the stream
        long requestBytes = 0;
        if (context.Request.ContentLength.HasValue) {
            requestBytes = context.Request.ContentLength.Value;
        } else if (context.Request.Body.CanSeek) {
            requestBytes = context.Request.ContentLength ?? 0;
        }

        // Wrap response body to measure bytes written
        var originalBody = context.Response.Body;
        using var responseBuffer = new CountingStream(originalBody);
        context.Response.Body = responseBuffer;

        try {
            await _next(context).ConfigureAwait(false);
        } finally {
            sw.Stop();
            context.Response.Body = originalBody;

            var statusCode = context.Response.StatusCode;
            var responseBytes = responseBuffer.BytesWritten;
            var level = statusCode >= 500 ? LogLevel.Error
                      : statusCode >= 400 ? LogLevel.Warning
                      : LogLevel.Information;

            _logger.Log(level,
                "HTTP {Method} {Path} → {StatusCode} | req={RequestBytes}B res={ResponseBytes}B elapsed={ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                statusCode,
                requestBytes,
                responseBytes,
                sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Transparent stream wrapper that counts bytes written through it.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213",
        Justification = "_inner is owned by the caller (ASP.NET response body) and must not be disposed here.")]
    private sealed class CountingStream : Stream {
        private readonly Stream _inner;
        public long BytesWritten { get; private set; }

        public CountingStream(Stream inner) {
            _inner = inner;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken ct) => _inner.FlushAsync(ct);
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) {
            BytesWritten += count;
            _inner.Write(buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) {
            BytesWritten += count;
            await _inner.WriteAsync(buffer.AsMemory(offset, count), ct).ConfigureAwait(false);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) {
            BytesWritten += buffer.Length;
            await _inner.WriteAsync(buffer, ct).ConfigureAwait(false);
        }

        protected override void Dispose(bool disposing) {
            // _inner is owned by the caller (the ASP.NET response body); we must not dispose it.
            base.Dispose(disposing);
        }
    }
}
