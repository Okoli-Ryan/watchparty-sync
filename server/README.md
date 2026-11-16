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

## How a request flows

Follow one action end to end: the host presses pause in the extension.

```
PUT /api/v1/rooms/{id}/playback
  │
  ▼  Api            RoomEndpoints.RecordPlaybackAsync
  │                 binds the request, takes the user id from the JWT, builds a command
  ▼  Application    ValidationDecorator → RecordPlaybackHandler
  │                 validates the command, then RoomEditor loads the Room (retrying on a lost race)
  ▼  Domain         Room.RecordPlayback
  │                 checks the caller is the host, updates state, raises PlaybackRecorded
  ▼  Infrastructure UnitOfWork.CommitAsync
  │                 saves in one transaction (version-checked), then dispatches the raised events
  ▼  Application    RealtimeNotifications.HandleAsync(PlaybackRecorded)
  │                 turns the event into notifier.RoomChanged(roomId)
  ▼  Api            RealtimeQueue → RealtimeBroadcaster → RealtimeHub
                    reads the room once and pushes "room" to everyone subscribed
```

The HTTP response (`204` or a problem) is produced as soon as the commit succeeds. The
push to viewers happens afterwards, in the background, and never blocks the request.

Things that explain most of the code:

- **Errors are values.** Handlers and domain methods return `Result` or `Result<T>`;
  `ResultHttpExtensions` maps an `Error` to the HTTP status. Exceptions mean bugs.
- **One aggregate per commit.** Every aggregate carries a version. A commit that loses a
  race comes back as `ConcurrencyErrors.Conflict`, and handlers that are safe to redo
  re-run through `ConcurrencyRetry`.
- **Domain events carry the side effects.** An aggregate only raises an event. Reacting
  to it (pushing to clients, recording attendance, syncing roles) is an
  `IDomainEventHandler<TEvent>` in Application, registered automatically from the
  interfaces a class implements.
- **Ownership has two claims.** `Room.OwnerId` is who hosts right now and moves on its
  own when the host goes quiet; `PrimaryOwnerId` is who the room belongs to and only
  moves on a deliberate transfer. `RoomSuccession` applies the handoff after every join,
  heartbeat and leave.

Where things live:

| To change…                          | Look in                                             |
| ----------------------------------- | --------------------------------------------------- |
| a route or its request body         | `Api/Endpoints`, `Api/Contracts`                    |
| what a use case does                | `Application/<Area>/Commands/<UseCase>`: command, validator, handler |
| a business rule                     | `Domain/Rooms`, `Domain/History`, `Domain/Identity` |
| what gets pushed to clients         | `Application/Realtime/RealtimeNotifications`, `Api/Realtime` |
| a table, index or query             | `Infrastructure/Persistence` (`Configurations`, `Queries`, `Migrations`) |
| rate limits, auth, CORS             | `Api/Hosting/ApiServiceCollectionExtensions`        |

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
Development only. There is no seeding step: anyone can create an account with
`POST /api/v1/auth/register` (`email`, `displayName`, `password`), which returns a
token like `/auth/login` does.

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
