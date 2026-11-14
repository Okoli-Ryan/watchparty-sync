# ASP.NET Core Coding Guidelines (.NET 10, C# 14, Domain-Driven Design)

Standards for ASP.NET Core Web APIs built with **Domain-Driven Design (DDD)** and Clean/Onion-style layering. Drop into a repo as `ASPNET_GUIDELINES.md` or reference it from `CLAUDE.md`.

**MUST / MUST NOT** = enforced in review or CI. **SHOULD** = default unless justified. **MAY** = optional.

Sources: Microsoft Learn *ASP.NET Core Best Practices* (async, `HttpContext` lifetime, memory, EF Core, `HttpClientFactory`); DDD practice (Evans, Vernon). Security, DDD and testing sections reflect general industry practice.

---

## 1. Principles

1. Correctness, then clarity, then speed. Optimize only with measurements.
2. **The domain model is the heart of the system.** Business rules live in the domain layer, never in endpoints, EF configurations, or DTOs.
3. Make illegal states unrepresentable (value objects, enums, private setters, non-nullable refs).
4. Validate at every trust boundary; enforce invariants inside the domain.
5. Fail loudly in dev, gracefully in prod; no swallowed exceptions.
6. Secrets never enter source control or logs.
7. If a rule can be an analyzer or test, it MUST be one (architecture tests included, see §12).
8. **Code is read far more often than it is written.** Optimize for the human reviewer: full-word names and short, step-by-step statements (see §1.1).

### 1.1 Naming and readability (MUST)

**Names are written in full words, not abbreviations.** A reviewer should never need to decode a name.

| Don't | Do |
|---|---|
| `ct` | `cancellationToken` |
| `req`, `res` | `request`, `response` |
| `ctx` | `dbContext` / `httpContext` (say which) |
| `qty` | `quantity` |
| `cust`, `usr`, `acct` | `customer`, `user`, `account` |
| `cfg`, `opts` | `configuration`, `options` |
| `msg`, `ex` | `message`, `exception` |
| `dto`, `vm` as a variable name | `orderSummary`, `orderResponse` |
| `l`, `x`, `r`, `i` in lambdas/loops | `orderLine`, `order`, `result`, `lineIndex` |
| `tmp`, `data`, `info`, `obj` | a name describing the content |

- Allowed exceptions: universally understood terms (`Id`, `Url`, `Http`, `Json`, `Api`, `Utc`) and conventional generic parameters (`T`, `TKey`, `TResult`). Spell out anything else.
- Name booleans as questions (`isShipped`, `hasDiscount`, `canCancel`); name methods with verbs (`PlaceOrder`, `CalculateTotal`); name collections in the plural (`orderLines`).
- Don't encode types in names (`strName`, `lstOrders`). Avoid vague suffixes (`Manager`, `Helper`, `Util`, `Processor`).
- Longer is better than cryptic: `customerBillingAddress` beats `addr`.

**Code is broken into small, reviewable steps.**

- **One statement, one idea.** MUST NOT chain several operations, ternaries, lambdas and null-checks into one long line. If a line needs horizontal scrolling or more than one "and then", split it.
- Introduce **named intermediate variables** to explain each step (`var isEligibleForDiscount = ...`), instead of nesting calls.
- Keep lines at roughly **100 characters** or fewer; put each argument or LINQ step on its own line when wrapping.
- Prefer **guard clauses and early returns** over deep nesting. Maximum nesting depth of about 3.
- Break long LINQ chains into named steps, one operation per line.
- Extract a well-named method when a block needs a comment to explain what it does.
- Snippets in this document and in the codebase follow the same rule, so they can be read top to bottom in a review.

```csharp
// Hard to review: everything in one line, abbreviated names
var t = o.Lines.Where(l => l.Qty > 0 && !l.IsGift).Select(l => l.Price * l.Qty).Sum() * (c.IsVip ? 0.9m : 1m);

// Easy to review: one idea per line, full names
var payableOrderLines = order.Lines
    .Where(orderLine => orderLine.Quantity > 0)
    .Where(orderLine => !orderLine.IsGift);

var subtotal = payableOrderLines
    .Select(orderLine => orderLine.UnitPrice * orderLine.Quantity)
    .Sum();

var discountMultiplier = customer.IsVip ? 0.9m : 1m;
var totalPrice = subtotal * discountMultiplier;
```

---

## 2. Domain-Driven Design architecture

### 2.1 Layers and dependency rule

