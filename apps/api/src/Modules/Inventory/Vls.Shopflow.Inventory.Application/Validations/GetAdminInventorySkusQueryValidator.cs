using FluentValidation;
using Vls.Shopflow.Inventory.Application.Queries;

namespace Vls.Shopflow.Inventory.Application.Validations;

public sealed class GetAdminInventorySkusQueryValidator : AbstractValidator<GetAdminInventorySkusQuery>
{
    public const int MaxSearchLength = 100;

    public GetAdminInventorySkusQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Sort)
            .Must(s => string.IsNullOrWhiteSpace(s) || AdminInventorySkuListSort.Allowed.Contains(s.Trim().ToLowerInvariant()))
            .WithMessage(
                "Sort must be one of: default, product_name_asc, product_name_desc, sku_code_asc, sku_code_desc, stock_asc, stock_desc, available_asc, available_desc, reserved_desc, price_asc, price_desc.");
        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) || AdminInventorySkuListFilters.StatusAllowed.Contains(s.Trim().ToLowerInvariant()))
            .WithMessage("Status must be one of: all, active, inactive.");
        RuleFor(x => x.StockStatus)
            .Must(s => string.IsNullOrWhiteSpace(s) || AdminInventorySkuListFilters.StockAllowed.Contains(s.Trim().ToLowerInvariant()))
            .WithMessage("StockStatus must be one of: all, in_stock, low_stock, out_of_stock, reserved.");
        RuleFor(x => x.CategorySlug)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.CategorySlug));
        RuleFor(x => x.CategoryId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("CategoryId inválido.");
        RuleFor(x => x.ProductId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("ProductId inválido.");
        RuleFor(x => x.Q)
            .MaximumLength(MaxSearchLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Q));
    }
}
