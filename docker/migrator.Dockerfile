FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0 AS build

WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props MinimalBankSystem.slnx ./
COPY src ./src

RUN dotnet restore src/MinimalBankSystem.Migrator/MinimalBankSystem.Migrator.csproj --verbosity minimal
RUN dotnet publish src/MinimalBankSystem.Migrator/MinimalBankSystem.Migrator.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    --verbosity minimal

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b AS runtime

WORKDIR /app

COPY --from=build /app/publish ./
COPY docker/read-secret-and-exec.sh /usr/local/bin/read-secret-and-exec
RUN chmod 0555 /usr/local/bin/read-secret-and-exec

ENTRYPOINT ["/usr/local/bin/read-secret-and-exec"]
