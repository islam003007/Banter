using Banter.API.Extensions;
using Banter.Application.Constants;
using Banter.Application.Features.Messages;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banter.API.Endpoints.Messages;

internal class GetConversationMessages : IEndpoint
{
    public bool IsAdminEndpoint => false;
    public Feature Feature => Feature.Conversations;
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("conversations/{ConversationId}/messages", async Task<Results<Ok<GetMessagesResponse>, ProblemHttpResult>> (ISender sender,
            CancellationToken cancellationToken,
            Guid ConversationId,
            string? cursor,
            int pageSize = PaginationConstants.DefaultPageSize) =>
        {
            var query = new GetMessagesQuery(ConversationId, cursor, pageSize);

            var result = await sender.Send(query, cancellationToken);

            return result.Match(TypedResults.Ok, CustomResults.Problem);

        }).RequireAuthorization();
    }
}
