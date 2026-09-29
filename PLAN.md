# Plan de implementación — Order Management System

## Stack y decisiones de versión

- **.NET 10** (SDK y runtime ya instalados en la máquina) en lugar de .NET 8.
  El enunciado pide ".NET 8 / ASP.NET Core" como baseline; usamos la LTS/versión
  más reciente disponible localmente. Se documenta como decisión consciente:
  mismo modelo de programación (Minimal Hosting, `IExceptionHandler`, EF Core),
  sin breaking changes relevantes para este alcance. Se defiende en la
  entrevista como "usamos la versión moderna disponible en el entorno".
- **SQL Server Express** (ya corriendo localmente como `MSSQL$SQLEXPRESS`) + **EF Core**.
- **React + TypeScript** con **Vite** (reemplazo moderno de CRA, que está deprecado).
- **xUnit** + **Shouldly** (MIT — se evita FluentAssertions v8+ por su cambio de
  licencia a modelo comercial) + **Moq** para tests backend.
- **Vitest + React Testing Library** para tests frontend.
- Git local, commits semánticos por hito.

## Arquitectura backend

Un solo proyecto Web API (sin capas/proyectos artificiales para este alcance),
organizado por carpetas:

```
CustomChallenge.Api/
├── Controllers/        ProductsController, OrdersController
├── Services/            IOrderService/OrderService, IProductService/ProductService
├── Data/                AppDbContext, Configurations/ (Fluent API)
├── Domain/              Product, Order, OrderItem, OrderStatus
├── Exceptions/          ProductNotFoundException, ProductInactiveException,
                         InsufficientStockException, OrderNotPendingException
├── Dtos/                CreateOrderRequest, OrderResponse, ProductResponse...
├── ExceptionHandling/   GlobalExceptionHandler : IExceptionHandler
└── Program.cs
CustomChallenge.Api.Tests/   unit + integration
```

Decisión: **no** repositorio genérico sobre EF Core — `AppDbContext` ya actúa
como Unit of Work; una capa `IRepository<T>` sería abstracción sin beneficio
real a este tamaño. Los `Service` usan el `DbContext` directamente.

## Manejo de errores

`IExceptionHandler` (moderno, reemplazo del middleware manual) + `AddProblemDetails()`
para formato RFC7807 consistente en toda la API (incluyendo 404/400 nativos de
ASP.NET Core, no solo excepciones propias).

## Atomicidad y concurrencia (creación de orden)

1. **Atomicidad transaccional**: insertar `Order` + `OrderItems` y descontar
   `Product.Stock` en un único `SaveChangesAsync()` → EF Core ya lo envuelve en
   una transacción implícita.
2. **Condición de carrera** (el punto realmente "senior"): dos requests
   concurrentes podrían leer el mismo stock antes de que ninguno lo descuente.
   Solución: **concurrency token** (`RowVersion` / `[Timestamp]`) en `Product`.
   Si el stock cambió entre la validación y el `SaveChanges`, EF lanza
   `DbUpdateConcurrencyException` → se mapea a `409 Conflict`.

## Cancelación

Solo permitida si `Order.Status == Pending`. Cambia estado a `Cancelled` y
devuelve el stock de cada `OrderItem` a su `Product` (misma lógica de
atomicidad/concurrencia que la creación).

## Endpoints

| Método | Ruta | Descripción |
|---|---|---|
| GET | /api/products | Lista de productos |
| POST | /api/orders | Crear orden |
| GET | /api/orders | Lista de órdenes |
| GET | /api/orders/{id} | Detalle de orden |
| POST | /api/orders/{id}/cancel | Cancelar orden |

## Validación

- `DataAnnotations` + `ModelState` para forma del request (campos requeridos, cantidades > 0).
- Reglas de negocio (producto existe / activo / stock suficiente) viven en
  `OrderService`, lanzan excepciones de dominio mapeadas por el `GlobalExceptionHandler`.

## Testing backend

- Unit tests de `OrderService` (SQLite in-memory o EF InMemory provider):
  orden válida, stock insuficiente, producto inválido/inactivo, cancelar orden pendiente.
- 1 integration test real con `WebApplicationFactory<Program>` contra SQL Server
  Express local (BD de prueba dedicada, migraciones aplicadas, limpieza entre tests):
  cubre HTTP → Controller → Service → EF Core → SQL.

## Frontend

- Vite + React + TypeScript.
- Páginas: `ProductsPage`, `OrdersPage`, `OrderDetail`.
- Sin Redux/React Query: custom hook `useApi` simple (`useState` + `useEffect`).
- Loading/Error states explícitos por página, validación básica de formulario
  (cantidad > 0, ≤ stock disponible).
- Responsive con Flexbox/Grid simple, sin librería de UI pesada.
- Tests: Vitest + RTL — render de `ProductsPage` con datos mockeados y
  validación del formulario de compra.

## Git

Init local, commits pequeños y semánticos por hito:
`feat: domain models + migrations`, `feat: order creation with concurrency handling`,
`test: order service unit tests`, `feat: frontend products/orders pages`, etc.

## README final

Sección "Architecture Decisions" (estilo ADR) cubriendo: por qué `IExceptionHandler`,
manejo de concurrencia de stock, por qué no repositorio genérico, por qué no
Redux/React Query, elección de testing, y la decisión de usar .NET 10.

## Orden de implementación

1. ~~Plan (este archivo)~~
2. Scaffold solución backend (sln + proyecto API + proyecto de tests) + git init
3. Dominio + `AppDbContext` + migraciones + seed de productos
4. `GlobalExceptionHandler` + excepciones de dominio
5. `OrderService` (crear orden con concurrencia + cancelar) + Controllers
6. Unit tests backend
7. Integration test contra SQL Server Express
8. Frontend (Vite scaffold, páginas, hook de API)
9. Tests frontend
10. README con decisiones + revisión final de commits
