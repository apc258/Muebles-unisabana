FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore InventoryService.Api/InventoryService.Api.csproj

RUN dotnet publish InventoryService.Api/InventoryService.Api.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8086

ENTRYPOINT ["dotnet", "InventoryService.Api.dll"]