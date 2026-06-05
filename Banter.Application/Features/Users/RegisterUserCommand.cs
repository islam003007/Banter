using Banter.Application.Abstractions.Messaging;
using Banter.Domain.Constants;
using Banter.Domain.Users;
using Banter.SharedKernel;
using FluentValidation;
using Microsoft.AspNetCore.Identity;

namespace Banter.Application.Features.Users;

public record RegisterUserCommand(string Email, string DisplayName, string? ProfilePictureUrl, string Password, string PasswordConfirm)
    : ICommand<Guid>;

internal class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(UserConstants.DisplayNameMaxLength);

        RuleFor(x => x.ProfilePictureUrl)
            .MaximumLength(UserConstants.ProfilePictureUrlMaxLength);

        RuleFor(x => x.Password)
            .NotEmpty();

        RuleFor(x => x.PasswordConfirm)
            .Equal(x => x.Password);
    }
}

internal class RegisterUserCommandHandler(UserManager<User> _userManager, SignInManager<User> _signInManager) : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var user = new User(request.Email, request.DisplayName, request.ProfilePictureUrl);

        var identityResult = await _userManager.CreateAsync(user, request.Password);

        if (!identityResult.Succeeded)
            return Result.Failure<Guid>(new MultiError(identityResult.Errors.Select(e => new Error(e.Code, e.Description, ErrorType.Problem))));

        await _signInManager.SignInAsync(user, isPersistent: true);

        return user.Id;
    }
}