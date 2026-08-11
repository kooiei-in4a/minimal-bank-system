# syntax=docker/dockerfile:1
# FND-05 Migrator image — digest-qualified bases per D-02.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json MinimalBankSystem.slnx ./
COPY src/MinimalBankSystem.Domain/MinimalBankSystem.Domain.csproj src/MinimalBankSystem.Domain/
COPY src/MinimalBankSystem.Infrastructure/MinimalBankSystem.Infrastructure.csproj src/MinimalBankSystem.Infrastructure/
COPY src/MinimalBankSystem.Migrator/MinimalBankSystem.Migrator.csproj src/MinimalBankSystem.Migrator/

RUN dotnet restore src/MinimalBankSystem.Migrator/MinimalBankSystem.Migrator.csproj

COPY src/MinimalBankSystem.Domain/ src/MinimalBankSystem.Domain/
COPY src/MinimalBankSystem.Infrastructure/ src/MinimalBankSystem.Infrastructure/
COPY src/MinimalBankSystem.Migrator/ src/MinimalBankSystem.Migrator/

RUN dotnet publish src/MinimalBankSystem.Migrator/MinimalBankSystem.Migrator.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends bash \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
COPY deploy/compose/read-db-secret-and-exec.sh /usr/local/bin/read-db-secret-and-exec.sh
RUN chmod 755 /usr/local/bin/read-db-secret-and-exec.sh

ENTRYPOINT ["/usr/local/bin/read-db-secret-and-exec.sh", "dotnet", "MinimalBankSystem.Migrator.dll"]
