# VacationManager

VacationManager is a small role-based vacation tracker built as a Blazor Server application. Employees request time off, team leads and the CEO review those requests, and the CEO organizes people into teams and projects.

The repository is maintained by Alex Venkov ([JohnyKid123](https://github.com/JohnyKid123)), a Software Engineering student.

## Stack

- C# / .NET 8
- ASP.NET Core
- Blazor Server
- Entity Framework Core 8 with SQL Server
- ASP.NET Core Identity
- AutoMapper
- xUnit and Moq for unit tests

## Features

- Account registration and sign-in with ASP.NET Core Identity
- Roles: CEO, Team Lead, Developer, and Unassigned
- Team management: create and edit teams, assign a team lead and developers
- Project management: assign teams to a project and soft-delete projects (`IsDeleted`, kept out of normal queries)
- Leave requests: paid, unpaid, and sick leave, including an optional attachment path
- Approval flow: a pending request can be approved or denied
- Removing a team unassigns its lead and developers

There is no public deployment of this app.

## Architecture

```
VacationManager/
  VacationManager/            Blazor Server UI, Identity pages, composition root
  VacationManager.Core/       Team, project, user, and leave-request services
  VacationManager.Data/       EF Core model, Identity, repositories, migrations
  VacationManager.Test/       Unit tests for the core services
```

The UI calls services in `VacationManager.Core`. Those services use a generic `IRepository<T>` and `IAuthenticationContext` for the signed-in user. SQL Server access stays in `VacationManager.Data`.

Authorization policies:

- `RequireCEORole` — CEO only (teams and projects)
- `RequireCEOOrTLRole` — CEO or team lead (leave-request review)

## Run locally

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). From the repository root:

```bash
dotnet restore VacationManager/VacationManager.sln
dotnet build VacationManager/VacationManager.sln
dotnet test VacationManager/VacationManager.sln
dotnet run --project VacationManager/VacationManager/VacationManager.csproj
```

The HTTPS profile listens on `https://localhost:7184` and `http://localhost:5174`.

The app expects SQL Server. `appsettings.Development.json` points at LocalDB, which is available with SQL Server Express on Windows:

`Server=(localdb)\mssqllocaldb;Database=VacationManager;Trusted_Connection=True;MultipleActiveResultSets=true`

Apply the existing migrations (the connection string is the single argument after `--`):

```bash
dotnet tool install --global dotnet-ef
dotnet ef database update \
  --project VacationManager/VacationManager.Data/VacationManager.Data.csproj \
  --startup-project VacationManager/VacationManager/VacationManager.csproj \
  -- "Server=(localdb)\\mssqllocaldb;Database=VacationManager;Trusted_Connection=True;MultipleActiveResultSets=true"
```

On a machine without LocalDB, set `ConnectionStrings__DefaultConnection` to your SQL Server instance before `dotnet ef` and `dotnet run`.

### Secrets

`appsettings.json` does not contain a database password. For a local SQL login, store the connection string with [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) (the web project already has a user-secrets id):

```bash
cd VacationManager/VacationManager
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=VacationManager;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True"
```

User-secrets override `appsettings.Development.json` when `ASPNETCORE_ENVIRONMENT` is `Development`. Do not commit passwords or `appsettings.*.local.json` files.

## Tests

```bash
dotnet test VacationManager/VacationManager.sln
```

`VacationManager.Test` covers team creation and removal, project ownership and soft-delete, and the leave-request approval flow. Database reads that go through `DbContext` directly are exercised with the EF Core in-memory provider.

## Screenshots

No product screenshots are checked in. When you have the app running, add images under `docs/screenshots/` (for example the team list, a project, and an approve/deny leave request) and link them here.
