using Banter.API.Extensions;
using Banter.Application.Features.Auth;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banter.API.Endpoints.Auth;

internal class Logout : IEndpoint
{
    public Feature Feature => Feature.Auth;

    public bool IsAdminEndpoint => false;

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/logout", async Task<Results<NoContent, ProblemHttpResult>> (ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new LogoutCommand();

            var result = await sender.Send(command, cancellationToken);

            return result.Match(TypedResults.NoContent, CustomResults.Problem);

        }).RequireAuthorization();
    }
}
