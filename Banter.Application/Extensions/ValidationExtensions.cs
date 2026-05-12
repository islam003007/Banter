using Banter.Application.Abstractions;
using Banter.Application.Constants;
using FluentValidation;

namespace Banter.Application.Extensions;

internal static class ValidationExtensions
{
    public static IRuleBuilderOptions<T, T> HasValidPagination<T>(
        this IRuleBuilder<T, T> ruleBuilder)
        where T : IPagedQuery
    {
        return ruleBuilder.Must(query =>
            query.PageNumber >= 1 &&
            query.PageSize >= 1 &&
            query.PageSize <= PaginationConstants.MaxPageSize)
        .WithMessage($"Invalid pagination. PageNumber must be greater or equal to 1, PageSize must be between 1 and {PaginationConstants.MaxPageSize}.");
    }
}
