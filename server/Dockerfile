# WatchParty API: ASP.NET Core 10 + SignalR, one small image.
#
#   docker build -t watchparty-api server        (from the repo root)
#
# The build context is server/. The image listens on 8080 and expects Postgres, so run it
# through deploy/docker-compose.yml rather than on its own.

# ---- 1. build and publish -------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Build settings and package versions, then only the project files, so editing source
# doesn't re-download every NuGet package.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/WatchParty.Domain/WatchParty.Domain.csproj                 src/WatchParty.Domain/
COPY src/WatchParty.Application/WatchParty.Application.csproj       src/WatchParty.Application/
COPY src/WatchParty.Infrastructure/WatchParty.Infrastructure.csproj src/WatchParty.Infrastructure/
COPY src/WatchParty.Api/WatchParty.Api.csproj                       src/WatchParty.Api/
RUN dotnet restore src/WatchParty.Api/WatchParty.Api.csproj

COPY src ./src
RUN dotnet publish src/WatchParty.Api/WatchParty.Api.csproj \
      -c Release --no-restore -o /app /p:UseAppHost=false

# ---- 2. the image that runs -----------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app ./

# The aspnet image ships an unprivileged "app" user; never run as root.
USER app
EXPOSE 8080

# Healthy when Postgres answers through the API. (The image has no curl, so bash opens the socket.)
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /health/ready HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3 && head -n1 <&3 | grep -q " 200 "'

ENTRYPOINT ["dotnet", "WatchParty.Api.dll"]
