using Banter.Application.Abstractions.Demo;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Messaging;
using Banter.Application.Errors;
using Banter.Domain.Users;
using Banter.SharedKernel;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Banter.Application.Features.Auth;

public record DemoLoginCommand(Guid UserId) : ICommand;

internal class DemoLoginCommandValidator : AbstractValidator<DemoLoginCommand>
{
    public DemoLoginCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}

internal class DemoLoginCommandHandler(
    IAppDbContext _dbContext,
    SignInManager<User> _signInManager,
    IDemoUserDirectory _demoUsers,
    IOptions<DemoOptions> _demoOptions)
    : ICommandHandler<DemoLoginCommand>
{
    public async Task<Result> Handle(DemoLoginCommand request, CancellationToken cancellationToken)
    {
        if (!_demoOptions.Value.Enabled)
            return Result.Failure(DemoErrors.Disabled);

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        // Never let a caller sign in as an account outside the seeded demo allow-list.
        if (user is null || !_demoUsers.IsAllowed(user.Email))
            return Result.Failure(DemoErrors.UserNotAllowed);

        await _signInManager.SignInAsync(user, isPersistent: true);

        return Result.Success();
    }
}
