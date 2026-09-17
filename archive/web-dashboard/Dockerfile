FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY GaiaLife.sln ./
COPY src/App.Core/App.Core.csproj src/App.Core/
COPY src/App.Shared/App.Shared.csproj src/App.Shared/
COPY src/App.Modules.Finance/App.Modules.Finance.csproj src/App.Modules.Finance/
COPY src/App.Modules.Travail/App.Modules.Travail.csproj src/App.Modules.Travail/
COPY src/App.Modules.Course/App.Modules.Course.csproj src/App.Modules.Course/
COPY src/App.Modules.Stock/App.Modules.Stock.csproj src/App.Modules.Stock/
COPY tests/App.Modules.Stock.Tests/App.Modules.Stock.Tests.csproj tests/App.Modules.Stock.Tests/
COPY tests/App.Modules.Travail.Tests/App.Modules.Travail.Tests.csproj tests/App.Modules.Travail.Tests/
RUN dotnet restore GaiaLife.sln

COPY src/ src/
COPY tests/ tests/
RUN dotnet publish src/App.Core/App.Core.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# L'image aspnet fournit déjà l'utilisateur non-root « app » (APP_UID).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /logs /backups /.dataprotection-keys

COPY --from=build /app/publish .
RUN chown -R app:app /app /logs /backups /.dataprotection-keys

USER app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
HEALTHCHECK --interval=10s --timeout=5s --start-period=40s --retries=12 \
    CMD curl -f http://127.0.0.1:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "App.Core.dll"]
