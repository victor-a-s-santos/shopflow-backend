using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vls.Shopflow.BuildingBlocks.Domain.ValueObjects;

namespace Vls.Shopflow.Catalog.Infrastructure.Mappings;

/// <summary>
/// Helper para mapear o VO Price como owned type e aplicar constraints
/// </summary>
internal static class PriceOwnedMap
{
    /// <summary>
    /// Mapeia as colunas do VO Price com um prefixo opcional (ex.: "base_").
    /// Define colunas de promoção como opcionais.
    /// </summary>
    public static OwnedNavigationBuilder<TEntity, Price> MapPrice<TEntity>(
        this OwnedNavigationBuilder<TEntity, Price> owned,
        string prefix)
        where TEntity : class
    {
        // Regular (Money) – obrigatório
        owned.OwnsOne(p => p.Regular, r =>
        {
            r.Property(m => m.Amount)
                .HasColumnName($"{prefix}regular_price")
                .HasColumnType("numeric(12,2)");

            r.Property(m => m.Currency)
                .HasColumnName($"{prefix}currency")
                .HasMaxLength(3);
        });


        // Promo é opcional — se não houver, colunas ficam NULL
        // Promotional (Money?) – opcional
        owned.OwnsOne(p => p.Promotional, promo =>
        {
            promo.Property(m => m.Amount)
                .HasColumnName($"{prefix}promo_price")
                .HasColumnType("numeric(12,2)");

            promo.Property(m => m.Currency)
                .HasColumnName($"{prefix}promo_currency")
                .HasMaxLength(3);
        });
        
        owned.Navigation(p => p.Promotional).IsRequired(false);

        // Janela da promoção (no próprio Price)
        owned.Property(p => p.PromoStart)
            .HasColumnName($"{prefix}promo_start");

        owned.Property(p => p.PromoEnd)
            .HasColumnName($"{prefix}promo_end");

        return owned;
    }

    /// <summary>
    /// Aplica check constraints relacionados ao Price na TABELA do owner.
    /// Use dentro de <c>ToTable(name, t => t.AddPriceConstraints(...))</c> com o mesmo prefixo do MapPrice.
    /// </summary>
    public static TableBuilder<TEntity> AddPriceConstraints<TEntity>(
        this TableBuilder<TEntity> table,
        string prefix,
        string tableName)
        where TEntity : class
    {
        table.HasCheckConstraint(
            $"CK_{tableName}_{prefix}price_nonnegative",
            $@"({prefix}regular_price >= 0)
            AND ({prefix}promo_price IS NULL OR {prefix}promo_price >= 0)");

        table.HasCheckConstraint(
            $"CK_{tableName}_{prefix}promo_le_regular",
            $@"({prefix}promo_price IS NULL OR {prefix}promo_price <= {prefix}regular_price)");

        table.HasCheckConstraint(
            $"CK_{tableName}_{prefix}promo_window",
            $@"({prefix}promo_start IS NULL OR {prefix}promo_end IS NULL OR {prefix}promo_start <= {prefix}promo_end)");

        return table;
    }
}