```
src/
├─ Company.Product.Domain/           # Pure C#. NO NuGet dependencies (except tiny abstractions)
│   ├─ <BoundedContext>/ (e.g. Ordering/)
│   │   ├─ Aggregates/Order/         # Order (root), OrderLine (entity), OrderStatus
│   │   ├─ ValueObjects/             # Money, Address, Sku
│   │   ├─ Events/                   # OrderPlaced, OrderCancelled
│   │   ├─ Services/                 # Domain services (stateless, cross-aggregate rules)
│   │   ├─ Repositories/             # IOrderRepository (interfaces only)
│   │   └─ Specifications/
│   └─ SharedKernel/                 # Entity, AggregateRoot, ValueObject, IDomainEvent, Result
├─ Company.Product.Application/      # Use cases: commands, queries, handlers, DTOs, validators
│   └─ Ordering/ (Commands/, Queries/, EventHandlers/, Abstractions/)
├─ Company.Product.Infrastructure/   # EF Core, repositories impl, external clients, messaging
│   └─ Persistence/ (DbContext, Configurations/, Migrations/, Repositories/, Outbox/)
├─ Company.Product.Api/              # ASP.NET Core host: endpoints, DI, middleware, auth
└─ tests/
   ├─ Domain.UnitTests/
   ├─ Application.UnitTests/
   ├─ Api.IntegrationTests/          # WebApplicationFactory + Testcontainers
   └─ Architecture.Tests/            # NetArchTest / ArchUnitNET
```

**Dependency rule (enforced by architecture tests):**

```
Api ──► Application ──► Domain
Infrastructure ──► Application, Domain
Domain ──► (nothing)
```

- **Domain** MUST NOT reference EF Core, ASP.NET Core, MediatR, serializers, or any framework. Only the BCL.
- **Application** references Domain only; it defines interfaces (ports) that Infrastructure implements.
- **Api** is a thin composition root and adapter; it MUST NOT contain business logic or direct `DbContext` queries.
- Cross-bounded-context communication goes through integration events or an explicit contract, never by referencing another context's domain types.

### 2.2 Strategic design

- Identify **bounded contexts** and a **ubiquitous language** with domain experts; class and method names MUST use the business vocabulary (`Order.Place()`, not `OrderService.UpdateStatus(3)`).
- Start as a **modular monolith**: one deployable, one module per bounded context with its own schema/namespace and public contract. Split into services only when there's a proven scaling or team-ownership reason.
- Document context relationships (customer/supplier, anti-corruption layer, shared kernel) in `docs/context-map.md`. Use an **anti-corruption layer** when integrating with legacy or third-party models.
- Keep the shared kernel tiny. Anything shared between contexts is a coupling cost.

### 2.3 Tactical design: building blocks

**Entities**
- Identity-based equality. Strongly-typed IDs (`readonly record struct OrderId(Guid Value)`) to prevent mixing IDs.
- State is changed only via **behavior methods** that enforce invariants. No public setters; no anemic models.
- Parameterless/private constructors only for EF; expose **factory methods** (`Order.Place(...)`) that validate.

**Value objects**
- Immutable, equality by value, self-validating, no identity: `Money`, `EmailAddress`, `Address`, `DateRange`.
- Implement as `record`/`readonly record struct` with a validating factory. Avoid primitive obsession.

**Aggregates**
- An aggregate is a **consistency and transaction boundary** with a single **aggregate root**; external code references only the root.
- Keep aggregates **small**. Reference other aggregates **by ID**, never by object reference.
- One aggregate modified per transaction; use domain events and eventual consistency across aggregates.
- Child entities are accessed via the root (`IReadOnlyCollection<OrderLine> Lines`); expose no mutable collections.
- Use optimistic concurrency (`rowversion`) on the root.

**Domain events**
- Past-tense, immutable records raised by the aggregate (`OrderPlaced`). Aggregate collects them; infrastructure dispatches **after** successful persistence (or via the outbox, see §2.6).
- Domain events (in-process, same context) differ from **integration events** (cross-context/service, versioned, serialized). Don't leak domain events across boundaries.

**Domain services**
- Only for domain logic that doesn't naturally belong to one entity/value object. Stateless; depend on domain abstractions only.

