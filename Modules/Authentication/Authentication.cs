#pragma warning disable IDE0005 // Using directive is unnecessary - false positive
using HaloPsaMcp.Modules.Authentication.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0005

namespace HaloPsaMcp.Modules.Authentication;

/// <summary>
/// Authentication module registration - OAuth, token validation, middleware
/// </summary>
internal class AuthenticationModuleRegistrar : IModuleRegistrar
{
    public int Priority => 2; // Register second - depends on Common

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Register authentication services
        services.AddSingleton<McpAuthenticationService>();
        services.AddSingleton<TokenStorageService>();

        // HttpContextAccessor needed for per-user token retrieval
        services.AddHttpContextAccessor();
    }
}