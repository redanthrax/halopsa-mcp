using HaloPsaMcp.Modules.Authentication.Models;

namespace HaloPsaMcp.Modules.Authentication.Services;

/// <summary>
/// Cross-request OAuth authorization state (pending HaloPSA flows and
/// short-lived authorization codes). Must be shared across replicas when
/// running more than one pod.
/// </summary>
public interface IOAuthFlowStore {
    void AddPending(string key, PendingAuth value);
    bool TryRemovePending(string key, out PendingAuth? value);
    void AddCompleted(string key, CompletedAuth value);
    bool TryRemoveCompleted(string key, out CompletedAuth? value);
    int CleanExpiredEntries();
    int PendingCount { get; }
    int CompletedCount { get; }
}
