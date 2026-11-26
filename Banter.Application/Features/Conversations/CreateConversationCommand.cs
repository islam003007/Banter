using Banter.Application.Abstractions.Auth;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Messaging;
using Banter.Application.Bots;
using Banter.Domain.Constants;
using Banter.Domain.Conversations;
using Banter.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Banter.Application.Features.Conversations;

public record CreateConversationCommand(IReadOnlyList<Guid> ParticipantIds, string? GroupTitle = null) : ICommand<Guid>;

internal class CreateConversationCommandValidator : AbstractValidator<CreateConversationCommand>
{
    public CreateConversationCommandValidator()
    {
        RuleFor(x => x.ParticipantIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("ParticipantIds is required.")
            .Must(ids => ids.Count > 0).WithMessage("At least 1 ParticipantId is required.")
            .Must(ids => ids.All(id => id != Guid.Empty)).WithMessage("ParticipantIds cannot contain an empty user id.")
            .Must(ids => ids.Distinct().Count() <= ConversationConstants.MaxParticipants)
                .WithMessage($"A conversation can invite at most {ConversationConstants.MaxParticipants} participants.");

        RuleFor(x => x.GroupTitle)
            .MaximumLength(ConversationConstants.TitleMaxLength)
            .When(x => x.GroupTitle is not null);

        RuleFor(x => x.GroupTitle)
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithMessage("A group conversation must have a title")
            .When(x => x.ParticipantIds is not null && x.ParticipantIds.Distinct().Count() > 1);
    }
}

internal class CreateConversationCommandHandler(
    IAppDbContext _dbContext,
    IUserContext _userContext,
    IOptions<BotOptions>? botOptions = null)
    : ICommandHandler<CreateConversationCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateConversationCommand request, CancellationToken cancellationToken)
    {
        Guid currentUserId = _userContext.UserId;
        var includeBots = botOptions?.Value.Enabled ?? true;

        var requestedParticipantIds = request.ParticipantIds?.Distinct().ToList() ?? [];

        // The validator normally handles these, but the handler is also called directly (and by
        // the integration tests) so it must not rely on the pipeline to keep bad payloads out.
        if (requestedParticipantIds.Count == 0)
            return Result.Failure<Guid>(ConversationErrors.ParticipantsRequired);

        if (requestedParticipantIds.Count > ConversationConstants.MaxParticipants)
            return Result.Failure<Guid>(ConversationErrors.TooManyParticipants);

        if (request.ParticipantIds!.Contains(currentUserId))
            return Result.Failure<Guid>(ConversationErrors.CreatorCannotBeParticipant);

        // Validate the whole participant set before touching the FK graph. Missing users used to
        // surface as an opaque 500 from the ConversationParticipants foreign key.
        var existingParticipants = await _dbContext.Users
            .Where(user => requestedParticipantIds.Contains(user.Id))
            .Select(user => new { user.Id, user.IsBot })
            .ToListAsync(cancellationToken);

        var existingParticipantIds = existingParticipants.Select(user => user.Id).ToList();

        var missingParticipantIds = requestedParticipantIds.Except(existingParticipantIds).ToList();

        if (missingParticipantIds.Count > 0)
            return Result.Failure<Guid>(ConversationErrors.ParticipantsNotFound(missingParticipantIds));

        if (!includeBots && existingParticipants.Any(user => user.IsBot))
            return Result.Failure<Guid>(ConversationErrors.BotsDisabled);

        Conversation conversation;

        if (requestedParticipantIds.Count == 1)
        {
            var otherUserId = requestedParticipantIds[0];

            // A direct conversation is unique per pair: reuse the existing thread so two people
            // cannot accidentally create parallel DMs.
            Guid oldConversationId = await _dbContext.Conversations
                .Where(c => !c.IsGroup
                    && c.Participants.Count == 2
                    && c.Participants.Any(p => p.UserId == currentUserId)
                    && c.Participants.Any(p => p.UserId == otherUserId))
                .Select(c => c.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (oldConversationId != default)
                return oldConversationId;

            conversation = Conversation.CreateDM(currentUserId, otherUserId);
        }
        else
        {
            var title = string.IsNullOrWhiteSpace(request.GroupTitle) ? null : request.GroupTitle.Trim();

            if (title is null)
                return Result.Failure<Guid>(ConversationErrors.TitleRequired);

            if (title.Length > ConversationConstants.TitleMaxLength)
                return Result.Failure<Guid>(ConversationErrors.TitleTooLong);

            // The creator is implicit (and is rejected if supplied in ParticipantIds), so add
            // them to the group here. Without this the creator could not read or post to the
            // group they just created.
            conversation = Conversation.CreateGroupConversation(
                requestedParticipantIds.Prepend(currentUserId),
                title);
        }

        await _dbContext.Conversations.AddAsync(conversation, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return conversation.Id;
    }
}
