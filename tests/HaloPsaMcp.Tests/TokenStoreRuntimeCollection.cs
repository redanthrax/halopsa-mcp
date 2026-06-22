using HaloPsaMcp.Modules.Authentication.Services;
using Xunit;

namespace HaloPsaMcp.Tests;

[CollectionDefinition("TokenStoreRuntime", DisableParallelization = true)]
public sealed class TokenStoreRuntimeCollection;

internal static class TokenStoreRuntimeTestReset {
    internal sealed class Scope : IDisposable {
        private readonly bool _previous;

        private Scope(bool disableDefaultFallback) {
            _previous = TokenStoreRuntime.DisableDefaultFallback;
            TokenStoreRuntime.DisableDefaultFallback = disableDefaultFallback;
        }

        internal static Scope WithDefaultFallbackDisabled() => new(true);

        public void Dispose() {
            TokenStoreRuntime.DisableDefaultFallback = _previous;
        }
    }
}
