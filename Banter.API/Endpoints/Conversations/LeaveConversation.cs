using Banter.API.Extensions;
using Banter.Application.Features.Conversations;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banter.API.Endpoints.Conversations;

internal class LeaveConversation : IEndpoint
{
    public bool IsAdminEndpoint => false;
    public Feature Feature => Feature.Conversations;
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/conversations/{ConversationId}/participants/me", async Task<Results<NoContent, ProblemHttpResult>> (ISender sender,
            CancellationToken cancellationToken,
            Guid ConversationId) =>
        {
            var command = new LeaveConversationCommand(ConversationId);

            var result = await sender.Send(command, cancellationToken);

            return result.Match(TypedResults.NoContent, CustomResults.Problem);

        }).RequireAuthorization();
    }
}
