namespace Banter.Application.Abstractions;

internal interface IPagedQuery
{
    int PageNumber { get; }
    int PageSize { get; }
}
