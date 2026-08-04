FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore CatalogService.Api/CatalogService.Api.csproj

RUN dotnet publish CatalogService.Api/CatalogService.Api.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8082

ENTRYPOINT ["dotnet", "CatalogService.Api.dll"]