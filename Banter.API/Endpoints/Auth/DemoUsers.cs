using Banter.API.Extensions;
using Banter.Application.Features.Auth;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banter.API.Endpoints.Auth;

internal class DemoUsers : IEndpoint
{
    public Feature Feature => Feature.Auth;
    public bool IsAdminEndpoint => false;

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/demo-users", async Task<Results<Ok<IReadOnlyList<DemoUserResponse>>, ProblemHttpResult>> (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetDemoUsersQuery(), cancellationToken);

            return result.Match(TypedResults.Ok, CustomResults.Problem);
        });
    }
}
