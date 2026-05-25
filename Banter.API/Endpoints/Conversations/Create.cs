using Banter.API.Extensions;
using Banter.Application.Features.Conversations;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banter.API.Endpoints.Conversations;

internal class Create : IEndpoint
{
    public Feature Feature => Feature.Conversations;
    public bool IsAdminEndpoint => false;
    private record Request(IReadOnlyList<Guid> ParticipantsIds, string? GroupTitle);
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/conversations", async Task<Results<Ok<Guid>, ProblemHttpResult>> (ISender sender,
            CancellationToken cancellationToken,
            Request request) =>
        {
            var command = new CreateConversationCommand(request.ParticipantsIds, request.GroupTitle);

            var result = await sender.Send(command, cancellationToken);

            return result.Match(TypedResults.Ok, CustomResults.Problem); // a 201 created response is better 
                                                                         // if there were an endpoint to return the resource.
        }).RequireAuthorization();                                          
    }
}
