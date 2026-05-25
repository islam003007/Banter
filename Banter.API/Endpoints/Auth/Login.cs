using Banter.API.Extensions;
using Banter.Application.Features.Auth;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banter.API.Endpoints.Auth;

internal class Login : IEndpoint
{
    public Feature Feature => Feature.Auth;
    public bool IsAdminEndpoint => false;
    private record Request(string Email, string Password);
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", async Task<Results<Ok, ProblemHttpResult>> (ISender sender, CancellationToken cancellationToken, Request request) =>
        {
            var command = new LoginCommand(request.Email, request.Password);

            var result = await sender.Send(command, cancellationToken);

            return result.Match(TypedResults.Ok, CustomResults.Problem);

        });
    }
}
