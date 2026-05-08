namespace HaloPsaMcp.Modules.HaloPsa.Queries.Projects;

/// <summary>
/// Query to list projects. Backed by FAULTS where RequestTypeNew is a
/// project-class request type (REQUESTTYPE.RTIsProject = 1).
/// </summary>
public record ListProjectsQuery(
    int Count = 25,
    int? ClientId = null,
    string? Search = null,
    bool OpenOnly = true);
