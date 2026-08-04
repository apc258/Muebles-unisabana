FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore PaymentService.Api/PaymentService.Api.csproj

RUN dotnet publish PaymentService.Api/PaymentService.Api.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8085

ENTRYPOINT ["dotnet", "PaymentService.Api.dll"]