**Repositories**
- One repository **per aggregate root**, interface in Domain, implementation in Infrastructure. Methods speak the domain language (`GetByIdAsync`, `Add`), return full aggregates, never `IQueryable`.
- Repositories don't call `SaveChanges`; the **Unit of Work** (the `DbContext`) is committed by the application handler / pipeline behavior.
- Read/query needs go through separate **query services** (see CQRS), not repositories.

**Specifications** (optional): encapsulate reusable query criteria in the domain; translate in Infrastructure.

```csharp
// Domain: aggregate root with invariants, behavior, and events
public sealed class Order : AggregateRoot<OrderId>
{
    private readonly List<OrderLine> _lines = [];
    private Order() { }                                   // EF

    public CustomerId CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyCollection<OrderLine> Lines => _lines;
    public Money Total
    {
        get
        {
            var total = Money.Zero;

            foreach (var orderLine in _lines)
            {
                total = total + orderLine.Subtotal;
            }

            return total;
        }
    }

    public static Result<Order> Place(
        CustomerId customerId,
        IEnumerable<OrderLineRequest> requestedLines,
        TimeProvider timeProvider)
    {
        var order = new Order
        {
            Id = OrderId.New(),
            CustomerId = customerId,
            Status = OrderStatus.Placed
        };

        foreach (var requestedLine in requestedLines)
        {
            var addLineResult = order.AddLine(
                requestedLine.Sku,
                requestedLine.Quantity,
                requestedLine.UnitPrice);

            if (addLineResult.IsFailure)
            {
                return addLineResult.Error;
            }
        }

        if (order._lines.Count == 0)
        {
            return OrderErrors.MustContainAtLeastOneLine;
        }

        var placedAtUtc = timeProvider.GetUtcNow();
        order.Raise(new OrderPlaced(order.Id, placedAtUtc));

        return order;
    }

    public Result Cancel()
    {
        if (Status is OrderStatus.Shipped)
        {
            return OrderErrors.AlreadyShipped;
        }

        Status = OrderStatus.Cancelled;
        Raise(new OrderCancelled(Id));

        return Result.Success();
    }
}
```

### 2.4 Application layer: use cases (CQRS)

- Organize by **feature/use case**, not by technical type: `Ordering/Commands/PlaceOrder/{PlaceOrderCommand, PlaceOrderHandler, PlaceOrderValidator}.cs`.
- **Commands** change state and go through the domain (load aggregate → call behavior → save). **Queries** return DTOs and bypass the domain model, reading with EF `AsNoTracking().Select(...)` projections or Dapper for read models.
- A handler orchestrates: load, call domain, persist, publish. It contains **no business rules**.
- Cross-cutting concerns (validation, logging, transaction/unit of work, authorization) live in **pipeline behaviors/decorators**, not repeated in handlers.
- Mediator library (MediatR/Mediator source generator/Wolverine) is optional; plain handler interfaces (`ICommandHandler<TCommand, TResult>`) are fine. Pick one and be consistent. Note: some mediator libraries are commercially licensed, so check the license.
- Commands return `Result`/`Result<T>` (or the new aggregate ID), not entities. Expected failures (not found, rule violated, conflict) are values, not exceptions.
- **Input validation** (shape, required, ranges) happens in the Application layer validator (FluentValidation/DataAnnotations); **business invariants** happen in the Domain.
- DTOs/requests/responses are separate from domain types. Map explicitly (hand-written or source-generated like Mapperly). Avoid reflection-heavy auto-mapping for domain objects.

### 2.5 Persistence (Infrastructure, EF Core)

- Map the domain model with **`IEntityTypeConfiguration<T>` Fluent API** in Infrastructure. No EF attributes in Domain.
- Configure private fields/backing fields (`UsePropertyAccessMode`), value objects as **owned types** or **value converters**, strongly-typed IDs via converters, and enums as strings.
- One `DbContext` per bounded context (own schema); register `Scoped` (pooled where measured).
- Load whole aggregates for commands (with the `Include`s it needs). For queries use projections: never load aggregates just to display data.
- MUST: `AsNoTracking()` for read-only; async + `CancellationToken` everywhere; filters/aggregation in the DB; no N+1; always paginate; parameterized raw SQL only.
- Migrations committed and reviewed, applied by a deployment step, **not** `Migrate()` on startup in multi-instance prod.
- Use **optimistic concurrency** tokens; handle `DbUpdateConcurrencyException` → `409 Conflict`.

### 2.6 Domain events and the Outbox

