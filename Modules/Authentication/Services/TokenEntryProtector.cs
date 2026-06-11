using System.Security.Cryptography;
using System.Text.Json;
using HaloPsaMcp.Modules.Authentication.Models;
using Microsoft.AspNetCore.DataProtection;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>Encrypts individual session payloads at rest (Redis backend).</summary>
internal sealed class TokenEntryProtector {
    private const string ProtectorPurpose = "HaloPsaMcp.TokenStorage.v1";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly IDataProtector _protector;

    internal TokenEntryProtector(IDataProtectionProvider provider) {
        _protector = provider.CreateProtector(ProtectorPurpose);
    }

    internal string Protect(UserTokenEntry entry) =>
        _protector.Protect(JsonSerializer.Serialize(entry, JsonOptions));

    internal UserTokenEntry? Unprotect(string raw) {
        string json;
        try {
            json = _protector.Unprotect(raw);
        } catch (CryptographicException) {
            // Migrate legacy plaintext Redis payloads written before encryption.
            json = raw;
        }

        try {
            return JsonSerializer.Deserialize<UserTokenEntry>(json, JsonOptions);
        } catch (JsonException) {
            return null;
        }
    }
}
