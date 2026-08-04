FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore CartService.Api/CartService.Api.csproj

RUN dotnet publish CartService.Api/CartService.Api.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8083

ENTRYPOINT ["dotnet", "CartService.Api.dll"]