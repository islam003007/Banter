using Banter.API.Extensions;
using Banter.Application.Features.Auth;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banter.API.Endpoints.Auth;

internal class DemoLogin : IEndpoint
{
    public Feature Feature => Feature.Auth;
    public bool IsAdminEndpoint => false;

    private record Request(Guid UserId);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/demo-login", async Task<Results<Ok, ProblemHttpResult>> (
            ISender sender,
            CancellationToken cancellationToken,
            Request request) =>
        {
            var command = new DemoLoginCommand(request.UserId);

            var result = await sender.Send(command, cancellationToken);

            return result.Match(TypedResults.Ok, CustomResults.Problem);
        });
    }
}
