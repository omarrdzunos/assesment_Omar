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

Estrategia actual (revisada — ver [README.md](README.md#concurrency-atomic-conditional-executeupdateasync-not-a-concurrency-token)
para el detalle completo y el porqué del cambio): **update condicional atómico**
en la base de datos en vez de un concurrency token optimista.

1. Se valida que no haya `ProductId` repetidos en el request (`DuplicateOrderItemsException`, 400).
2. Se abre una transacción explícita (`Database.BeginTransactionAsync`).
3. Por cada item, **en orden ascendente de `ProductId`** (evita deadlocks entre
   órdenes que tocan los mismos productos en orden distinto):
   ```csharp
   var affected = await db.Products
       .Where(p => p.Id == item.ProductId && p.IsActive && p.Stock >= item.Quantity)
       .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - item.Quantity), ct);
   ```
   El `WHERE` y el `SET` se evalúan como una sola sentencia atómica — no hay
   ventana entre "leer stock" y "escribir stock" para que otra transacción se
   cuele. Si `affected == 0`, una lectura de diagnóstico determina la causa
   exacta (`ProductNotFoundException` / `ProductInactiveException` /
   `InsufficientStockException`).
4. Se agrega la `Order` + `OrderItems`, `SaveChangesAsync`, y `CommitAsync`.
   La transacción explícita es obligatoria porque `ExecuteUpdateAsync` no pasa
   por el change tracker ni por la transacción implícita de `SaveChanges`.
5. Red de seguridad en la BD: check constraint `CK_Products_Stock_NonNegative`
   (`Stock >= 0`) en `Products`.

Se descartó el concurrency token (`Product.Version` + `DbUpdateConcurrencyException`)
porque rechazaba compras válidas: si dos requests piden 1 unidad de un producto
con 5 en stock, uno de los dos podía perder la carrera optimista y recibir 409
aunque había stock de sobra para ambos.

## Cancelación

Solo permitida si `Order.Status == Pending`. La transición de estado es también
un update condicional atómico (`WHERE Id = @id AND Status = Pending`), así que
dos cancelaciones concurrentes de la misma orden no pueden devolver el stock
dos veces — solo la que realmente cambia el estado continúa y restaura stock
(también vía `ExecuteUpdateAsync`, ordenado por `ProductId`).

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

- Unit tests de `OrderService` contra **SQLite in-memory real** (no el provider
  `InMemory` de EF — `ExecuteUpdateAsync` no es compatible con él): orden válida,
  stock insuficiente, producto inválido/inactivo, items duplicados, cancelar
  orden pendiente, cancelar dos veces (verifica que el stock no se restaure dos
  veces).
- 1 integration test real con `WebApplicationFactory<Program>` contra SQL Server
  Express local (BD de prueba dedicada, migraciones aplicadas, limpieza entre tests):
  cubre HTTP → Controller → Service → EF Core → SQL.
- 1 integration test de **concurrencia real**: 10 `POST /api/orders` en paralelo
  contra un producto con stock 5 (cantidad 1 cada uno) → exactamente 5 deben
  responder 201 y 5 con 409, y el stock final debe ser 0. Es la prueba de que
  la estrategia de `ExecuteUpdateAsync` condicional realmente sostiene bajo
  carga concurrente real, no solo que "el SQL se ve atómico".

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

1. ~~Plan (este archivo)~~ ✅
2. ~~Scaffold solución backend (sln + proyecto API + proyecto de tests) + git init~~ ✅
3. ~~Dominio + `AppDbContext` + migraciones + seed de productos~~ ✅
4. ~~`GlobalExceptionHandler` + excepciones de dominio~~ ✅
5. ~~`OrderService` (crear orden con concurrencia + cancelar) + Controllers~~ ✅
6. ~~Unit tests backend~~ ✅
7. ~~Integration test contra SQL Server Express~~ ✅
8. ~~Frontend (Vite scaffold, páginas, hook de API)~~ ✅
9. ~~Tests frontend~~ ✅
10. ~~README con decisiones + revisión final de commits~~ ✅ (ver [README.md](README.md))
