# syntax=docker/dockerfile:1
# FND-05 D-02: digest-qualified base images only. Do not re-resolve the mutable tag as authority.
# Build context is the repository root (see compose.yaml `build.context: .`).

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json MinimalBankSystem.slnx ./
COPY src/MinimalBankSystem.Domain/MinimalBankSystem.Domain.csproj src/MinimalBankSystem.Domain/
COPY src/MinimalBankSystem.Application/MinimalBankSystem.Application.csproj src/MinimalBankSystem.Application/
COPY src/MinimalBankSystem.Infrastructure/MinimalBankSystem.Infrastructure.csproj src/MinimalBankSystem.Infrastructure/
COPY src/MinimalBankSystem.Api/MinimalBankSystem.Api.csproj src/MinimalBankSystem.Api/
RUN dotnet restore src/MinimalBankSystem.Api/MinimalBankSystem.Api.csproj

COPY src/MinimalBankSystem.Domain/ src/MinimalBankSystem.Domain/
COPY src/MinimalBankSystem.Application/ src/MinimalBankSystem.Application/
COPY src/MinimalBankSystem.Infrastructure/ src/MinimalBankSystem.Infrastructure/
COPY src/MinimalBankSystem.Api/ src/MinimalBankSystem.Api/

RUN dotnet publish src/MinimalBankSystem.Api/MinimalBankSystem.Api.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b AS runtime
WORKDIR /app

COPY --from=build /app ./
COPY --chmod=0755 docker/entrypoint-with-secret.sh /usr/local/bin/entrypoint-with-secret.sh

# Normal API startup never evolves the schema (ADR-0009); this image only ever runs the API host.
ENTRYPOINT ["/usr/local/bin/entrypoint-with-secret.sh", "dotnet", "MinimalBankSystem.Api.dll"]
