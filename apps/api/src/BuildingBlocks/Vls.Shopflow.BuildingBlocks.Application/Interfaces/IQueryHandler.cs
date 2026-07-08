using MediatR;

namespace Vls.Shopflow.BuildingBlocks.Application.Interfaces;

public interface IQueryHandler<in TQuery, TResult> 
    : IRequestHandler<TQuery, TResult> 
    where TQuery : IQuery<TResult>;