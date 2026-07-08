dotnet ef migrations add InitialCatalog \
  -p src/Modules/Catalog/Vls.Shopflow.Catalog.Infrastructure/Vls.Shopflow.Catalog.Infrastructure.csproj \
  -s ApiGateways/Vls.Shopflow.HttpApi/Vls.Shopflow.HttpApi.csproj \
  --output-dir Migrations


dotnet ef database update \
  -p src/Modules/Catalog/Vls.Shopflow.Catalog.Infrastructure/Vls.Shopflow.Catalog.Infrastructure.csproj \
  -s ApiGateways/Vls.Shopflow.HttpApi/Vls.Shopflow.HttpApi.csproj
