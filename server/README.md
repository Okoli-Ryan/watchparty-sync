# WatchParty API

The backend for the WatchParty extension and web dashboard: an ASP.NET Core 10 API with
a SignalR hub for realtime updates, and Postgres through EF Core. Production runs on
[Neon](https://neon.tech). Coding standards: [ASPNET_GUIDELINES.md](ASPNET_GUIDELINES.md).

```
src/WatchParty.Domain          aggregates, value objects, domain events (no dependencies)
src/WatchParty.Application     use cases, validators, ports, DTOs
src/WatchParty.Infrastructure  EF Core DbContext, migrations, repositories, JWT, hashing
src/WatchParty.Api             endpoints, SignalR hub, auth, rate limiting, health checks
tests/                         domain, application, integration (real Postgres), architecture
```

## Run it locally

Needs the .NET 10 SDK and Docker.

```bash
docker compose up -d
```

```bash
dotnet user-secrets set "Jwt:SigningKey" "<at least 32 random characters>" --project src/WatchParty.Api
```

```bash
dotnet run --project src/WatchParty.Api
```

The API listens on `http://localhost:5080` and applies migrations on startup in
Development only. Create the first administrator (the password is prompted for, or read
from `WATCHPARTY_ADMIN_PASSWORD`):

```bash
dotnet run --project src/WatchParty.Api -- seed-admin --email you@example.com --name "You"
```

## Tests

```bash
dotnet test
```

The integration tests start a throwaway `postgres:17-alpine` container with
Testcontainers, so Docker must be running. Each test class gets its own database, built
by the same EF migrations production uses.

## Deploy with Neon

1. **Create a Neon project** (Postgres 17). From its dashboard, copy two connection
   strings in the .NET format:
   - the **pooled** one (host contains `-pooler`), which the running API uses;
   - the **direct** one, used only to apply migrations.

2. **Apply migrations** as a deploy step, against the direct connection:

   ```bash
   WATCHPARTY_MIGRATIONS_CONNECTION="<direct connection string>" dotnet ef database update --project src/WatchParty.Infrastructure --startup-project src/WatchParty.Api
   ```

   For CI/CD, `dotnet ef migrations bundle` produces a single executable that does the
   same. The running API never migrates production by itself.

3. **Configure the API** with environment variables (never in `appsettings.json`):

   | Variable | Value |
   |---|---|
   | `ConnectionStrings__WatchParty` | the pooled Neon connection string, with `SSL Mode=Require` |
   | `Jwt__SigningKey` | at least 32 random characters |
   | `Cors__AllowedOrigins__0` | the web dashboard's origin, e.g. `https://watchparty.example.com` |
   | `Cors__AllowedOrigins__1` | the extension's origin, `chrome-extension://<extension id>` |

4. **Check it**: `GET /health/ready` returns 200 once the API can reach Neon.

Neon suspends idle computes. The first query after a pause takes longer, and the Npgsql
provider retries transient failures (`Database:MaxRetryCount`, default 3).

## Changing the schema

Change the domain model or an `IEntityTypeConfiguration`, then:

```bash
dotnet ef migrations add <Name> --project src/WatchParty.Infrastructure --startup-project src/WatchParty.Api --output-dir Persistence/Migrations
```

Review the generated migration and commit it with the change.

## Single instance

Realtime fan-out (`RealtimeQueue` → `RealtimeBroadcaster` → SignalR groups) runs in
memory, so run one API instance. Scaling out needs a SignalR backplane (Redis, or Azure
SignalR Service). The database side already supports several instances: concurrency is
guarded by each aggregate's `version` column.
