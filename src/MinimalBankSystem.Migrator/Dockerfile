# syntax=docker/dockerfile:1
# FND-05 D-02: digest-qualified base images only. Do not re-resolve the mutable tag as authority.
# Build context is the repository root (see compose.yaml `build.context: .`).
#
# The runtime stage intentionally reuses the D-02 locked ASP.NET runtime image rather than
# introducing an unlocked plain `dotnet/runtime` image: FND-05 does not add a fourth image
# identity to the pre-run lock, and the Migrator (a generic-host console app) runs unmodified
# on the superset ASP.NET runtime.

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
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b AS runtime
WORKDIR /app

COPY --from=build /app ./
COPY --chmod=0755 docker/entrypoint-with-secret.sh /usr/local/bin/entrypoint-with-secret.sh

# Explicit one-shot migrator (ADR-0009, Issue #42). Never starts an API host.
ENTRYPOINT ["/usr/local/bin/entrypoint-with-secret.sh", "dotnet", "MinimalBankSystem.Migrator.dll"]
