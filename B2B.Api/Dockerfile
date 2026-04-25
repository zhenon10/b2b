FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first for better layer caching
COPY B2B.Api/B2B.Api.csproj B2B.Api/
COPY B2B.Application/B2B.Application.csproj B2B.Application/
COPY B2B.Contracts/B2B.Contracts.csproj B2B.Contracts/
COPY B2B.Domain/B2B.Domain.csproj B2B.Domain/
COPY B2B.Infrastructure/B2B.Infrastructure.csproj B2B.Infrastructure/

RUN dotnet restore B2B.Api/B2B.Api.csproj

COPY . .
RUN dotnet publish B2B.Api/B2B.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "B2B.Api.dll"]

