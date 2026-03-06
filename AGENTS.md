# AGENTS.md

This file provides guidance to AI coding agents working in the WildGoose repository.

## Project Overview

WildGoose is a .NET 10 user and organization management API built with ASP.NET Core, Entity Framework Core, and Dapr. The application provides multi-tenant organization management with hierarchical structures, role-based access control, and user administration.

## Build, Test, and Run Commands

### Building the Solution
```bash
dotnet build WildGoose.sln
```

### Running All Tests
```bash
dotnet test src/WildGoose.Tests/WildGoose.Tests.csproj
```

### Running a Single Test
```bash
# Run by fully qualified test name
dotnet test --filter "FullyQualifiedName~UserAdminServiceTests.SuperAdminAddUserWithoutOrganization"

# Run all tests in a specific class
dotnet test --filter "FullyQualifiedName~UserAdminServiceTests"

# Run tests matching a pattern
dotnet test --filter "Name~AddUser"
```

### Running the Application
```bash
cd src/WildGoose
dotnet run
```

### Database Migrations
Migrations are created from the main WildGoose project with output to Infrastructure:
```bash
cd src/WildGoose
dotnet ef migrations add MigrationName -p ../WildGoose.Infrastructure
dotnet ef database update -p ../WildGoose.Infrastructure
```

Migrations are automatically applied on application startup.

## Solution Architecture

The solution follows a layered architecture with these projects:

- **WildGoose** - Main API project (Presentation/Web layer)
  - Controllers organized by API version (V10, V11, Admin)
  - Entry point: `Program.cs`

- **WildGoose.Domain** - Domain layer (Core business entities and interfaces)
  - Entity classes: `User`, `Role`, `Organization`, etc.
  - Domain interfaces: `ISession`, `IObjectStorageService`
  - Domain concepts: `ICreation`, `IModification`, `IDeletion` (audit trail)

- **WildGoose.Infrastructure** - Data access layer
  - `WildGooseDbContext` with automatic audit tracking
  - Supports MySQL and PostgreSQL

- **WildGoose.Application** - Application/Business logic layer
  - Services organized by domain (User, Role, Organization)
  - CQRS-style: Commands (write) and Queries (read)
  - All services inherit from `BaseService`

- **WildGoose.Tests** - Test project using xUnit

## Code Style Guidelines

### Target Framework and Language Features
- Target Framework: .NET 10.0
- Nullable reference types: enabled
- ImplicitUsings: enabled
- Use C# 12 primary constructors for dependency injection in services and controllers

### Naming Conventions
- **Classes, Methods, Properties**: PascalCase
- **Local variables, Parameters**: camelCase
- **Async methods**: End with `Async` suffix (e.g., `GetListAsync`, `AddAsync`)
- **Commands**: End with `Command` (e.g., `AddUserCommand`, `UpdateUserCommand`)
- **Queries**: End with `Query` (e.g., `GetUserQuery`, `GetUserListQuery`)
- **DTOs**: End with `Dto` (e.g., `UserDto`, `UserDetailDto`)

### File Organization
Services follow this structure:
```
Application/
├── User/Admin/V10/
│   ├── UserAdminService.cs        # Main service class
│   ├── Command/                    # Input DTOs for write operations
│   ├── Queries/                    # Input DTOs for read operations
│   ├── Dto/                        # Output DTOs
│   └── IntegrationEvents/          # Dapr pub/sub events
```

### Dependency Injection Pattern
Use primary constructor syntax:
```csharp
public class UserAdminService(
    WildGooseDbContext dbContext,
    ISession session,
    ILogger<UserAdminService> logger) : BaseService(dbContext, session, ...)
{
}
```

### Controllers Pattern
```csharp
[ApiController]
[Route("api/admin/v1.0/users")]
[Authorize(Policy = Defaults.SuperOrUserAdminOrOrgAdminPolicy)]
public class UserController(UserAdminService userAdminService) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<UserDto>> GetList([FromQuery] GetUserListQuery query)
    {
        return userAdminService.GetListAsync(query);
    }
}
```

## Error Handling

### Business Logic Errors
Throw `WildGooseFriendlyException` for business logic errors. These return HTTP 200 with error code:
```csharp
throw new WildGooseFriendlyException(1, "用户不存在");
throw new WildGooseFriendlyException(403, "权限不足");
```

### System Errors
Use standard .NET exceptions for system errors (returns HTTP 500).

### Global Exception Handling
`GlobalExceptionFilter` converts exceptions to standardized responses with format:
```json
{
  "success": false,
  "code": 1,
  "msg": "Error message"
}
```

## API Design Conventions

- **HTTP Methods**: Only GET and POST (no PUT/DELETE due to gateway restrictions)
- **Route Pattern**: `api/admin/v{version}/{resource}` or `api/v{version}/{resource}`
- **JSON Serialization**: camelCase property naming
- **Response Wrapping**: All responses wrapped by `ResponseWrapperFilter`

## Database Patterns

### Dual Database Support
Code supports both MySQL and PostgreSQL. When writing raw SQL:
- Use parameterized queries to prevent SQL injection
- Prefer Dapper for complex queries
- Recursive CTEs for organization hierarchy

### Entity Configuration
- Soft delete with `IsDeleted` flag and global query filters
- Automatic audit fields (CreationTime, CreatorId, etc.) applied in `SaveChangesAsync`
- Table prefix configurable via `DbOptions.TablePrefix`
- Snake_case column names optional via `DbOptions.UseUnderScoreCase`

### Organization Hierarchy
- Tree-based structure with recursive relationships
- Permission via path prefix matching: admin of `/A` can manage `/A/B`
- `OrganizationDetail` entity provides pre-computed path information

## Authorization

### Authorization Policies
- `SUPER` - Requires "admin" role only
- `SUPER_OR_ORG_ADMIN` - Requires "admin" or "organization-admin" role

### BaseService Authorization Methods
All services inherit from `BaseService` which provides:
- `CheckUserPermissionAsync(userId)` - Verify permission to manage a user
- `CanManageOrganizationAsync(orgId)` - Check if user can manage an organization
- `CheckAllRolePermissionAsync(roles)` - Verify role assignment permissions
- Super-admin bypass via `Session.IsSupperAdmin()` or `Session.IsSupperAdminOrUserAdmin()`

## Testing Guidelines

### Test Structure
```csharp
[Collection("WebApplication collection")]
public class UserAdminServiceTests(WebApplicationFactoryFixture fixture) : BaseTests
{
    [Fact]
    public async Task SuperAdminAddUserWithoutOrganization()
    {
        // Arrange
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);
        
        // Act & Assert
        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var user = await userAdminService.AddAsync(new AddUserCommand { ... });
        Assert.NotNull(user);
    }
}
```

### Test Naming
Method names should describe the scenario: `{Role}{Action}{Condition}`
- `SuperAdminAddUserWithoutOrganization`
- `OrganizationAdminGetUser3` (for edge cases)

### Test Data
- Use `BaseTests.CreateName()` for unique test names
- Use `BaseTests.GenerateChinesePhoneNumber()` for valid phone numbers
- Predefined constants for org/user IDs in `BaseTests`

## Integration Events

Services publish domain events via Dapr:
```csharp
await PublishEventAsync(_daprOptions, new UserAddedEvent { UserId = user.Id });
```

## Comments and Documentation

- Chinese comments are acceptable in this codebase
- XML documentation comments on public members encouraged
- Use `/// <summary>` style for public APIs
