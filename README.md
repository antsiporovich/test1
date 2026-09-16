# PropertyManagement

ASP.NET Core MVC + Razor rental-application system for a property-management company.
Two roles — **Property Manager** and **Applicant**. Applicants browse available units
and submit a multi-section rental application through a single-page wizard; property
managers maintain properties/units and review applications, and an approval issues a
12-month lease.

Server-rendered throughout (Razor + Bootstrap 5 + small vanilla-JS `fetch`, no SPA
framework). All business rules are enforced server-side, not by hidden UI.

## Tech stack

- **.NET 10**, ASP.NET Core MVC + Razor
- **ASP.NET Identity** (role chosen at sign-up)
- **EF Core** with code-first migrations (SQL Server)
- **Bogus** for idempotent startup seeding
- **xUnit** + FluentAssertions for business-logic unit tests
- **Microsoft.AspNetCore.OpenApi** for the JSON endpoint spec

## Solution layout

- `PropertyManagement.Domain` — entities, enums, pure business rules (no EF/ASP.NET)
- `PropertyManagement.Infrastructure` — `AppDbContext`, EF configs, migrations, Identity, Bogus seeder, services
- `PropertyManagement.Web` — controllers, view models, Razor views/partials/view components, startup, OpenAPI
- `PropertyManagement.Tests` — xUnit business-logic tests

Dependency direction: Web → Infrastructure → Domain.

## Prerequisites

- **.NET 10 SDK** (`dotnet --version` ≥ 10.0)
- **SQL Server** reachable locally (a default local instance works — the connection
  string below uses `Server=localhost;Trusted_Connection=True`)

## Running it

1. Set the connection string via user-secrets (never committed):
   ```
   dotnet user-secrets set ConnectionStrings:DefaultConnection "Server=localhost;Database=PropertyManagement;Trusted_Connection=True;TrustServerCertificate=True;" --project PropertyManagement.Web
   ```
2. `dotnet run --project PropertyManagement.Web` — on start it applies EF Core
   migrations (`MigrateAsync`) then idempotently seeds the database. Safe to run
   repeatedly; re-running inserts nothing new.

## Running the tests

```
dotnet test
```

## JSON API + OpenAPI

The Applications grid is backed by `GET /api/applications` (paged/sorted/filtered,
same ownership scope as the server-rendered list). The OpenAPI document is served at
`/openapi/v1.json` when the app is running.

## Demo accounts

Shared password for every seeded account: **`Demo#12345`**

| Role | Email |
|---|---|
| Property Manager | `pm1@demo.local` (Jordan Blake), `pm2@demo.local` (Casey Nguyen) |
| Applicant | `applicant1@demo.local` … `applicant4@demo.local` |

The seed data covers every application status (Draft, Submitted, Under Review,
Returned, Approved, Denied, Withdrawn), the lease-availability matrix (a lease
covering today, one expired, one future), a multi-applicant application, and a
PM-only note.

## Note: Bonus 4 (save-with-errors) is active

Bonus 4 intentionally overrides the baseline rule F4b.i: the wizard's **Continue**
persists a section and advances even when it fails validation. The Summary lists all
outstanding issues and **Submit is rejected server-side** while any remain.
