using Banter.Application.Abstractions.Messaging;
using Banter.Application.Errors;
using Banter.Domain.Users;
using Banter.SharedKernel;
using FluentValidation;
using Microsoft.AspNetCore.Identity;


namespace Banter.Application.Features.Auth;

public record LoginCommand(string Email, string Password) : ICommand;

internal class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}

internal class LoginCommandHandler(SignInManager<User> _signInManager, UserManager<User> _userManager) : ICommandHandler<LoginCommand>
{
    public async Task<Result> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        // Bot accounts exist only as message senders. They have no password and must never get a
        // session, even if an operator later assigns one by accident. Return the generic failure so
        // the response does not reveal which accounts are bots.
        if (user is { IsBot: true })
            return Result.Failure(AuthErrors.LoginFailed);

        var loginResult = await _signInManager.PasswordSignInAsync(request.Email,
            request.Password,
            isPersistent: true,
            lockoutOnFailure: false); // In a producitno system set to true.

        // These are extra checks in this system neither lockout nor email confirmation is enabled right now.
        if (loginResult.Succeeded)
        {
            return Result.Success();
        }
        else if (loginResult.IsLockedOut)
        {
            return Result.Failure(AuthErrors.LockedOut);
        }
        else if (loginResult.IsNotAllowed)
        {
            return Result.Failure(AuthErrors.EmailNotConfirmed); 
        }                                                       
        else
        {
            return Result.Failure(AuthErrors.LoginFailed);
        }
    }
}