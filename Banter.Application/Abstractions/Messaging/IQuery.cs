using Banter.SharedKernel;
using MediatR;

namespace Banter.Application.Abstractions.Messaging;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