- Aggregates raise events; `SaveChangesAsync` (override or interceptor) collects them from tracked aggregates.
- **In-process handlers** (same transaction) for same-aggregate-consistent side effects; **eventual-consistency handlers** run after commit.
- For reliable cross-context/external messaging use the **Transactional Outbox**: write the integration event to an `OutboxMessages` table in the same transaction, a `BackgroundService` publishes it (Service Bus/RabbitMQ/Kafka), consumers are **idempotent** (inbox/dedupe by message ID).
- Never call an external system inside an aggregate or inside the DB transaction.

---

## 3. API layer (ASP.NET Core)

- Prefer **Minimal APIs** with `MapGroup` and one endpoint class per feature (or controllers if the codebase already uses them; do not mix). Handlers are thin: bind → send command/query → map `Result` to HTTP.
- Resource-oriented plural lowercase routes: `POST /api/v1/orders`, `GET /api/v1/orders/{id}`.
- Correct status codes: `201 + Location`, `204`, `400/422` validation, `401/403`, `404`, `409` conflict. Never `200` with an error body.
- Use `TypedResults` / `Results<...>` for compile-time-checked responses and accurate OpenAPI.
- **Version the API** (`Asp.Versioning`). Publish OpenAPI (`Microsoft.AspNetCore.OpenApi`) and generate typed clients from it; run an OpenAPI diff in CI to catch breaking changes.
- **Never expose domain entities or EF types.** Request/response DTOs only (records). Prevent over-posting.
- Paginate all collections with a server-enforced max page size.
- Map domain `Error`s to `ProblemDetails` in one place (an `IResult` extension), not ad hoc in each endpoint.

```csharp
group.MapPost("/", PlaceOrderAsync);

static async Task<Results<Created, ProblemHttpResult>> PlaceOrderAsync(
    PlaceOrderRequest request,
    ISender sender,
    CancellationToken cancellationToken)
{
    var command = request.ToCommand();

    var result = await sender.Send(command, cancellationToken);

    if (result.IsFailure)
    {
        return result.Error.ToProblem();
    }

    var newOrderId = result.Value;
    var orderLocation = $"/api/v1/orders/{newOrderId}";

    return TypedResults.Created(orderLocation);
}
```

### 3.1 Errors and Problem Details

- `AddProblemDetails()` + a global `IExceptionHandler`; responses are RFC 9457 `application/problem+json` including a `traceId`.
- MUST NOT leak stack traces or internals in production.
- Domain/application errors are typed values (`Error(Code, Description, Type)`) mapped to status codes; exceptions are for unexpected faults only. MUST NOT use exceptions for control flow; MUST NOT write empty `catch`.

---

## 4. Async, threading, and request lifetime

