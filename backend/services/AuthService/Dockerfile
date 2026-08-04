FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore AuthService.Api/AuthService.Api.csproj

RUN dotnet publish AuthService.Api/AuthService.Api.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8081

ENTRYPOINT ["dotnet", "AuthService.Api.dll"]