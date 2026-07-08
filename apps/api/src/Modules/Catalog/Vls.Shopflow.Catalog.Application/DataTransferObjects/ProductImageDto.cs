namespace Vls.Shopflow.Catalog.Application.DataTransferObjects;

public sealed record ProductImageDto(Guid Id, string Url, int SortOrder, bool IsPrimary);
