using Banter.Application.Abstractions.Demo;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Messaging;
using Banter.Application.Errors;
using Banter.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Banter.Application.Features.Auth;

public record GetDemoUsersQuery : IQuery<IReadOnlyList<DemoUserResponse>>;

public record DemoUserResponse(Guid Id, string Email, string DisplayName, string? ProfilePictureUrl);

internal class GetDemoUsersQueryHandler(
    IAppDbContext _dbContext,
    IDemoUserDirectory _demoUsers,
    IOptions<DemoOptions> _demoOptions)
    : IQueryHandler<GetDemoUsersQuery, IReadOnlyList<DemoUserResponse>>
{
    public async Task<Result<IReadOnlyList<DemoUserResponse>>> Handle(GetDemoUsersQuery request, CancellationToken cancellationToken)
    {
        if (!_demoOptions.Value.Enabled)
            return Result.Failure<IReadOnlyList<DemoUserResponse>>(DemoErrors.Disabled);

        var emails = _demoUsers.Emails;
        var normalizedEmails = emails.Select(email => email.ToLowerInvariant()).ToList();

        var users = await _dbContext.Users
            .Where(u => u.Email != null && normalizedEmails.Contains(u.Email.ToLower()))
            .Select(u => new DemoUserResponse(u.Id, u.Email!, u.DisplayName, u.ProfilePictureUrl))
            .ToListAsync(cancellationToken);

        // Keep the picker in seed order (Ahmed first) rather than whatever order the database returns.
        var order = emails
            .Select((email, index) => new { Email = email, Index = index })
            .ToDictionary(x => x.Email, x => x.Index, StringComparer.OrdinalIgnoreCase);

        var result = users
            .Where(u => order.ContainsKey(u.Email))
            .OrderBy(u => order[u.Email])
            .ToList();

        return Result.Success<IReadOnlyList<DemoUserResponse>>(result);
    }
}
