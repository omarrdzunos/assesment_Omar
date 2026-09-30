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

### Concurrency: atomic conditional `ExecuteUpdateAsync`, not a concurrency token

Order creation must (a) be atomic — stock updates and the order insert
either all land or none do — and (b) survive two concurrent requests
racing to buy the last unit of the same product.

An earlier version of this solved (b) with an optimistic concurrency
token (`Product.Version`): read stock, decide in application code,
write with `WHERE Version = @original`, and turn the resulting
`DbUpdateConcurrencyException` into a 409 on conflict. That works, but
it has a real cost: it **rejects valid purchases**. If two requests
each want 1 unit of a product with 5 in stock, one of them loses the
optimistic-concurrency race and gets a 409 *even though there was
plenty of stock for both* — it would have succeeded fine on a retry,
but nothing about the design tells the caller that.

The current approach removes the token entirely and makes the stock
check and the decrement **one atomic statement on the database**:

```csharp
var affected = await db.Products
    .Where(p => p.Id == item.ProductId && p.IsActive && p.Stock >= item.Quantity)
    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - item.Quantity), ct);
```

The `WHERE` clause and the `SET` are evaluated together by SQL Server as
a single statement — there is no gap between "read the stock" and
"write the stock" for another transaction to land in, regardless of
isolation level. If `affected == 0`, a cheap follow-up read tells us
*why* (product missing / inactive / genuinely out of stock) and the
right domain exception is thrown. Two requests racing for the last unit
now both get a correct, deterministic outcome — the one that reaches
the row first wins, the other correctly sees insufficient stock — with
no valid purchase ever rejected.

This trades the token's application-level check for a database-level
one, so it's back to being provider-agnostic in a different way: it
relies on standard atomic `UPDATE ... WHERE ...` semantics, not a
SQL-Server-specific column type, and it happens to also side-step the
SQLite-vs-SQL-Server column type mismatch the previous approach had
(`ExecuteUpdate`/`ExecuteDelete` are supported by both providers).

**Why an explicit transaction is required.** `ExecuteUpdateAsync` is a
*bulk* operation — it's translated straight to SQL and bypasses the
change tracker entirely. Without wrapping it in an explicit
`BeginTransactionAsync`, it would commit independently of the
subsequent `SaveChangesAsync()` that inserts the `Order`/`OrderItems`,
so a crash between the two could decrement stock for an order that was
never actually created. The explicit transaction (disposed without a
commit on any exception, which rolls it back) makes the whole sequence
— every item's stock decrement plus the order insert — one unit again.

**Why items are processed in `ProductId` order.** Two orders that both
touch products 1 and 2 could otherwise take row locks in opposite
order (order A locks 1 then waits for 2; order B locks 2 then waits for
1) — a classic deadlock. Always touching rows in ascending `ProductId`
order gives every transaction the same lock acquisition order, which
rules that out.

**Why duplicate `ProductId`s in one request are rejected outright**
(`DuplicateOrderItemsException`, 400) rather than silently summed:
`ExecuteUpdateAsync` doesn't have a natural place to combine two lines
for the same product into one conditional update, and guessing intent
is worse than asking the caller to send one line per product.

**A defense-in-depth check constraint** (`CK_Products_Stock_NonNegative`,
`Stock >= 0`) is also on the `Products` table, so even a future bug in
application logic can't push stock negative — the database itself
refuses the write.

Cancellation uses the same pattern: the status flip is itself a
conditional update (`WHERE Id = @id AND Status = Pending`), so two
concurrent cancel requests for the same order can't both restore stock
— only the one that actually flips `Pending -> Cancelled` proceeds to
restore it.

One gotcha this approach surfaces: because `ExecuteUpdateAsync` bypasses
the change tracker, a *tracking* read later in the same `DbContext` can
return a stale, already-tracked instance instead of what was just
persisted — caught in practice by a unit test that calls
`CancelOrderAsync` then `GetOrderByIdAsync` in the same context and got
back `Pending` for an order that was, in the database, already
`Cancelled`. `GetOrderByIdAsync` now projects straight to `OrderResponse`
with `.Select(...)` instead of loading full entities via `Include`: a
projection to a non-entity type is never tracked in the first place (no
`.AsNoTracking()` needed to opt out of something that was never
happening), it sidesteps the staleness risk entirely, and it also fetches
only the columns the DTO actually needs.

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
  constraint enforcement) for the same speed, and `ExecuteUpdateAsync`
  needs a real relational provider anyway (it isn't supported by the EF
  `InMemory` provider).
- One integration test
  (`tests/CustomChallenge.Api.Tests/OrderApiIntegrationTests.cs`) drives
  the full stack — HTTP request through `WebApplicationFactory`,
  through the real controller/service, through EF Core, into the local
  SQL Server instance — as required by the assignment.
- A second integration test
  (`OrderConcurrencyIntegrationTests`) is the actual proof for the
  concurrency section above: it fires 10 concurrent `POST /api/orders`
  requests for 1 unit each against a product with exactly 5 in stock,
  and asserts exactly 5 succeed, 5 get a 409, and the final stock is 0.
  This is the difference between "the SQL looks atomic" and "it's
  verified atomic under real concurrent load against a real database."

Both integration tests point at their own dedicated, disposable
database, dropped before and after the run so nothing leaks between
runs or between the two tests.
