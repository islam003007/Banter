using Banter.API.Extensions;
using Banter.Application.Features.Users;
using MediatR;

namespace Banter.API.Endpoints.Users;

internal class Register : IEndpoint
{
    public Feature Feature => Feature.Users;

    public bool IsAdminEndpoint => false;

    private record Request(string Email, string DisplayName, string? ProfilePictureUrl, string Password, string PasswordConfirm);
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/users/register", async (ISender sender, CancellationToken cancellationToken, Request request) =>
        {
            var command = new RegisterUserCommand(request.Email,
                request.DisplayName,
                request.ProfilePictureUrl,
                request.Password,
                request.PasswordConfirm);

            var result = await sender.Send(command, cancellationToken);

            return result.Match(TypedResults.Ok, CustomResults.Problem);
        });
    }
}
