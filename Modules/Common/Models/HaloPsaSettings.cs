namespace HaloPsaMcp.Modules.Common.Models;

public class HaloPsaSettings {
    public string Url { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
    /// <summary>file (default) or redis.</summary>
    public string TokenStoreBackend { get; set; } = "file";
    public string TokenStorePath { get; set; } = string.Empty;
    /// <summary>StackExchange.Redis connection string when <see cref="TokenStoreBackend"/> is redis.</summary>
    public string? RedisConnection { get; set; }
}
