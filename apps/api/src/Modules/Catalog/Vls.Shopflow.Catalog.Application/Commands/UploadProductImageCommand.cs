using Vls.Shopflow.BuildingBlocks.Application.Interfaces;
using Vls.Shopflow.Catalog.Application.DataTransferObjects;

namespace Vls.Shopflow.Catalog.Application.Commands;

public sealed record UploadProductImageCommand(
    Guid ProductId,
    Stream Content,
    string FileName,
    string ContentType,
    long Length) : ICommand<ProductImageDto>;
