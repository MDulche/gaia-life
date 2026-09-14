FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY GaiaLife.sln ./
COPY src/App.Core/App.Core.csproj src/App.Core/
COPY src/App.Shared/App.Shared.csproj src/App.Shared/
COPY src/App.Modules.Finance/App.Modules.Finance.csproj src/App.Modules.Finance/
COPY src/App.Modules.Travail/App.Modules.Travail.csproj src/App.Modules.Travail/
RUN dotnet restore GaiaLife.sln

COPY src/ src/
RUN dotnet publish src/App.Core/App.Core.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "App.Core.dll"]
