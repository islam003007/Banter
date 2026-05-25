using Banter.API.Extensions;
using Banter.Application.Constants;
using Banter.Application.Features.Conversations;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banter.API.Endpoints.Conversations;

internal class Get : IEndpoint
{
    public bool IsAdminEndpoint => false;
    public Feature Feature => Feature.Conversations;
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/conversations", async Task<Results<Ok<GetInboxResponse>, ProblemHttpResult>> (ISender sender,
            CancellationToken cancellationToken,
            string? cursor,
            int PageSize = PaginationConstants.DefaultPageSize)
             =>
        {
            var query = new GetInboxQuery(cursor, PageSize);

            var result = await sender.Send(query, cancellationToken);

            return result.Match(TypedResults.Ok, CustomResults.Problem);
        }).RequireAuthorization();
    }
}