(From Microsoft's ASP.NET Core best practices.)

- Async end to end for I/O. MUST NOT use `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`.
- MUST NOT use `Task.Run` to fake async, nor `async void` (except event handlers).
- Pass `CancellationToken` through every layer (endpoint → handler → repository → EF/HttpClient).
- Use `ReadFormAsync` and stream bodies; never buffer large request/response bodies into one `string`/`byte[]` (LOH, DoS risk). Enforce body size limits; don't assume `ContentLength` is non-null.
- Don't store `HttpContext` in fields, use it across threads, or capture it or scoped services (`DbContext`) in background work. Copy the needed values; use `IServiceScopeFactory` for a new scope.
- Long-running/fire-and-forget work → `BackgroundService`, a channel, or a message broker, not the request pipeline.
- Don't modify status/headers after the response has started (`Response.OnStarting` / `HasStarted`).
- Keep middleware fast; no long-running work.

---

## 5. Dependency injection and configuration

- Constructor injection only; no service locator in business code. Domain objects never take services via DI (pass what they need as method arguments, e.g. `TimeProvider`).
- Lifetimes: `Scoped` for `DbContext`/repositories/handlers; `Singleton` only for stateless, thread-safe; no singleton → scoped dependency. Enable `ValidateScopes` and `ValidateOnBuild`.
- Register per layer/module with extension methods: `AddDomain()`, `AddApplication()`, `AddInfrastructure(config)`, `AddOrderingModule()`. Keep `Program.cs` short.
- `IHttpClientFactory` with typed clients; add `Microsoft.Extensions.Http.Resilience` (retry, timeout, circuit breaker). Never `new HttpClient()` per call.
- Options pattern with `ValidateDataAnnotations().ValidateOnStart()`. Inject `TimeProvider` instead of `DateTime.UtcNow`.

---

## 6. Security

- HTTPS everywhere (`UseHttpsRedirection`, `UseHsts`).
- **Authentication:** OIDC/OAuth 2.0 via a proven IdP or ASP.NET Core Identity; JWT validation MUST check issuer, audience, lifetime, signing key. Never roll your own crypto/password storage.
- **Authorization:** deny by default (`FallbackPolicy`); policy-based and **resource-based** checks (does this user own this order?). Authorization is enforced server-side at the Application layer or endpoint, never just in the UI.
- CSRF protection for cookie auth (antiforgery, `SameSite`, `HttpOnly`, `Secure`).
- **CORS:** explicit origin allow-list; never `AllowAnyOrigin` with credentials.
- **Rate limiting** (`AddRateLimiter`) on auth and expensive endpoints. Security headers (CSP, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`).
- Secrets: user-secrets in dev; Key Vault/secret manager in prod. Persist Data Protection keys when scaled out.
- Validate uploads (size, content type by content, randomized names, stored outside web root).
- No passwords/tokens/PII in logs. Run `dotnet list package --vulnerable` and CodeQL in CI. Follow OWASP Top 10 / ASVS.

Pipeline order: `ExceptionHandler → HSTS → HttpsRedirection → StaticFiles → Routing → CORS → RateLimiter → Authentication → Authorization → endpoints`.

---

## 7. Performance

- Cache deliberately (`HybridCache` / `IMemoryCache` / output caching) with explicit expiry and invalidation.
- Response compression; `System.Text.Json` with source generation for hot DTOs; `IAsyncEnumerable<T>` or paging for large results.
- Avoid large-object allocations in hot paths (`ArrayPool<T>`, `Span<T>`, streaming).
- Measure with `dotnet-counters`, `dotnet-trace`, BenchmarkDotNet, OpenTelemetry. Optimize hot paths only. Use DbContext pooling and compiled queries only after measuring.

---

## 8. Logging and observability

- Structured logging: `logger.LogInformation("Order {OrderId} placed", id)`, never interpolated templates. `LoggerMessage` source generators on hot paths.
- **OpenTelemetry** traces/metrics/logs (ASP.NET Core, EF Core, HttpClient instrumentation). Return `traceId` in problem responses.
- Health checks: `/health/live`, `/health/ready`.
- Log levels: `Error` needs attention; `Warning` recoverable; `Information` business events (domain events are great log points); `Debug`/`Trace` off in prod.

---

## 9. C# style and conventions

- `Directory.Build.props`: `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<AnalysisLevel>latest-recommended</AnalysisLevel>`, `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`. Central package management (`Directory.Packages.props`); pin SDK in `global.json`.
- File-scoped namespaces; `record` for DTOs/value objects; `required` members; primary constructors where they read well.
- Naming: `PascalCase` types/methods/properties, `camelCase` locals/params, `_camelCase` private fields, `IPascalCase` interfaces, `Async` suffix, `CancellationToken cancellationToken` last. Full-word names and no long one-liners (see §1.1).
- One top-level type per file. Methods under ~30 lines. No `#region`. Prefer `sealed` classes by default.
- Domain naming reflects the ubiquitous language; avoid `Manager`, `Helper`, `Util`, `Data`, `Info` suffixes.

---

## 10. Testing

| Level | Scope | Tools |
|---|---|---|
| **Domain unit tests** (most numerous) | Aggregates, value objects, domain services; pure, no mocks, no DB | xUnit, FluentAssertions/Shouldly |
| **Application tests** | Handlers with in-memory fakes of repositories/ports | xUnit, NSubstitute (sparingly) |
| **Integration tests** | Full pipeline via `WebApplicationFactory<Program>` against a real DB | Testcontainers, Respawn |
| **Architecture tests** | Enforce layer/dependency rules | NetArchTest / ArchUnitNET |
| **E2E** (few) | Critical journeys | Playwright |

- Test behavior and invariants ("cancelling a shipped order fails"), not implementation.
- Naming: `Method_Scenario_ExpectedResult`; Arrange-Act-Assert.
- Deterministic: inject `TimeProvider`/ID generators; no `Thread.Sleep`.
- Don't mock `DbContext`; use a real database in integration tests.
- Use test data builders/object mothers for aggregates.
- Always test authorization rules, validation, and concurrency conflicts.

---

## 11. CI/CD quality gates

Every PR MUST pass: `dotnet format --verify-no-changes`, analyzers with warnings as errors, `dotnet build`, all tests (including architecture tests), `dotnet list package --vulnerable`, secret scan (gitleaks), CodeQL, OpenAPI breaking-change diff.

- Conventional Commits; short-lived branches; ≥1 reviewer; main always deployable.
- Multi-stage Docker builds, non-root user, config via environment variables.
- DB migrations as a separate reviewed deploy step; health-check-gated rollouts and rollback plan.

---

## 12. Architecture tests (examples to enforce)

- Domain assembly references no other project and no framework packages.
- Application doesn't reference Infrastructure or Api.
- Infrastructure/Api types aren't referenced by Domain/Application.
- Entities/aggregates have no public setters; value objects are immutable and `sealed`.
- Repository interfaces exist only for aggregate roots.
- Handlers end with `Handler`; commands/queries are `sealed record`.
- One bounded context doesn't reference another's internals.

---

## 13. Code review checklist

**Readability**
- [ ] Full-word names (no `ct`, `req`, `qty`, single-letter lambdas)?
- [ ] No long one-liners; steps broken into named variables and short lines?

**Domain/DDD**
- [ ] Is the business rule in the aggregate/value object (not handler/endpoint)?
- [ ] Is the aggregate small, referencing others by ID, one aggregate per transaction?
- [ ] Are invariants enforced in constructors/factories/behavior methods; no public setters?
- [ ] Do names match the ubiquitous language?
- [ ] Are domain events raised for significant state changes, with outbox for cross-boundary messaging?

**Application/API**
- [ ] Handler orchestrates only; validation in validators; failures returned as `Result`?
- [ ] DTOs used; no entities exposed; status codes and Problem Details correct?
- [ ] Queries use projections/`AsNoTracking`/pagination; no N+1?

**Platform**
- [ ] Async end-to-end with `CancellationToken`; no `.Result`/`async void`?
- [ ] AuthN/AuthZ enforced server-side (including resource ownership)? No secrets/PII in logs?
- [ ] Migrations included and reviewed; tests meaningful and deterministic?

---

## 14. Anti-patterns

| Don't | Do instead |
|---|---|
| Anemic domain (entities as bags of getters/setters, logic in services) | Behavior on aggregates and value objects |
| Business logic in endpoints/handlers/EF configs | Domain layer |
| Domain referencing EF Core/ASP.NET | Interfaces in Domain, implementations in Infrastructure |
| Giant aggregates / object references between aggregates | Small aggregates, reference by ID |
| Repository returning `IQueryable` | Aggregate-returning methods + separate query services |
| Modifying several aggregates in one transaction | Domain events + eventual consistency |
| Publishing to a broker inside the DB transaction | Transactional outbox |
| Returning EF entities / domain objects from API | Response DTO records |
| `.Result` / `.Wait()` / `async void` | `await` all the way, return `Task` |
| `new HttpClient()` per call | `IHttpClientFactory` |
| Exceptions for expected failures | `Result<T>` with typed errors |
| `Database.Migrate()` at startup in multi-instance prod | Pipeline migration step |
| Capturing `HttpContext`/`DbContext` in background tasks | Copy values; new DI scope |
| Primitive obsession (`string email`, `decimal price`) | Value objects (`EmailAddress`, `Money`) |
| Abbreviated names (`ct`, `req`, `qty`, `l`) | Full words (`cancellationToken`, `request`, `quantity`, `orderLine`) |
| Long chained one-liners | Named intermediate variables, one step per line |
| Mocking `DbContext` | Real DB via Testcontainers |

---

## 15. Definition of done

Builds with zero warnings; domain, application, integration and architecture tests pass; lint/format/security scans pass; OpenAPI and generated clients updated; logs/metrics/traces added for new flows; docs and context map updated; peer-reviewed via CI-green PR.

---

## References

- Microsoft Learn: [ASP.NET Core Best Practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices); [Enterprise web app patterns](https://learn.microsoft.com/en-us/azure/architecture/web-apps/guides/enterprise-app-patterns/overview); [Microservices DDD guidance](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/)
- Eric Evans, *Domain-Driven Design*; Vaughn Vernon, *Implementing Domain-Driven Design*
- OWASP [Top 10](https://owasp.org/www-project-top-ten/), [ASVS](https://owasp.org/www-project-application-security-verification-standard/); RFC 9457 (Problem Details)

*Review quarterly and after each major .NET release. Change via PR.*
