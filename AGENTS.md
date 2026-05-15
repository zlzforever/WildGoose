# PROJECT KNOWLEDGE BASE

**Generated:** 2026-05-14
**Commit:** dfb3893
**Branch:** main

## OVERVIEW

User/role/organization management system built on ASP.NET Core Identity. .NET 10 backend + React 19 SPA frontend. Three fixed roles (admin, organization_admin, user_admin) with path-based hierarchical org permissions.

## STRUCTURE

```
WildGoose/
├── src/WildGoose/              # API host (Program.cs entry, Controllers, Filters, Middleware)
├── src/WildGoose.Application/  # Business logic, EF DbContext, Services (CQRS-lite)
├── src/WildGoose.Domain/       # Entities, ISession, Options, ErrorCodes (zero infra deps)
├── src/WildGoose.Infrastructure/ # EMPTY — unused shell, NOT in .sln
├── src/WildGoose.Web/          # React 19 + Ant Design 5 + Vite 7 SPA
├── src/WildGoose.Tests/        # xUnit integration tests (WebApplicationFactory)
├── api.Dockerfile              # Multi-stage .NET 10 build
├── web.Dockerfile              # Node 20 + nginx build
└── docker-entrypoint.sh        # Config template rendering + optional Dapr sidecar
```

## WHERE TO LOOK

| Task | Location | Notes |
|------|----------|-------|
| Add API endpoint | `src/WildGoose/Controllers/{Admin,}/V{version}/` | Only GET/POST allowed. Deletes use `POST .../delete` |
| Add business logic | `src/WildGoose.Application/Services/Admin/{Entity}/V{version}/` | Follow Command/Queries/Dto folder pattern |
| Add domain entity | `src/WildGoose.Domain/Entity/` | Implement ICreation, IModification, IDeletion as needed |
| Add EF mapping | `src/WildGoose.Application/WildGooseDbContext.cs` | Register DbSet + configure in OnModelCreating |
| Add config option | `src/WildGoose.Domain/Options/` | Create POCO, bind in Program.cs |
| Add frontend page | `src/WildGoose.Web/src/pages/` + `App.tsx` + `config/routes.ts` | 3 places to update |
| Add frontend API call | `src/WildGoose.Web/src/services/wildgoose/api.ts` + `wildgoose.ts` | Function + type declaration |
| Modify auth | `src/WildGoose/AuthenticationExtensions.cs` | JWT + X-AUTH-TOKEN dual scheme |
| Modify permissions | `src/WildGoose.Application/Services/BaseService.cs` | Path-based org hierarchy checks |
| Add integration event | `src/WildGoose.Application/Services/Admin/{Entity}/V{version}/IntegrationEvents/` | Publish via BaseService.PublishEventAsync |
| Add test | `src/WildGoose.Tests/` | Inherit BaseTests, use [Collection("WebApplication collection")] |
| Add SQL migration | `src/WildGoose.Application/Migrations/` | `dotnet ef migrations add` |

## CONVENTIONS

- **Only GET/POST HTTP methods** — DELETE ops use `POST .../delete` (environment constraint)
- **API versioning via namespace folders** — `V10/`, `V11/` (not Microsoft.AspNetCore.ApiVersioning)
- **ObjectId-style string IDs** — `ObjectId.GenerateNewId().ToString()` (MongoDB.Bson for ID gen only)
- **NId auto-increment** — Used for tree path construction (ObjectId too long for paths)
- **Table prefix** `wild_goose_` on all DB tables, underscore case naming via `DbOptions.UseUnderScoreCase`
- **Unix epoch timestamps** — All DateTimeOffsets stored as long (seconds/milliseconds)
- **Soft delete** — `IDeletion` entities: `SaveChangesAsync` converts Delete→Modify+IsDeleted=true; EF query filters exclude deleted
- **Auto-audit** — `ICreation`/`IModification` auto-populated from ISession in `WildGooseDbContext.ApplyConcepts()`
- **Primary constructors** — All service classes use C# 12 primary constructors for DI
- **C# 13 extension blocks** — `extension(T)` syntax used in SessionExtensions, PathExtensions, WebApplicationBuilderExtensions
- **InternalsVisibleTo** — Application layer exposes internals to WildGoose + WildGoose.Tests
- **Standard response envelope** — All responses auto-wrapped to `{Code, Success, Data/Msg}` by ResponseWrapperFilter
- **3-tier RBAC** — admin (super), organization_admin (scoped), user_admin (users+orgs, no roles)
- **Path-based org hierarchy** — Admin permissions via `OrganizationDetail.Path.StartsWith()` prefix matching

## ANTI-PATTERNS (THIS PROJECT)

- **Broad `catch(Exception)`** — 11 instances across 6 files mask original exception types during transaction rollback; EF Core auto-rollbacks on dispose make explicit RollbackAsync redundant
- **In-memory DB filtering** — `DbContextExtensions.AnyPermissionAsync` loads all org admin paths then filters in C# (should filter in SQL)
- **Task.Delay polling** — `GenerateTop3LevelOrganizationsToFileService` uses 5-minute polling loop; `BaseService` Dapr retry uses fixed 100ms with no backoff
- **Commented-out dead code** — ~130 lines in OrganizationAdminService.cs, blocks in BaseService.cs and SeedData.cs

## UNIQUE STYLES

- `Identity.Sm` package replaces standard `Microsoft.AspNetCore.Identity` (supports SM3 national crypto)
- AES-ECB request body encryption toggled via `window.wildgoose.enableEncryption` (frontend)
- `Config_source` env var renders `appsettings.json` from template at container startup
- Runtime `config.js` substitution for frontend Docker deployment

## COMMANDS

```bash
# Backend
dotnet build                                          # Build solution
dotnet run --project src/WildGoose                    # Run API (needs PostgreSQL)
dotnet test src/WildGoose.Tests                       # Run tests (needs PostgreSQL + testdata.sql)

# Frontend
cd src/WildGoose.Web && yarn install && yarn run dev  # Dev server on :5174
cd src/WildGoose.Web && yarn run build                # Production build → dist/

# EF Migrations
dotnet ef migrations add <Name> --project src/WildGoose.Application --startup-project src/WildGoose

# Docker
docker build -f api.Dockerfile -t wildgoose-api .
docker build -f web.Dockerfile -t wildgoose-web .
```

## NOTES

- `WildGoose.Infrastructure` project exists on disk but is NOT in .sln and has zero code — do not use
- Tests share a single PostgreSQL database (no per-test isolation); seeded by `testdata.sql`
- CI pipelines only build Docker images — no `dotnet test` or lint in CI
- Docker image tags are hardcoded (e.g., `20260512.1`), not auto-generated from git
- `yarn.lock` is gitignored — frontend builds may not be reproducible
- Special roles (admin, organization_admin, user_admin) cannot be added/modified/deleted
- Password login can be disabled via `DISABLE_PASSWORD_LOGIN` config
