using Banter.API.Extensions;
using Banter.Application.Features.Auth;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banter.API.Endpoints.Auth;

internal class Me : IEndpoint
{
    public Feature Feature => Feature.Auth;
    public bool IsAdminEndpoint => false;

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/me", async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetCurrentUserQuery(), cancellationToken);

            return result.Match(TypedResults.Ok, CustomResults.Problem);
        }).RequireAuthorization();
    }
}
