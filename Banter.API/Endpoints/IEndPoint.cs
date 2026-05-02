namespace Banter.API.Endpoints;

internal interface IEndpoint
{
    public Feature Feature { get; }
    public bool IsAdminEndpoint { get; }
    public void MapEndpoint(IEndpointRouteBuilder app);
}

internal enum Feature
{
    Conversations
}
