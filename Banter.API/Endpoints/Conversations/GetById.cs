using Banter.API.Extensions;
using Banter.Application.Features.Conversations;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banter.API.Endpoints.Conversations;

internal class GetById : IEndpoint
{
    public Feature Feature => Feature.Conversations;
    public bool IsAdminEndpoint => false;

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/conversations/{conversationId}", async Task<Results<Ok<GetConversationResponse>, ProblemHttpResult>> (
            ISender sender,
            CancellationToken cancellationToken,
            Guid conversationId) =>
        {
            var query = new GetConversationQuery(conversationId);

            var result = await sender.Send(query, cancellationToken);

            return result.Match(TypedResults.Ok, CustomResults.Problem);
        }).RequireAuthorization();
    }
}
