# WildGoose.Application — Business Logic Layer

## OVERVIEW

Core business logic layer: services, EF DbContext, identity customization, migrations. Uses CQRS-lite folder organization (Command/Queries/Dto) within versioned service namespaces.

## STRUCTURE

```
WildGoose.Application/
├── Services/
│   ├── BaseService.cs                          # Abstract base: permission checks, Dapr events, org queries
│   ├── GenerateTop3LevelOrganizationsToFileService.cs  # Background hosted service (5min polling)
│   ├── Admin/
│   │   ├── User/V10/                           # User admin CRUD (965 lines main service)
│   │   │   ├── UserAdminService.cs             # 10 DI deps, 36 methods, 3 query strategies
│   │   │   ├── Command/                        # AddUserCommand, UpdateUserCommand, etc.
│   │   │   ├── Queries/                        # GetUserQuery, GetUserListQuery
│   │   │   ├── Dto/                            # UserDto, UserDetailDto
│   │   │   └── IntegrationEvents/              # UserAddedEvent, UserDeletedEvent, etc.
│   │   ├── User/V11/                           # Simplified user creation for internal services
│   │   ├── Role/V10/                           # Role CRUD + assignable roles
│   │   └── Organization/V10/                   # Org tree CRUD + admin management (805 lines)
│   ├── User/V10/                               # Self-service user ops (password reset)
│   └── Organization/V10/                       # Read-only org queries for non-admin
├── Identity/                                   # Custom validators, Chinese error describer, SM3 hasher
├── Extensions/                                 # DbContextExtensions (raw SQL), SessionExtensions (role checks)
├── Ef/                                         # DateTimeOffset converters, PropertyBuilder extensions
├── Migrations/                                 # EF Core migrations
├── Dto/                                        # Shared DTOs
├── Permission/                                 # Permission checking service
├── OSS/                                        # Object storage abstraction
├── WildGooseDbContext.cs                       # IdentityDbContext with soft-delete, auto-audit, table config
├── HttpSession.cs                              # ISession impl from HttpContext claims
├── ScopeServiceProvider.cs                     # Service locator via IHttpContextAccessor
└── SeedData.cs                                 # Default role seeding at startup
```

## WHERE TO LOOK

| Task | Location |
|------|----------|
| Add new admin service | `Services/Admin/{Entity}/V{version}/` — create {Entity}AdminService.cs + Command/Queries/Dto folders |
| Add new version of existing service | Copy `V10/` structure to `V11/`, modify as needed |
| Modify permission logic | `Services/BaseService.cs` — CanManageAll, CheckUserPermissionAsync |
| Add EF mapping/entity | `WildGooseDbContext.cs` — add DbSet + OnModelCreating config |
| Add integration event | `Services/Admin/{Entity}/V{version}/IntegrationEvents/` — publish via BaseService.PublishEventAsync |
| Add custom Identity validator | `Identity/` — implement IUserValidator/IUserStore as needed |
| Add raw SQL for new DB op | `Extensions/DbContextExtensions.cs` — use Dapper for perf-critical queries |
| Change DB table naming | `WildGooseDbContext.OnModelCreating` — DbOptions.TablePrefix + UseUnderScoreCase |

## CONVENTIONS

- **Primary constructors** for DI — `public class XxxService(DbContext db, ISession session, ...) : BaseService(...)`
- **BaseService inheritance** — All admin services extend BaseService for shared permission/event logic
- **Permission branching** — Every public method forks on `IsSupperAdminOrUserAdmin()` vs `IsOrganizationAdmin()`
- **Command/Queries/Dto** folders under each versioned service — NOT full CQRS (no MediatR), service class handles all
- **Integration events** — Dapr pub/sub via `BaseService.PublishEventAsync` (3 retries, 100ms delay)
- **InternalsVisibleTo** — Internal classes visible to WildGoose host + Tests
- **Unsafe code** — `AllowUnsafeBlocks` enabled (used in StringExtensions.ToCamelCase)
- **ObjectId IDs** — `ObjectId.GenerateNewId().ToString()` for all new entity IDs
- **Soft delete** — `WildGooseDbContext.ApplyConcepts()` intercepts SaveChanges, converts Delete→Modify

## ANTI-PATTERNS

- **UserAdminService is 965 lines** — mixes CRUD, file upload/OSS, extension properties, identity management, query building. Consider splitting into query + command services.
- **Transaction catch(Exception)** — UserAdminService, OrganizationAdminService, RoleAdminService all use `catch(Exception) { rollback; throw friendly; }` — redundant since EF auto-rollbacks on dispose
- **In-memory filtering** — `DbContextExtensions.AnyPermissionAsync` loads all admin paths then filters in C# (TODO at line 67)
- **No caching on org permission checks** — BaseService TODO at line 70: repeated DB calls for org admin lookups need caching
- **~130 lines dead code** — OrganizationAdminService.cs lines 642-804, BaseService line 165, SeedData line 142
