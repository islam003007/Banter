using Banter.API.Extensions;
using Banter.Application.Features.Messages;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banter.API.Endpoints.Messages;

internal class Create : IEndpoint
{
    public Feature Feature => Feature.Conversations;
    public bool IsAdminEndpoint => false;
    private record Request(string Content);
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/conversations/{ConversationId}/messages", async Task<Results<Ok<Guid>, ProblemHttpResult>> (ISender sender,
            CancellationToken cancellationToken,
            Guid ConversationId,
            Request request) =>
        {
            var command = new SendMessageCommand(ConversationId, request.Content);

            var result = await sender.Send(command, cancellationToken);

            return result.Match(TypedResults.Ok, CustomResults.Problem);

        }).RequireAuthorization();
    }
}
