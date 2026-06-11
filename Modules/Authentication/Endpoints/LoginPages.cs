using System.Globalization;
using System.Net;
using HaloPsaMcp.Modules.Common.Models;

namespace HaloPsaMcp.Modules.Authentication.Endpoints;

internal static class LoginPages {
    internal static IResult Success() => Results.Content(SuccessHtml, "text/html");

    internal static IResult DesktopStatus(AppConfig config, bool authenticated, string loginUrl) {
        var status = authenticated
            ? """<p class="ok">Signed in — return to desktop MCP client and retry your request.</p>"""
            : $"""<p class="warn">Not signed in yet.</p><p><a class="btn" href="{WebUtility.HtmlEncode(loginUrl)}">Sign in to HaloPSA</a></p>""";
        var html = string.Format(
            CultureInfo.InvariantCulture,
            StatusHtmlTemplate,
            WebUtility.HtmlEncode(loginUrl),
            WebUtility.HtmlEncode(config.HaloPsa.Url),
            status);
        return Results.Content(html, "text/html");
    }

    private const string SuccessHtml = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>HaloPSA — signed in</title>
          <style>
            body { font-family: system-ui, sans-serif; max-width: 32rem; margin: 4rem auto; padding: 0 1rem; color: #1a1a1a; }
            h1 { font-size: 1.35rem; }
            .ok { color: #0d6b3a; }
          </style>
        </head>
        <body>
          <h1>Signed in to HaloPSA</h1>
          <p class="ok">Authentication succeeded.</p>
          <p>You can close this tab and return to <strong>desktop MCP client</strong>. Retry your HaloPSA request — no restart needed.</p>
        </body>
        </html>
        """;

    private const string StatusHtmlTemplate = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>HaloPSA MCP</title>
          <style>
            body {{ font-family: system-ui, sans-serif; max-width: 36rem; margin: 3rem auto; padding: 0 1rem; color: #1a1a1a; line-height: 1.5; }}
            h1 {{ font-size: 1.35rem; }}
            .btn {{ display: inline-block; margin-top: 0.5rem; padding: 0.6rem 1rem; background: #2563eb; color: #fff; text-decoration: none; border-radius: 6px; }}
            .muted {{ color: #555; font-size: 0.9rem; }}
            .ok {{ color: #0d6b3a; }}
            .warn {{ color: #9a3412; }}
          </style>
        </head>
        <body>
          <h1>HaloPSA MCP (desktop MCP client)</h1>
          <p class="muted">Tenant: {1}</p>
          {2}
          <p class="muted">Login URL: <code>{0}</code></p>
        </body>
        </html>
        """;
}
