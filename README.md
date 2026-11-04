# IncidentManagementSystem

A single ASP.NET Core MVC project targeting .NET 10, configured with ASP.NET Core Identity, EF Core 10, SQL Server, and AutoMapper.

## Requirements

- .NET 10 SDK
- SQL Server Express installed and running

## Find the SQL Server Express instance name

In SQL Server Configuration Manager, open **SQL Server Services** and find the service named `SQL Server (INSTANCE_NAME)`. The instance name is the value in parentheses. From PowerShell, list SQL Server services with:

```powershell
Get-Service -Name 'MSSQL*' | Select-Object Name, Status, DisplayName
```

For example, the service `MSSQL$SQLEXPRESS` corresponds to the server `localhost\SQLEXPRESS` or `.\SQLEXPRESS`. Do not assume that another machine uses the same instance name. On this development machine, the `SQLEXPRESS` service was found running.

## Configure the database connection

The default `ConnectionStrings:DefaultConnection` in `FMS-FaultMangmentSystem/appsettings.json` uses Windows Authentication and the verified local instance. Change the server and database names in that setting as needed. Do not store SQL credentials in source control.

To override the connection string locally with User Secrets, run this from the solution directory, replacing the server name if needed:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=IncidentManagementSystemDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true" --project .\FMS-FaultMangmentSystem\IncidentManagementSystem.csproj
```

## Run the application

```powershell
dotnet restore .\FMS-FaultMangmentSystem\IncidentManagementSystem.csproj
dotnet run --project .\FMS-FaultMangmentSystem\IncidentManagementSystem.csproj --launch-profile https
```

While the application is running locally, open `https://localhost:7290/health/database`. A successful connection returns a generic success response; a failed connection returns HTTP 503. The endpoint only responds to loopback requests and does not expose connection details. It checks connectivity only and does not create, delete, or migrate a database.

The application synchronizes the four predefined roles and their permission claims at startup. To create the initial SystemAdmin user, configure `BootstrapAdmin:Email` and `BootstrapAdmin:Password` through User Secrets or environment variables. The optional `BootstrapAdmin:FullName` defaults to `System Administrator`. Startup creates the account only if that email does not exist and idempotently adds the SystemAdmin role; it never resets an existing account's password or reactivates an inactive account. Do not commit these values to source control.

Set bootstrap credentials locally from PowerShell in the solution directory:

```powershell
dotnet user-secrets set "BootstrapAdmin:Email" "admin@example.invalid" --project .\FMS-FaultMangmentSystem\IncidentManagementSystem.csproj
dotnet user-secrets set "BootstrapAdmin:Password" "Use-A-Unique-Strong-Secret-Here!" --project .\FMS-FaultMangmentSystem\IncidentManagementSystem.csproj
dotnet user-secrets set "BootstrapAdmin:FullName" "System Administrator" --project .\FMS-FaultMangmentSystem\IncidentManagementSystem.csproj
```

For deployment, supply the equivalent `BootstrapAdmin__Email`, `BootstrapAdmin__Password`, and optional `BootstrapAdmin__FullName` through the platform's secret provider. If credentials are absent, bootstrap is skipped and a warning is logged. Sign in at `/Account/Login`; inactive accounts are rejected.

Apply the pending domain migration before signing in. The permission refresh checks the current user's `IsActive` value in the database, which is added by that migration.

## EF Core migrations

The project preserves the existing `20261104084638_initialcreate` Identity migration and adds `20261104085814_AddIncidentManagementEntities` for the incident-system schema. Review the configured connection string and back up any existing database before applying pending migrations.

To list migrations:

```powershell
dotnet ef migrations list --project .\FMS-FaultMangmentSystem\IncidentManagementSystem.csproj --startup-project .\FMS-FaultMangmentSystem\IncidentManagementSystem.csproj
```

To apply pending migrations to the configured SQL Server database:

```powershell
dotnet ef database update --project .\FMS-FaultMangmentSystem\IncidentManagementSystem.csproj --startup-project .\FMS-FaultMangmentSystem\IncidentManagementSystem.csproj
```

This applies migrations incrementally; it does not drop or recreate the database. Do not use `EnsureCreated` with this migration-based schema. No database update has been run as part of this phase.
