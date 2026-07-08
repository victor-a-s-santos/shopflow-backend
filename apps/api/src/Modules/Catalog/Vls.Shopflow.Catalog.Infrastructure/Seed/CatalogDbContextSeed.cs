        using Microsoft.EntityFrameworkCore;
        using Vls.Shopflow.Catalog.Domain.Entities;

        namespace Vls.Shopflow.Catalog.Infrastructure.Seed;

        public static class CatalogDbContextSeed
        {
            public static async Task SeedAsync(CatalogDbContext db)
            {
                // Evita duplicação total do seed
                if (await db.Categories.AnyAsync())
                    return;

                // ----------------------------------------
                // 1) Criar Categorias e Definitions
                // ----------------------------------------

                var categoriasIniciais = new List<Category>
                {
                    new("Camisetas"), new("Calças"), new("Vestidos"), new("Tênis"),
                    new("Casacos"), new("Blusas"), new("Polos"), new("Blazers"),
                    new("Bermudas"), new("Shorts"), new("Camisas"), new("Jaquetas"),
                    new("Croppeds"), new("Macacões"), new("Moletons"), new("Saias"),
                    new("Suéteres"), new("Cardigans"), new("Tops / Regatas"), new("Body"),
                    new("Bolsas"), new("Moda Praia"), new("Acessórios"),
                    new("Cuecas"), new("Lingerie")
                };

                await db.Categories.AddRangeAsync(categoriasIniciais);

                var cor = AttributeDefinition.Create("Cor", false, null);
                var tamanho = AttributeDefinition.Create("Tamanho", false, null);

                await db.AttributeDefinitions.AddRangeAsync(cor, tamanho);

                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();

                // ----------------------------------------
                // 2) Recarregar entidades limpas
                // ----------------------------------------

                var categorias = await db.Categories.AsNoTracking().ToListAsync();
                cor = await db.AttributeDefinitions.AsNoTracking().FirstAsync(a => a.Name == "Cor");
                tamanho = await db.AttributeDefinitions.AsNoTracking().FirstAsync(a => a.Name == "Tamanho");

                Category Find(string name) => categorias.First(c => c.Name == name);

                // ----------------------------------------
                // 3) Inserir valores de Cor (via DBSET)
                // ----------------------------------------

                var cores = new List<(string Name, string? Hex)>
                {
                    ("Preto", "#000000"), ("Branco", "#FFFFFF"), ("Off-White", "#F8F5F2"),
                    ("Gelo", "#F4F4F4"), ("Cinza Claro", "#D4D4D4"), ("Cinza Médio", "#A3A3A3"),
                    ("Cinza Escuro", "#4B4B4B"), ("Prata", "#C0C0C0"), ("Grafite", "#2F2F2F"),

                    ("Creme", "#FBF5E9"), ("Bege", "#F5F5DC"), ("Nude", "#E7D7C1"),
                    ("Areia", "#E4D3B5"), ("Caramelo", "#C28C4D"), ("Marrom", "#7A4A21"),
                    ("Chocolate", "#4E2C18"),

                    ("Terracota", "#CB6D51"), ("Telha", "#B75E41"), ("Ferrugem", "#A84032"),
                    ("Mostarda", "#D8A31A"), ("Dourado Fosco", "#C49B00"),
                    ("Oliva", "#6B7B45"), ("Verde Militar", "#4D5D2C"),

                    ("Rosa Quartzo", "#F7DDE2"), ("Rosa Bebê", "#F4C2C2"), ("Rosa Seco", "#D8A7B1"),
                    ("Azul Bebê", "#C9DFF2"), ("Azul Serenity", "#A7C7E7"), ("Lavanda", "#C6B4E9"),
                    ("Lilás", "#B497DD"), ("Verde Menta", "#BCE9D0"), ("Pistache", "#B4D9A5"),
                    ("Amarelo Pastel", "#FFF4A3"),

                    ("Azul Royal", "#1E3A8A"), ("Azul Marinho", "#0A2342"), ("Vermelho", "#D2232A"),
                    ("Vinho", "#5B0C18"), ("Bordô", "#701A2E"), ("Roxo", "#532E69"),

                    ("Verde Bandeira", "#0B7A28"), ("Verde Esmeralda", "#008F63"),
                    ("Amarelo Vivo", "#F4D03F"), ("Laranja", "#F26B38"),

                    ("Jeans Claro", "#A7C7E7"), ("Jeans Médio", "#6A89A6"), ("Jeans Escuro", "#2D4A68")
                };

                var valoresCor = cores.Select(c =>
                    new AttributeValueDefinition(cor.Id, c.Name, c.Hex)
                ).ToList();

                await db.AttributeValueDefinitions.AddRangeAsync(valoresCor);
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();

                // ----------------------------------------
                // 4) Inserir valores de Tamanho
                // ----------------------------------------

                var tamanhos = new List<string>
                {
                    "PP", "P", "M", "G", "GG", "XG", "XXG",
                    "34", "35", "36", "37", "38", "39", "40", "41", "42", "43", "44"
                };

                var valoresTamanho = tamanhos.Select(t =>
                    new AttributeValueDefinition(tamanho.Id, t, null)
                ).ToList();

                await db.AttributeValueDefinitions.AddRangeAsync(valoresTamanho);
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();

                // ----------------------------------------
                // 5) Recarregar entidades novamente e relacionar
                // ----------------------------------------

                categorias = await db.Categories.ToListAsync();
                cor = await db.AttributeDefinitions.FirstAsync(a => a.Name == "Cor");
                tamanho = await db.AttributeDefinitions.FirstAsync(a => a.Name == "Tamanho");

                var categoriasComCorETamanho = new[]
                {
                    "Camisetas", "Calças", "Vestidos", "Casacos", "Blusas", "Polos",
                    "Blazers", "Bermudas", "Shorts", "Camisas", "Jaquetas",
                    "Croppeds", "Macacões", "Moletons", "Saias", "Suéteres",
                    "Cardigans", "Tops / Regatas", "Body"
                };

                foreach (var nome in categoriasComCorETamanho)
                {
                    var cat = Find(nome);
                    cat.AddDefaultAttribute(cor);
                    cat.AddDefaultAttribute(tamanho);
                }

                var categoriasComCor = new[]
                {
                    "Tênis", "Bolsas", "Moda Praia", "Acessórios"
                };

                foreach (var nome in categoriasComCor)
                {
                    var cat = Find(nome);
                    cat.AddDefaultAttribute(cor);
                }

                await db.SaveChangesAsync();
            }
        }