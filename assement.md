Exercise: Order Management System
Technology
Candidate must use:
Backend: .NET 8 / ASP.NET Core
Database: SQL Server + EF Core
Frontend: React + TypeScript
Tests: xUnit/NUnit/MSTest
Git
AI tools are allowed. But you must be able to explain and justify what was done.
1. Functional Requirements
Build a small application for managing customer orders.
Products
A product has:
Id
Name
Price
Stock
IsActive
Orders
An order has:
Id
CreatedAt
Status
Items
An order item has:
ProductId
Quantity
UnitPrice
Backend API
Implement:
GET    /api/products
POST   /api/orders
GET    /api/orders
GET    /api/orders/{id}
POST   /api/orders/{id}/cancel
Creating an order
The API must:
Validate the request.
Verify the product exists.
Verify the product is active.
Verify sufficient stock.
Store the current product price in OrderItem.UnitPrice.
Decrease the product stock.
Create the order.
The stock update and order creation should be treated as one atomic operation.
Cancelling
An order can be cancelled only if it is still pending.
When cancelled:
The order status changes.
The stock is returned.
2. Frontend
Keep this intentionally small.
Products page
Display:
Product       Price       Stock       Action
Laptop        $1,200      10          Buy
Keyboard      $100        25          Buy
Mouse         $50         0           Buy
Orders page
Display:
Order #       Date          Status
1001          Sep 29        Pending
1002          Sep 28        Cancelled
Clicking an order displays its details.
The frontend should include:
Loading state
Error state
Basic form validation
Responsive layout
API integration
Don't require Redux, React Query, Next.js, etc. for the basic implementation.
Those can be discussed later.
3. Tests
Require only:
Backend
At least:
Valid order
Insufficient stock
Invalid product
Cancel order
And one integration test covering:
HTTP → API → EF Core → SQL database
Frontend
At least 1–2 meaningful component tests.
Don't make test quantity the goal.