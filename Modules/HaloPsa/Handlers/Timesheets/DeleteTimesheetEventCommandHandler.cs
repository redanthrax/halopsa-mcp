using HaloPsaMcp.Modules.HaloPsa.Queries.Timesheets;
using HaloPsaMcp.Modules.HaloPsa.Services;

namespace HaloPsaMcp.Modules.HaloPsa.Handlers.Timesheets;

public static class DeleteTimesheetEventCommandHandler {
    public static async Task<DeleteTimesheetEventResult> Handle(
        DeleteTimesheetEventCommand command,
        HaloPsaClientFactory factory,
        IHttpContextAccessor contextAccessor) {
        var client = factory.CreateClientOrThrow(contextAccessor.HttpContext);
        await client.DeleteAsync($"/api/TimesheetEvent/{command.Id}").ConfigureAwait(false);
        return new DeleteTimesheetEventResult(command.Id);
    }
}
