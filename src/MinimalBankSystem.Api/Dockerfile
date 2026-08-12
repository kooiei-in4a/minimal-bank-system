ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b

FROM ${SDK_IMAGE} AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json MinimalBankSystem.slnx ./
COPY src ./src

RUN dotnet restore src/MinimalBankSystem.Api/MinimalBankSystem.Api.csproj --verbosity minimal
RUN dotnet publish src/MinimalBankSystem.Api/MinimalBankSystem.Api.csproj --configuration Release --no-restore --output /out /p:UseAppHost=false

FROM ${RUNTIME_IMAGE} AS runtime
WORKDIR /app

COPY --from=build /out/ ./
COPY deployment/fnd05/with-database-secret.sh /usr/local/bin/with-database-secret
COPY deployment/fnd06/http-get-status.sh /usr/local/bin/http-get-status

RUN chmod 0555 /usr/local/bin/with-database-secret /usr/local/bin/http-get-status

ENTRYPOINT ["/usr/local/bin/with-database-secret"]
CMD ["dotnet", "MinimalBankSystem.Api.dll"]
