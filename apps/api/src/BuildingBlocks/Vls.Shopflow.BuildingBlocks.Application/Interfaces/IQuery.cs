using MediatR;

namespace Vls.Shopflow.BuildingBlocks.Application.Interfaces;

public interface IQuery<out TResponse> : IRequest<TResponse>;