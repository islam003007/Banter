using Microsoft.EntityFrameworkCore;
using Banter.Domain.Users;
using Banter.Domain.Conversations;
using Banter.Domain.Messages;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Banter.Domain;
using Banter.Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace Banter.Infrastructure.Database;

internal class AppDbContext : IdentityDbContext<User, Role, Guid>, IAppDbContext
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public IQueryable<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>().AsNoTracking();
    public Task<IDbContextTransaction> BeginTransactionAsync() => Database.BeginTransactionAsync();
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
