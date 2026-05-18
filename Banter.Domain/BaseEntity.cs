namespace Banter.Domain;

public abstract class BaseEntity
{
    public Guid Id { get; protected init; }
}
