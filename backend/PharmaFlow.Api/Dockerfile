FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
ARG TARGETARCH
WORKDIR /src
COPY global.json dotnet-tools.json ./
COPY backend/PharmaFlow.Api/PharmaFlow.Api.csproj backend/PharmaFlow.Api/
RUN dotnet restore backend/PharmaFlow.Api/PharmaFlow.Api.csproj && dotnet tool restore
COPY backend/PharmaFlow.Api/ backend/PharmaFlow.Api/
COPY demo-data/catalog.json demo-data/catalog.json
RUN dotnet publish backend/PharmaFlow.Api -c Release --no-restore -o /out /p:UseAppHost=false
RUN case "$TARGETARCH" in amd64) rid=linux-x64 ;; arm64) rid=linux-arm64 ;; *) exit 1 ;; esac \
    && dotnet ef migrations bundle --project backend/PharmaFlow.Api --configuration Release --self-contained -r "$rid" -o /out/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS base
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER app

FROM base AS db-init
COPY --from=build /out/ ./
COPY docker/db-init.sh ./db-init.sh
# Explicit interpreter also handles a non-executable script on a Windows checkout.
ENTRYPOINT ["sh", "./db-init.sh"]

FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS release-build
WORKDIR /src
COPY global.json ./
COPY backend/PharmaFlow.Api/PharmaFlow.Api.csproj backend/PharmaFlow.Api/
COPY backend/PharmaFlow.DatabaseRelease/PharmaFlow.DatabaseRelease.csproj backend/PharmaFlow.DatabaseRelease/
RUN dotnet restore backend/PharmaFlow.DatabaseRelease/PharmaFlow.DatabaseRelease.csproj
COPY backend/PharmaFlow.Api/ backend/PharmaFlow.Api/
COPY backend/PharmaFlow.DatabaseRelease/ backend/PharmaFlow.DatabaseRelease/
COPY demo-data/catalog.json demo-data/catalog.json
RUN dotnet publish backend/PharmaFlow.DatabaseRelease -c Release --no-restore -o /out/release /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS database-release
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_ENVIRONMENT=Production
COPY --from=release-build /out/release/ ./
COPY --from=build /out/efbundle ./efbundle
USER app
ENTRYPOINT ["dotnet", "PharmaFlow.DatabaseRelease.dll"]

FROM base AS runtime
COPY --from=build /out/PharmaFlow.Api* ./
COPY --from=build /out/appsettings*.json ./
COPY --from=build /out/*.dll ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "PharmaFlow.Api.dll"]
