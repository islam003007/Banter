using Banter.API.Extensions;
using Banter.Application.Features.Conversations;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banter.API.Endpoints.Conversations;

internal class MarkAsRead : IEndpoint
{
    public Feature Feature => Feature.Conversations;
    public bool IsAdminEndpoint => false;
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/conversations/{ConversationId}/mark-as-read", async Task<Results<NoContent, ProblemHttpResult>> (ISender sender,
            CancellationToken cancellationToken,
            Guid ConversationId) =>
        {
            var command = new MarkConversationAsReadCommand(ConversationId);

            var result = await sender.Send(command, cancellationToken);

            return result.Match(TypedResults.NoContent, CustomResults.Problem);

        }).RequireAuthorization();
    }
}
