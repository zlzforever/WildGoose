# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

WildGoose is a .NET 10 user and organization management API built with ASP.NET Core, Entity Framework Core, and Dapr. The application provides multi-tenant organization management with hierarchical organization structures, role-based access control, and user administration.

## Solution Architecture

The solution follows a layered architecture with clear separation of concerns:

- **WildGoose** - Main API project (Presentation/Web layer)
  - Controllers organized by API version (V10, V11, Admin)
  - Middleware pipeline includes request decryption and response wrapping
  - JWT and token-based authentication with custom authorization policies
  - Entry point: `Program.cs`

- **WildGoose.Domain** - Domain layer (Core business entities and interfaces)
  - Entity classes: `User`, `Role`, `Organization`, `OrganizationUser`, `OrganizationAdministrator`, etc.
  - Domain interfaces: `ISession`, `IObjectStorageService`
  - Domain concepts: `ICreation`, `IModification`, `IDeletion` (audit trail)
  - Value objects and DTOs defined in application layer

- **WildGoose.Infrastructure** - Data access layer
  - `WildGooseDbContext` - EF Core DbContext with automatic audit tracking
  - Supports both MySQL and PostgreSQL via configuration
  - Database-agnostic design with configurable table prefixes
  - Dapper integration for complex queries

- **WildGoose.Application** - Application/Business logic layer
  - Service classes organized by domain (User, Role, Organization, Permission)
  - CQRS-style separation: Commands (write operations) and Queries (read operations)
  - All services inherit from `BaseService` which provides common authorization logic
  - Integration events for Dapr pub/sub

- **WildGoose.Tests** - Test project using xUnit, Moq, and ASP.NET Core testing utilities

## Key Technologies and Patterns

### Entity Framework Core
- Dual database support: MySQL (Pomelo provider) and PostgreSQL (Npgsql provider)
- Configured via `DbContext` configuration section in appsettings
- Soft delete pattern with global query filters (`IsDeleted`)
- Automatic audit fields (CreationTime, CreatorId, LastModificationTime, etc.) applied in `SaveChangesAsync`
- Database type determined by `DbContext:DatabaseType` configuration

### Authentication & Authorization
- Primary: JWT Bearer authentication with RSA security key or Authority discovery
- Secondary: Custom "SecurityToken" scheme for internal service communication
- ASP.NET Core Identity for user management with custom `User` and `Role` entities
- Two authorization policies:
  - `SUPER_OR_ORG_ADMIN` - Requires "admin" or "organization-admin" role
  - `SUPER` - Requires "admin" role only
- Custom password hasher option: SM3 (Chinese cryptographic standard) via `Identity.Sm` package

### Organization Hierarchy
- Tree-based organization structure with recursive relationships
- Permission model based on organization paths: admin users can manage descendants
- `OrganizationDetail` entity provides pre-computed path information for queries
- Organization administrators have scoped permissions within their subtree

### Service Registration
All application services are registered in `WebApplicationBuilderExtensions.RegisterServices()` using `TryAddScoped` to prevent duplicate registrations.

### Request/Response Processing
- `DecryptRequestMiddleware` - Decrypts incoming requests
- `ResponseWrapperFilter` - Wraps all responses in a standard envelope
- `GlobalExceptionFilter` - Centralized exception handling
- JSON serialization uses camelCase property naming

## Common Development Commands

### Building the Solution
```bash
dotnet build WildGoose.sln
```

### Running Tests
```bash
# Run all tests
dotnet test src/WildGoose.Tests/WildGoose.Tests.csproj

# Run a single test (filter by fully qualified test name)
dotnet test --filter "FullyQualifiedName~TestMethodName"
```

### Database Migrations
Migrations are created from the main WildGoose project with output to Infrastructure:

```bash
# Add a new migration
cd src/WildGoose
dotnet ef migrations add MigrationName -p ../WildGoose.Infrastructure

# Apply migrations to database
dotnet ef database update -p ../WildGoose.Infrastructure
```

Note: Migrations are automatically applied on application startup in `Program.cs:135-144`.

### Running the Application
```bash
cd src/WildGoose
dotnet run
```

The application will:
1. Configure Serilog logging from appsettings or environment variables
2. Apply any pending EF Core migrations automatically
3. Seed initial data via `SeedData.Init()`
4. Start listening on configured ports

### Docker Support
- `api.Dockerfile` - Containerizes the API
- `web.Dockerfile` - For the frontend (if present)
- `docker-entrypoint.sh` - Container startup script
- GitHub Actions workflow: `.github/workflows/backend.yml`

## Configuration Structure

Key configuration sections in `appsettings.json`:

- **DbContext** - Database connection string, table prefix, naming convention (camelCase/snake_case)
- **Identity** - Password requirements, lockout settings, user validation rules
- **JwtBearer** - JWT validation settings (RSA key path or Authority URL)
- **Dapr** - Pub/sub component name for integration events
- **WildGoose** - Application-specific settings (e.g., default roles for new users)
- **Serilog** - Logging configuration with console and Grafana Loki sinks

## Application Layer Patterns

### Service Organization
Services are organized by domain and version:
```
Application/
├── User/Admin/V10/
│   ├── UserAdminService.cs       # Main service class
│   ├── Command/                   # Input DTOs for write operations
│   │   ├── AddUserCommand.cs
│   │   └── UpdateUserCommand.cs
│   ├── Queries/                   # Input DTOs for read operations
│   │   └── GetUsersQuery.cs
│   ├── Dto/                       # Output DTOs
│   │   └── UserDto.cs
│   └── IntegrationEvents/         # Dapr pub/sub events
└── Role/Admin/V10/
    └── ...
```

### BaseService Authorization
All services inherit from `BaseService` which provides:
- `HasOrganizationPermissionAsync()` - Check if user can manage an organization (checks subtree)
- `CheckUserPermissionAsync()` - Verify permission to manage a specific user
- `VerifyRolePermissionAsync()` - Verify role assignment permissions
- Super-admin bypass via `Session.IsSupperAdmin()`

### Session Management
The `ISession` interface (implemented as `HttpSession`) provides:
- Current user ID (`UserId`)
- User display name (`UserDisplayName`)
- User roles (`Roles`)
- Authorization context for audit trails

## Important Implementation Notes

### Database Support
The codebase supports both MySQL and PostgreSQL. When writing raw SQL queries:
- Use parameterized queries to prevent SQL injection
- Prefer Dapper for complex queries (already configured)
- Recursive CTEs are used for organization hierarchy queries
- The `UseUnderScoreCase` option controls column naming (snake_case vs camelCase)

### Permission Model
- Organization permissions are **inherited**: administrators of a parent organization can manage all descendants
- This is implemented via path prefix matching: `/A/B` starts with `/A`
- The `OrganizationDetail` entity (view or table) contains pre-computed paths

### Integration Events
Services publish domain events via Dapr for cross-service communication:
- Events are defined in `IntegrationEvents/` folders
- Published using `DaprClient` from service scope
- Example: `UserAddedEvent`, `UserDisabledEvent`

### Error Handling
- Throw `WildGooseFriendlyException` for business logic errors (returns 200 with error code)
- Use standard .NET exceptions for system errors (returns 500)
- `GlobalExceptionFilter` converts exceptions to standardized responses

### Code Style
- Target Framework: .NET 10.0
- Nullable reference types enabled in some projects
- Unsafe blocks enabled in Domain and Application projects
- C# extension methods used extensively for clean syntax
- Internal visibility between projects enabled via `InternalsVisibleTo`
