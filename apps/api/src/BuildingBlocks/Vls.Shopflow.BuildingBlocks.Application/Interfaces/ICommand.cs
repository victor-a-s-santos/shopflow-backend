using MediatR;

namespace Vls.Shopflow.BuildingBlocks.Application.Interfaces;

public interface ICommand<out TResponse> : IRequest<TResponse>;
public interface ICommand : IRequest;