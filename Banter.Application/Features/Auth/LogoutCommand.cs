using Banter.Application.Abstractions.Messaging;
using Banter.Domain.Users;
using Banter.SharedKernel;
using Microsoft.AspNetCore.Identity;

namespace Banter.Application.Features.Auth;

public record LogoutCommand() : ICommand;

internal class LogoutCommandHandler(SignInManager<User> _signInManager) : ICommandHandler<LogoutCommand>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        await _signInManager.SignOutAsync();

        return Result.Success();
    }
}