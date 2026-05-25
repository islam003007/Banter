using Banter.SharedKernel;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banter.API.Extensions;

public static class ResultExtensions
{
    public static Results<TSuccesOut, TFailOut> Match<TSuccesOut, TFailOut>(this Result result,
        Func<TSuccesOut> onSuccess,
        Func<Result, TFailOut> onFailure)
        where TSuccesOut : IResult
        where TFailOut : IResult
        => result.IsSuccess ? onSuccess() : onFailure(result);

    public static Results<TSuccessOut, TFaileOut> Match<TIn, TSuccessOut, TFaileOut>(
        this Result<TIn> result,
        Func<TIn, TSuccessOut> onSuccess,
        Func<Result<TIn>, TFaileOut> onFailure)
        where TSuccessOut : IResult
        where TFaileOut : IResult
        => result.IsSuccess ? onSuccess(result.Value) : onFailure(result);
}
