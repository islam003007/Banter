using Banter.API.Extensions;
using Banter.Application.Constants;
using Banter.Application.Features.Users;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banter.API.Endpoints.Users;

internal class Search : IEndpoint
{
    public Feature Feature => Feature.Users;
    public bool IsAdminEndpoint => false;
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/users/search", async Task<Results<Ok<SearchUsersResponse>, ProblemHttpResult>> (ISender sender,
            CancellationToken cancellationToken,
            string searchTerm,
            string? cursor,
            int pageSize = PaginationConstants.DefaultPageSize) =>
        {
            var query = new SearchUsersQuery(searchTerm, cursor, pageSize);

            var result = await sender.Send(query, cancellationToken);

            return result.Match(TypedResults.Ok, CustomResults.Problem);

        }).RequireAuthorization();
    }
}
