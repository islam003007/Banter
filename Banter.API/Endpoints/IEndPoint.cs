namespace Banter.API.Endpoints;

public interface IEndpoint
{
    public Feature Feature { get; }
    public bool IsAdminEndpoint { get; }
    public void MapEndpoint(IEndpointRouteBuilder app);
}

public enum Feature
{
    Conversations,
    Messages,
    Users,
    Auth
}
