FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore OrderService.Api/OrderService.Api.csproj

RUN dotnet publish OrderService.Api/OrderService.Api.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8084

ENTRYPOINT ["dotnet", "OrderService.Api.dll"]