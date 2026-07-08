# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY Vls.Shopflow.sln ./
COPY ApiGateways/ ApiGateways/
COPY src/ src/
COPY Workers/ Workers/
COPY seed-assets/ seed-assets/

RUN dotnet restore ApiGateways/Vls.Shopflow.HttpApi/Vls.Shopflow.HttpApi.csproj
RUN dotnet publish ApiGateways/Vls.Shopflow.HttpApi/Vls.Shopflow.HttpApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
COPY --from=build /src/seed-assets ./seed-assets

RUN mkdir -p /app/wwwroot/uploads/seed-products /app/wwwroot/uploads/products

VOLUME ["/app/wwwroot/uploads"]

ENTRYPOINT ["dotnet", "Vls.Shopflow.HttpApi.dll"]
