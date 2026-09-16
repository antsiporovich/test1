# PropertyManagement

ASP.NET Core MVC + Razor rental-application system (property managers and applicants).
.NET 10, EF Core code-first migrations, SQL Server, ASP.NET Identity.

## Running it

1. Set the connection string via user-secrets (never committed):
   ```
   dotnet user-secrets set ConnectionStrings:DefaultConnection "Server=localhost;Database=PropertyManagement;Trusted_Connection=True;TrustServerCertificate=True;" --project PropertyManagement.Web
   ```
2. `dotnet run --project PropertyManagement.Web` — migrations apply and the database
   is idempotently seeded automatically on start.

## Demo accounts

Shared password for every seeded account: **`Demo#12345`**

| Role | Email |
|---|---|
| Property Manager | `pm1@demo.local` (Jordan Blake), `pm2@demo.local` (Casey Nguyen) |
| Applicant | `applicant1@demo.local` … `applicant4@demo.local` |

## Bonus 4 — save-with-errors is active

The rental-application wizard's Continue button **persists a section's data even
when it fails validation**, and always advances to the next section — it no longer
blocks forward navigation the way the baseline "Continue validates + persists-when-
valid" rule does. Every outstanding issue across both sections is listed on the
Summary step, and **Submit is rejected server-side** while any issue remains, even
if the button were force-enabled client-side. See `Features/13-bonus-progressive-validation.md`.
