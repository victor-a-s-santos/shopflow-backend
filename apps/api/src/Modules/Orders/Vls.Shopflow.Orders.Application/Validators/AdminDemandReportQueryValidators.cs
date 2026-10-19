using FluentValidation;
using Vls.Shopflow.Orders.Application.Queries;

namespace Vls.Shopflow.Orders.Application.Validators;

public static class AdminDemandReportLimits
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 200;
}

public sealed class GetAdminProductDemandQueryValidator : AbstractValidator<GetAdminProductDemandQuery>
{
    public GetAdminProductDemandQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, AdminDemandReportLimits.MaxPageSize);
        RuleFor(x => x.Q).MaximumLength(AdminDemandReportLimits.MaxSearchLength);
        RuleFor(x => x.Sort)
            .Must(sort => AdminDemandReportSort.IsAllowed(sort, AdminDemandReportSort.ProductDemand))
            .WithMessage("sort must be quantity_desc, quantity_asc, orders_desc, orders_asc, subtotal_desc, or subtotal_asc.");
        RuleFor(x => x)
            .Must(x => x.From is null || x.To is null || x.From <= x.To)
            .WithMessage("from must be less than or equal to to.");
    }
}

public sealed class GetAdminProductDemandOrdersQueryValidator : AbstractValidator<GetAdminProductDemandOrdersQuery>
{
    public GetAdminProductDemandOrdersQueryValidator()
    {
        RuleFor(x => x.SkuId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, AdminDemandReportLimits.MaxPageSize);
        RuleFor(x => x.Sort)
            .Must(sort => AdminDemandReportSort.IsAllowed(sort, AdminDemandReportSort.ProductDemandOrders))
            .WithMessage("sort must be paidAt_desc, paidAt_asc, createdAt_desc, or createdAt_asc.");
        RuleFor(x => x)
            .Must(x => x.From is null || x.To is null || x.From <= x.To)
            .WithMessage("from must be less than or equal to to.");
    }
}

public sealed class GetAdminUnpaidOrdersQueryValidator : AbstractValidator<GetAdminUnpaidOrdersQuery>
{
    public GetAdminUnpaidOrdersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, AdminDemandReportLimits.MaxPageSize);
        RuleFor(x => x.Q).MaximumLength(AdminDemandReportLimits.MaxSearchLength);
        RuleFor(x => x.Sort)
            .Must(sort => AdminDemandReportSort.IsAllowed(sort, AdminDemandReportSort.UnpaidOrders))
            .WithMessage("sort must be createdAt_desc or createdAt_asc.");
        RuleFor(x => x)
            .Must(x => x.From is null || x.To is null || x.From <= x.To)
            .WithMessage("from must be less than or equal to to.");
    }
}
