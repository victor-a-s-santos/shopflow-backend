using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vls.Shopflow.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCategorySlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "slug",
                schema: "catalog",
                table: "categories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // Backfill from known seed names (Slug.CreateFromName rules).
            migrationBuilder.Sql(
                """
                UPDATE catalog.categories SET slug = CASE "Name"
                    WHEN 'Camisetas' THEN 'camisetas'
                    WHEN 'Calças' THEN 'calcas'
                    WHEN 'Vestidos' THEN 'vestidos'
                    WHEN 'Tênis' THEN 'tenis'
                    WHEN 'Casacos' THEN 'casacos'
                    WHEN 'Blusas' THEN 'blusas'
                    WHEN 'Polos' THEN 'polos'
                    WHEN 'Blazers' THEN 'blazers'
                    WHEN 'Bermudas' THEN 'bermudas'
                    WHEN 'Shorts' THEN 'shorts'
                    WHEN 'Camisas' THEN 'camisas'
                    WHEN 'Jaquetas' THEN 'jaquetas'
                    WHEN 'Croppeds' THEN 'croppeds'
                    WHEN 'Macacões' THEN 'macacoes'
                    WHEN 'Moletons' THEN 'moletons'
                    WHEN 'Saias' THEN 'saias'
                    WHEN 'Suéteres' THEN 'sueteres'
                    WHEN 'Cardigans' THEN 'cardigans'
                    WHEN 'Tops / Regatas' THEN 'tops-regatas'
                    WHEN 'Body' THEN 'body'
                    WHEN 'Bolsas' THEN 'bolsas'
                    WHEN 'Moda Praia' THEN 'moda-praia'
                    WHEN 'Acessórios' THEN 'acessorios'
                    WHEN 'Cuecas' THEN 'cuecas'
                    WHEN 'Lingerie' THEN 'lingerie'
                    ELSE NULL
                END
                WHERE slug IS NULL OR slug = '';
                """);

            // Fallback for any unexpected category names: stable unique slug from Id.
            migrationBuilder.Sql(
                """
                UPDATE catalog.categories
                SET slug = 'cat-' || replace("Id"::text, '-', '')
                WHERE slug IS NULL OR slug = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "slug",
                schema: "catalog",
                table: "categories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_categories_slug",
                schema: "catalog",
                table: "categories",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_categories_slug",
                schema: "catalog",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "slug",
                schema: "catalog",
                table: "categories");
        }
    }
}
