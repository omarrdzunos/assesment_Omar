# Order Management System

Small order management app: a .NET Web API backend (SQL Server + EF Core)
and a React + TypeScript frontend.

See [PLAN.md](PLAN.md) for the original implementation plan.

## Stack

- Backend: .NET 10, ASP.NET Core Web API (controllers), EF Core, SQL Server.
- Frontend: React + TypeScript, Vite.
- Tests: xUnit + Shouldly + Moq (backend), Vitest + React Testing Library (frontend).

## Getting started

### Prerequisites

- .NET 10 SDK.
- A local SQL Server instance (developed against SQL Server Express,
  instance name `SQLEXPRESS`; adjust the connection string in
  `src/CustomChallenge.Api/appsettings.json` if yours differs).
- Node.js 20+.

### Backend

```bash
cd src/CustomChallenge.Api
dotnet run
```

Migrations (including seed data for Laptop/Keyboard/Mouse) are applied
automatically on startup. The API listens on `http://localhost:5075` by
default (see `Properties/launchSettings.json`).

### Frontend

```bash
cd frontend
npm install
cp .env.example .env   # only needed if the API isn't on localhost:5075
npm run dev
```

Open `http://localhost:5173`.

### Tests

```bash
# Backend (unit tests + the SQL Server integration test — needs the DB reachable)
dotnet test tests/CustomChallenge.Api.Tests

# Frontend
cd frontend && npm test
```

## Architecture Decisions

### .NET 10 instead of .NET 8

The environment this was built in only had the .NET 10 SDK/runtime
installed. .NET 10 is a drop-in superset for everything this project
uses (Minimal Hosting, `IExceptionHandler`, EF Core) — no API used here
changed between 8 and 10. Documented as a deliberate choice: "used the
LTS/latest available locally" rather than an oversight.

### `IExceptionHandler` instead of a custom exception middleware

The .NET 8+ idiom for centralized error handling is a plain
`IExceptionHandler` implementation registered via
`AddExceptionHandler<T>()` + `app.UseExceptionHandler()`, combined with
`AddProblemDetails()` for a consistent RFC 7807 shape across the whole
API (including ASP.NET Core's own 404/400 responses, not just our own
exceptions). Compared to a hand-rolled middleware wrapping the pipeline
in a try/catch:

- It's a DI-resolvable class — unit-testable by calling `TryHandleAsync`
  directly, no need to simulate `RequestDelegate`/`HttpContext` plumbing.
- Multiple handlers can be chained (first one that returns `true` wins),
  which scales better if error handling ever needs to be split by concern.

See `src/CustomChallenge.Api/ExceptionHandling/GlobalExceptionHandler.cs`.

### No generic repository over EF Core

`AppDbContext` already *is* a Unit of Work — `Products`/`Orders` are
its aggregates, and `SaveChangesAsync` is the transaction boundary. An
`IRepository<T>` on top would just forward calls to `DbSet<T>` with
extra indirection and no real abstraction gained (we're not swapping
persistence technology). `OrderService`/`ProductService` use the
`DbContext` directly.

### Concurrency: app-managed `Product.Version`, not a SQL Server `rowversion` column

Order creation must (a) be atomic — order + stock update land in one
`SaveChangesAsync`, which EF Core wraps in an implicit transaction — and
(b) survive two concurrent requests racing to buy the last unit of the
same product.

The first instinct is a SQL Server `rowversion` column
(`.IsRowVersion()`), auto-updated by the database engine on every write.
That ties the concurrency check to a SQL-Server-specific column type,
which doesn't translate to the SQLite database used in the fast unit
tests (SQLite has no equivalent auto-incrementing binary column) —
tests would either not exercise real concurrency behavior or fail on
schema creation.

Instead, `Product.Version` is a plain `int`, marked `.IsConcurrencyToken()`,
incremented explicitly in `AppDbContext.SaveChanges`/`SaveChangesAsync`
for every modified `Product`. EF Core still generates the same
`WHERE Version = @original` clause and still throws
`DbUpdateConcurrencyException` on a conflicting concurrent write — the
mechanism is identical, just provider-agnostic. That exception is
caught in `OrderService` and re-thrown as `StockConflictException`,
mapped to `409 Conflict`.

### Validation split: `DataAnnotations` for shape, domain exceptions for business rules

`CreateOrderRequest` uses `DataAnnotations` (`[Required]`, `[Range]`) for
request *shape* (missing fields, non-positive quantities) — enforced
automatically by `[ApiController]` model binding, returning 400 before
the request even reaches the service. Business rules that need to
consult the database (product exists / is active / has enough stock)
live in `OrderService` and throw specific exceptions
(`ProductNotFoundException`, `ProductInactiveException`,
`InsufficientStockException`), mapped to the right status code by
`GlobalExceptionHandler`. Keeping these separate avoids stuffing
database lookups into attribute-based validation, which doesn't have
async/DI access anyway.

### Frontend: no Redux, no React Query

The assignment explicitly doesn't require them, and there's nothing
here that would justify the complexity: a handful of GET endpoints with
no shared cache invalidation across components, no optimistic updates,
no background refetching. `useApi` (`src/hooks/useApi.ts`) is a ~30-line
hook giving every page `{ data, loading, error, refetch }` — enough to
implement the required loading/error states without a state-management
dependency to configure and explain later.

### Shouldly instead of FluentAssertions

FluentAssertions v8+ moved to a commercial license (paid for most
company use, with some individual/OSS carve-outs). To sidestep any
licensing ambiguity in a professional deliverable, backend tests use
**Shouldly** (MIT) instead — same fluent-assertion ergonomics, no
license conversation needed.

### Testing strategy

- Unit tests run against a **real SQLite database** (in-memory, but
  actual SQL execution and constraints), not the EF `InMemory` provider
  — closer to production behavior (e.g. actual `NOT NULL`/unique
  constraint enforcement) for the same speed.
- Exactly **one** integration test
  (`tests/CustomChallenge.Api.Tests/OrderApiIntegrationTests.cs`) drives
  the full stack — HTTP request through `WebApplicationFactory`,
  through the real controller/service, through EF Core, into the local
  SQL Server instance — as required by the assignment. It points at a
  dedicated `CustomChallengeOmsIntegrationTests` database, dropped
  before and after the run so it never leaks state between runs.
