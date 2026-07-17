# Order Management System

## Overview
Order Management System is an ASP.NET Core 8 REST API for managing users, master data, orders, stock, reporting, and currency conversion.

## Architecture
The API uses a layered flow:
```text
Controller -> Service -> Repository -> EF Core -> SQL Server
```
Controllers handle HTTP routing, request binding, response status codes, and response shaping. Controllers do not access `ApplicationDbContext` directly.

Services contain business logic, transactions, validation beyond request shape, authorization decisions, order lifecycle rules, and stock-sensitive operations.

Repositories handle EF Core data access, query composition, persistence, eager loading, and database-specific concerns.

Middleware handles structured request logging and centralized Problem Details error responses. Background jobs, queues, seeders, database setup, and external integrations are infrastructure concerns.

Main folders:
- `Domain`: module DTOs, services, and repository contracts.
- `Http`: versioned API controllers.
- `Infrastructure`: EF Core, repositories, middleware, jobs, queues, seeders, and integrations.
- `Models`: EF Core entities.
- `Migrations`: EF Core migrations and model snapshot.

## Database Schema
Main entities and relationships:
- `Role` -> many `Users`
- `Category` -> many `Products`
- `Supplier` -> many `Products`
- `Customer` -> many `Orders`
- `User` -> many created `Orders`
- `Order` -> many `OrderItems`
- `Product` -> many `OrderItems`
- `Order` -> many `OrderStatusHistories`
- `DailySalesReport` -> many `DailySalesReportItems`
- `BackgroundJobExecution` records scheduled job execution state

Important rules: product SKU is unique; product and customer records support soft deactivation; audit fields are stored in UTC; product stock uses concurrency protection; order items preserve product and price snapshots; orders preserve `CurrencyCode` and `ExchangeRate` so historical totals are not recalculated when rates change.

## External Integration
Frankfurter is used as the external exchange-rate service.

The integration uses a typed `HttpClient` registered through `IHttpClientFactory`, Polly retry with exponential backoff, configured timeout, circuit breaker, and structured logging for retry and final-failure events. Final external failures are returned as graceful HTTP `503 Service Unavailable` Problem Details responses.

Currency conversion is applied during order creation. Product base prices, line totals, and subtotal remain preserved, while the order stores the applied currency code, exchange rate, and converted historical total.

Useful endpoint:
```http
GET /api/v1/exchange-rates?from=USD&to=IDR
```

## Run Locally
Prerequisites: .NET SDK 8, SQL Server 2019+ or Docker Desktop, and the EF Core CLI.

```bash
dotnet tool install --global dotnet-ef
```
Start SQL Server with Docker:
```powershell
$env:OMS_SQL_PASSWORD = "<your-local-sql-password>"
docker compose up -d
```
Configure secrets outside source control:
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your-local-sql-connection-string>" --project src/OMS.API
dotnet user-secrets set "Jwt:SigningKey" "<your-local-jwt-signing-key-at-least-32-characters>" --project src/OMS.API
dotnet user-secrets set "DevelopmentAdmin:Password" "<your-local-admin-password>" --project src/OMS.API
```
Run the API:
```bash
dotnet restore
dotnet ef database update --project src/OMS.API --startup-project src/OMS.API
dotnet run --project src/OMS.API --launch-profile http
```
Swagger: `http://localhost:5009/swagger`

## Postman
Import `postman/OMS.postman_collection.json` and `postman/OMS.local.postman_environment.json`. Set `baseUrl` if needed, fill local password variables in the environment, and run a login request to populate `accessToken`.

## Verification
```bash
dotnet build
dotnet test
dotnet test --filter "Category=Integration"
```
Latest verified results: 294 tests passed, including 5 integration tests.

