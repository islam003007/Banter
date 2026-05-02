namespace Banter.API.Endpoints.Conversations;

internal class Add : IEndpoint
{
    public Feature Feature => Feature.Conversations;

    public bool IsAdminEndpoint => false;

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        
    }
}
