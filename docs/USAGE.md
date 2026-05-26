# WildGoose 用户权限系统使用文档

> **文档版本：2.0**  
> **项目：WildGoose** — 基于 ASP.NET Core Identity 的用户、组织、权限管理系统  
> **后端：**.NET 10 + ASP.NET Core + Entity Framework Core  
> **前端：**React 19 + Ant Design 5 (ProLayout) + Vite 7 + TypeScript  
> **注意：**本系统仅支持 GET 和 POST 两种 HTTP 方法（DELETE 操作使用 `POST .../delete`）

---

## 目录

1. [系统概述](#1-系统概述)
2. [项目结构](#2-项目结构)
3. [配置说明](#3-配置说明)
   - [3.1 DbContext 数据库配置](#31-dbcontext-数据库配置)
   - [3.2 Identity 配置](#32-identity-配置)
   - [3.3 WildGoose 业务配置](#33-wildgoose-业务配置)
   - [3.4 JWT 认证配置](#34-jwt-认证配置)
   - [3.5 Dapr 配置](#35-dapr-配置)
   - [3.6 环境变量](#36-环境变量)
   - [3.7 CORS 配置](#37-cors-配置)
   - [3.8 会话参数前端配置](#38-会话参数前端配置)
4. [角色与权限](#4-角色与权限)
   - [4.1 内置角色](#41-内置角色)
   - [4.2 授权策略](#42-授权策略)
   - [4.3 权限声明模型](#43-权限声明模型)
   - [4.4 可授于角色机制](#44-可授于角色机制)
5. [认证机制](#5-认证机制)
   - [5.1 JWT Bearer 认证](#51-jwt-bearer-认证)
   - [5.2 X-AUTH-TOKEN 服务间认证](#52-x-auth-token-服务间认证)
   - [5.3 请求体加密](#53-请求体加密)
   - [5.4 OIDC 前端认证](#54-oidc-前端认证)
6. [API 接口文档](#6-api-接口文档)
   - [6.1 接口版本与权限策略](#61-接口版本与权限策略)
   - [6.2 用户管理接口 (Admin V1.0)](#62-用户管理接口-admin-v10)
   - [6.3 用户管理接口 (Admin V1.1)](#63-用户管理接口-admin-v11)
   - [6.4 角色管理接口 (Admin V1.0)](#64-角色管理接口-admin-v10)
   - [6.5 组织管理接口 (Admin V1.0)](#65-组织管理接口-admin-v10)
   - [6.6 用户自助接口 (V1.0)](#66-用户自助接口-v10)
   - [6.7 组织查询接口 (V1.0)](#67-组织查询接口-v10)
   - [6.8 权限验证接口 (V1.0)](#68-权限验证接口-v10)
7. [数据模型](#7-数据模型)
   - [7.1 核心表结构](#71-核心表结构)
   - [7.2 实体审计](#72-实体审计)
   - [7.3 软删除](#73-软删除)
   - [7.4 用户扩展属性](#74-用户扩展属性)
   - [7.5 组织层级与物化视图](#75-组织层级与物化视图)
8. [集成事件](#8-集成事件)
9. [Docker 部署](#9-docker-部署)
10. [附录：错误码](#10-附录错误码)
11. [附录：请求/响应示例](#11-附录请求响应示例)

---

## 1. 系统概述

### 1.1 核心功能

WildGoose 是一个基于 ASP.NET Core Identity 扩展的多租户用户、角色（权限）、组织（层级）管理系统。

| 功能模块 | 说明 |
|---------|------|
| **用户管理** | 用户的增删改查、密码重置、头像设置、禁用/启用、扩展属性 |
| **组织管理** | 层级组织架构管理（树形结构），支持多级嵌套 |
| **角色管理** | 角色 CRUD、可授于角色配置、权限声明（类 RAM 策略模型） |
| **权限控制** | 基于角色的访问控制 (RBAC)，支持组织级管理员，通配符资源/操作匹配 |
| **集成事件** | 通过 Dapr pub/sub 发布用户添加、删除、启用、禁用事件 |

### 1.2 设计理念

- **组织层级透明**：用户在全组织下的信息基本透明，任何登录用户都可以查看组织信息
- **路径前缀权限**：组织管理员的权限通过 `OrganizationDetail.Path.StartsWith()` 前缀匹配判断
- **三层管理角色**：
  - `admin`（超级管理员）— 最高权限
  - `user-admin`（用户管理员）— 管理用户和组织，无角色管理权限
  - `organization-admin`（组织管理员）— 管理所辖组织及下级组织的用户
- **ObjectId 字符串 ID**：使用 MongoDB.Bson 的 `ObjectId.GenerateNewId().ToString()` 生成
- **NId 自增数字**：仅用于组织树的 path 构建（ObjectId 过长不适合路径）

---

## 2. 项目结构

```
WildGoose/
├── src/
│   ├── WildGoose/                   # API 宿主项目
│   │   ├── Program.cs               # 入口、中间件管道、服务注册
│   │   ├── AuthenticationExtensions.cs  # JWT + X-AUTH-TOKEN 双重认证配置
│   │   ├── Controllers/
│   │   │   ├── Admin/V10/           # 管理员接口 V1.0（User/Role/Organization）
│   │   │   ├── Admin/V11/           # 管理员接口 V1.1（简化用户创建）
│   │   │   └── V10/                 # 用户自助接口（User/Organization/Permission）
│   │   ├── Filters/
│   │   │   ├── ResponseWrapperFilter.cs  # 统一响应包裹 {Code, Success, Data/Msg}
│   │   │   ├── GlobalExceptionFilter.cs  # 全局异常处理
│   │   │   └── TokenAuthHandler.cs       # X-AUTH-TOKEN 认证处理器
│   │   ├── Middlewares/
│   │   │   └── DecryptRequestMiddleware.cs  # AES-ECB 请求体解密中间件
│   │   └── Jwt/                       # JWT 相关（JwtBearerSettings, RSAParametersInfo）
│   ├── WildGoose.Application/       # 业务逻辑层
│   │   ├── Services/
│   │   │   ├── BaseService.cs        # 抽象基类：权限检查、Dapr 事件、组织查询
│   │   │   ├── Admin/User/V10/       # 用户管理 CRUD（含 IntegrationEvents）
│   │   │   ├── Admin/User/V11/       # 简化用户创建（内部服务调用）
│   │   │   ├── Admin/Role/V10/       # 角色管理 CRUD + 可授于角色
│   │   │   ├── Admin/Organization/V10/ # 组织树 CRUD + 管理员管理
│   │   │   ├── User/V10/             # 用户自助操作（密码重置）
│   │   │   └── Organization/V10/     # 只读组织查询
│   │   ├── WildGooseDbContext.cs     # EF Core DbContext（IdentityDbContext）
│   │   ├── SeedData.cs               # 启动时种子数据（默认角色、admin 用户）
│   │   ├── HttpSession.cs            # ISession 实现（从 HttpContext claims 读取）
│   │   └── Migrations/               # EF Core 迁移文件
│   ├── WildGoose.Domain/            # 领域层
│   │   ├── Entity/                   # 实体（User, Role, Organization 等）
│   │   ├── Options/                  # 配置 POCO 类
│   │   ├── ErrorCodes.cs             # 统一错误码定义
│   │   └── Defaults.cs               # 系统常量 & 默认值
│   ├── WildGoose.Web/               # 前端 SPA
│   │   ├── src/pages/                # UserPage.tsx, RolePage.tsx
│   │   ├── src/services/wildgoose/   # API 调用函数
│   │   ├── src/lib/                  # auth.ts, request.ts, utils.ts
│   │   └── public/config.js          # 运行时前端配置
│   └── WildGoose.Tests/             # xUnit 集成测试
├── api.Dockerfile                   # 后端 Docker 构建
├── web.Dockerfile                   # 前端 Docker 构建
└── docker-entrypoint.sh             # 配置模板渲染 + 可选 Dapr sidecar
```

---

## 3. 配置说明

### 3.1 DbContext 数据库配置

**绑定类：** `WildGoose.Domain.Options.DbOptions`  
**配置节：** `"DbContext"`  
**注册方式：** `builder.Services.Configure<DbOptions>(builder.Configuration.GetSection("DbContext"))`

```json5
{
  "DbContext": {
    "DatabaseType": "PostgreSql",          // 数据库类型：PostgreSql 或 MySql（大小写不敏感）
    "AutoMigrationEnabled": true,           // 启动时自动执行 EF Core/数据库 迁移
    "TablePrefix": "wild_goose_",           // 所有数据库表的前缀
    "UseUnderScoreCase": true,              // 是否使用下划线命名（如 OrganizationUser → organization_user）
    "EnableSensitiveDataLogging": false,    // 是否启用敏感数据日志（开发调试用）
    "ConnectionString": "User ID=postgres;Password=xxx;Host=localhost;Port=5432;Database=wildgoose;Pooling=true;",
    "TableMapper": {                        // 表名映射（兼容现有数据库）
      "organization_user2": "user_organization"
    }
  }
}
```

#### 属性详细说明

| 属性 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| `DatabaseType` | string | ✅ | — | `"PostgreSql"` 或 `"MySql"`。对应 UseNpgsql / UseMySql 提供程序 |
| `AutoMigrationEnabled` | bool | ✅ | — | `true` 时启动自动执行 `dbContext.Database.MigrateAsync()` |
| `TablePrefix` | string | ✅ | — | 所有表的统一前缀，同时用于 `MigrationsHistoryTable` |
| `UseUnderScoreCase` | bool | — | `false` | 实体名 → 下划线表名转换。如 `OrganizationUser` → `organization_user` |
| `EnableSensitiveDataLogging` | bool | — | `false` | 仅用于开发，生产环境不应启用 |
| `ConnectionString` | string | ✅ | — | 数据库连接字符串 |
| `TableMapper` | Dictionary | — | `null` | 键=代码实体名, 值=实际表名。当现有数据库表名与实体名不一致时使用 |

#### TableMapper 说明

`TableMapper` 用于当现有数据库表名与代码中的实体名不一致时进行映射。代码中使用 `user_organization`，但现有表中是 `organization_user2`，则：

```json
{
  "TableMapper": {
    "organization_user2": "user_organization"
  }
}
```

> 注：键需要匹配派生类中使用的表名，系统通过 `UseUnderScoreCase` + 实体名推导后查找映射。

### 3.2 Identity 配置

**绑定类：** `Microsoft.AspNetCore.Identity.IdentityOptions`（标准 Identity 配置）  
**绑定类（扩展）：** `WildGoose.Domain.Options.IdentityExtensionOptions` — 包含 `DefaultRoles`  
**配置节：** `"Identity"`  
**注册方式：** `builder.Services.Configure<IdentityOptions>(identity)` 同时 `services.Configure<IdentityExtensionOptions>(identity)`

```json5
{
  "Identity": {
    "Password": {
      "RequireDigit": false,                // 是否需要数字
      "RequireLowercase": false,            // 是否需要小写字母
      "RequireNonAlphanumeric": false,      // 是否需要特殊字符
      "RequireUppercase": false,            // 是否需要大写字母
      "RequiredLength": 0,                  // 最小密码长度（0 表示不限制）
      "RequiredUniqueChars": 1              // 唯一字符数量
    },
    "SignIn": {
      "RequireConfirmedEmail": false,       // 是否需要邮箱验证
      "RequireConfirmedAccount": false,     // 是否需要账户确认
      "RequireConfirmedPhoneNumber": false  // 是否需要手机验证
    },
    "Lockout": {
      "DefaultLockoutTimeSpan": "00:05:00", // 锁定时长
      "MaxFailedAccessAttempts": 5,         // 最大失败次数
      "AllowedForNewUsers": false           // 新用户是否可以被锁定
    },
    "User": {
      "AllowedUserNameCharacters": "^[a-zA-Z0-9\\u4e00-\\u9fa5@]+$", // 允许的用户名字符（正则）
      "RequireUniqueEmail": false                                     // 是否要求邮箱唯一
    }
  }
}
```

#### Identity:Password 快速参考

| 配置项 | 说明 | 典型值 |
|--------|------|--------|
| `RequiredLength` | 最小密码长度，设为 0 表示不限制 | `0` 或 `8` |
| `RequireDigit` | 是否必须包含数字 | `false` |
| `RequireLowercase` | 是否必须包含小写字母 | `false` |
| `RequireUppercase` | 是否必须包含大写字母 | `false` |
| `RequireNonAlphanumeric` | 是否必须包含特殊字符 | `false` |

> ⚠️ 当 `DISABLE_PASSWORD_LOGIN=true` 时，密码策略不生效，由 `NoopPasswordValidator` 接管。

#### IdentityExtensionOptions（代码中存在但配置中可选）

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `DefaultRoles` | `string[]` | `[]` | 默认角色列表。当前代码未在生产中使用此配置（角色在 SeedData 中硬编码） |

> **注意：** `IdentityExtensionOptions.DefaultRoles` 虽然可以通过 `"Identity"` 节配置，但当前版本的角色播种逻辑（`SeedData.cs`）直接硬编码了三个系统角色（admin、organization-admin、user-admin），未读取此配置。

### 3.3 WildGoose 业务配置

**绑定类：** `WildGoose.Domain.Options.WildGooseOptions`  
**配置节：** `"WildGoose"`  
**注册方式：** `builder.Services.Configure<WildGooseOptions>(builder.Configuration.GetSection("WildGoose"))`

```json5
{
  "WildGoose": {
    "AddUserRoles": ["内部添加用户"],
    "UserPropertyMappings": {
      "Title": { "Column": "Property01", "DisplayName": "职位" },
      "EmployeeId": { "Column": "Property02", "DisplayName": "工号" }
    }
  }
}
```

#### 属性详细说明

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `AddUserRoles` | `string[]` | `[]` | 定义哪些角色的用户可以使用 V1.1 简化添加用户接口 |
| `UserPropertyMappings` | `Dictionary<string, UserPropertyMapping>` | `{}` | 用户扩展属性映射字典 |

#### WildGoose:AddUserRoles 说明

控制 **V1.1 简化添加用户接口** (`POST /api/admin/v1.1/users`) 的访问权限：

- **空数组 `[]`**：任何已认证用户都可以调用
- **非空数组**：只有拥有数组中指定角色的用户才能调用，否则返回 403

实现逻辑（`UserAdminService.V11`）：
```csharp
var options = wildGooseOptions.Value;
if (options.AddUserRoles.Length == 0 || !Session.Roles.Any(x => options.AddUserRoles.Contains(x)))
{
    throw new WildGooseFriendlyException(403, "访问受限");
}
```

#### WildGoose:UserPropertyMappings 说明

用户扩展属性映射将逻辑属性名映射到数据库的 `UserExtension.Property01` ~ `Property09` 字段，使前端可以通过逻辑名称读取/写入用户扩展属性：

- **键**：逻辑属性名称（前端传入，如 `"Title"`、`"EmployeeId"`）
- **值**：包含存储字段名和显示名称

`UserPropertyMapping` 结构：

| 属性 | 类型 | 必需 | 说明 |
|------|------|------|------|
| `Column` | string | ✅ | 存储字段名，必须是 `Property01` ~ `Property09` |
| `DisplayName` | string | ✅ | 前端显示名称（如 "职位"、"工号"） |

> `UserExtension` 实体提供 `Property01` ~ `Property09` 共 9 个扩展字段，通过此映射将逻辑属性与物理存储关联。

### 3.4 JWT 认证配置

**绑定类：** `JwtBearerSettings`（位于 `src/WildGoose/Jwt/JwtBearerSettings.cs`）  
**配置节：** `"JwtBearer"`  
**读取方式：** `configuration.GetSection("JwtBearer").Get<JwtBearerSettings>()`

```json5
{
  "JwtBearer": {
    "Authority": "http://localhost:8099",       // OIDC Authority 地址
    "RequireHttpsMetadata": false,              // 是否要求 HTTPS 元数据
    "ValidateAudience": false,                  // 是否验证 Audience
    "ValidateIssuer": false,                    // 是否验证 Issuer
    "ValidateLifetime": true,                   // 是否验证 Token 生命周期
    "ValidIssuer": null,                        // 合法颁发者（ValidateIssuer=true 时需设置）
    "ValidAudience": null,                      // 合法受众（ValidateAudience=true 时需设置）
    "MetadataAddress": null,                    // OIDC 元数据地址（留空则自动从 Authority 发现）
    "KeyPath": "jwt.jwk",                       // RSA 私钥 JWK 文件路径（可选）
    "Key": null                                 // 内联 RSA 密钥参数（可选，与 KeyPath 二选一）
  }
}
```

#### 详细说明

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Authority` | string | `null` | OIDC 签发者地址 |
| `RequireHttpsMetadata` | bool | `true` | 是否要求 OIDC 元数据通过 HTTPS 获取 |
| `ValidateAudience` | bool | `true` | 是否验证 `aud` 声明 |
| `ValidateIssuer` | bool | `true` | 是否验证 `iss` 声明 |
| `ValidIssuer` | string | `null` | 合法的 Issuer 值 |
| `ValidAudience` | string | `null` | 合法的 Audience 值 |
| `ValidateLifetime` | bool | `true` | 是否验证 Token 过期时间 |
| `MetadataAddress` | string | `null` | OIDC 发现地址。如果 `RequireHttpsMetadata=false` 且 `Authority` 是 HTTPS，会自动将 `Authority` 替换为 HTTP 构造元数据地址 |
| `KeyPath` | string | `null` | 指向本地 JWK JSON 文件的路径。非空时从文件加载 RSA 密钥，同时会禁用 `Authority` 发现（`ConfigurationManager = null`） |
| `Key` | `RSAParametersInfo` | `null` | 内联 RSA 密钥参数（JWK 格式）。当 `KeyPath` 和 `Key` 均为空时，会使用 `Authority` 进行 OIDC 发现 |

#### RSAParametersInfo（内联密钥格式）

当使用 `Key` 属性配置 RSA 密钥时使用此格式：

```json
{
  "Key": {
    "kty": "RSA",
    "alg": "RS256",
    "n": "modulus_base64url",
    "e": "AQAB",
    "d": "private_exponent_base64url",
    "p": "prime1_base64url",
    "q": "prime2_base64url",
    "dp": "exponent1_base64url",
    "dq": "exponent2_base64url",
    "qi": "coefficient_base64url"
  }
}
```

> **私钥内容（D, P, Q, DP, DQ, Qi）**仅为 Token 签发端所需。仅验证 Token 时可只提供 `n` 和 `e`（公钥即可）。

#### ApiName（应用标识）

**读取方式：** `builder.Configuration["ApiName"]`

```json
{
  "ApiName": "sample-wildgoose-api"
}
```

此值用于 JWT 的 `scope` 声明验证。所有授权策略（SUPER、USER_ADMIN、SUPER_OR_USER_ADMIN_OR_ORG_ADMIN）均要求 JWT 包含 `"scope": "{ApiName}"` 声明。如果未配置或为空，系统启动时将抛出 `8001` 错误（ApiName 为空）。

### 3.5 Dapr 配置

**绑定类：** `WildGoose.Domain.Options.DaprOptions`  
**配置节：** `"Dapr"`  
**注册方式：** `builder.Services.Configure<DaprOptions>(builder.Configuration.GetSection("Dapr"))`

```json5
{
  "Dapr": {
    "pubsub": ""          // Dapr pub/sub 组件名称
  }
}
```

| 属性 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| `Pubsub` | string | — | `null` | Dapr pub/sub 组件名称。为空时集成事件功能不生效 |

集成事件：`BaseService.PublishEventAsync` 通过 Dapr Client 发布以下事件：

| 事件类 | CloudEvent 类型 | 触发时机 |
|--------|----------------|----------|
| `UserAddedEvent` | — | 用户创建后 |
| `UserDeletedEvent` | — | 用户删除后 |
| `UserDisabledEvent` | — | 用户禁用后 |
| `UserEnabledEvent` | — | 用户启用后 |

> 注意：Dapr 集成需要在宿主机上运行 Dapr sidecar，当前代码中 `DaprClient` 从 DI 获取，如未注入则返回 `null`，不会发布事件。

### 3.6 环境变量

| 环境变量 | 说明 | 默认值 | 受影响的配置 |
|---------|------|--------|-------------|
| `DISABLE_PASSWORD_LOGIN` | 禁用密码登录。设为 `true` 时替换密码验证器为 `NoopPasswordValidator`，替换 UserStore 为 `NoPasswordStore` | `false` | `Defaults.DisablePasswordLogin` |
| `ENABLE_SM3_PASSWORD_HASHER` | 启用国密 SM3 密码哈希（`Identity.Sm` 包提供） | `false` | `builder.Services.AddSm3PasswordHasher<User>()` |
| `HEALTH_CHECK_PATH` | 健康检查端点路径 | `/healthz` | `app.UseHealthChecks(healthCheckPath)` |
| `WildGooseSecurityToken` | X-AUTH-TOKEN 认证的安全令牌（见 5.2 节） | `""`（空字符串 = 不启用） | `TokenAuthHandler` |
| `ADMIN_PASSWORD` | 首次启动时 admin 用户的密码。为空则自动生成随机密码并在控制台输出 | 自动生成 | `SeedData.Init` |
| `CONFIG_SOURCE` | Docker 启动时配置模板文件路径（见 9.1 节） | — | `docker-entrypoint.sh` |
| `DAPR_URL` | Dapr sidecar 二进制下载地址（Docker 启动时） | — | `docker-entrypoint.sh` |

### 3.7 CORS 配置

**配置节：** `"AllowedCorsOrigins"`（数组，从 `appsettings.json` 读取）

```json
{
  "AllowedCorsOrigins": ["http://localhost:5173", "https://app.example.com"]
}
```

如果未配置或为空数组，默认回退为 `["http://localhost:5173"]`（Vite 开发服务器默认地址）。

CORS 策略特点：
- 允许跨域携带凭据（`AllowCredentials()`）
- 预检请求缓存 7 天（`SetPreflightMaxAge(TimeSpan.FromDays(7))`）
- 全局开放（建议在网关层面统一控制）

### 3.8 运行时前端配置

**文件：** `src/WildGoose.Web/public/config.js`

```js
window.wildgoose = {
  applicationId: '1',                              // 应用 ID
  baseName: '${BASE_PATH}',                         // 基础路径（Docker 部署时由 env var 替换）
  backend: 'http://localhost:5600/api',             // 后端 API 地址
  pageSize: 10,                                     // 默认分页大小
  enableEncryption: true,                           // 是否启用请求体 AES 加密
  disablePasswordLogin: false,                      // 禁用密码登录（对应 DISABLE_PASSWORD_LOGIN）
  icp: '',                                          // ICP 备案号（页脚显示）
  copyright: '',                                    // 版权信息（页脚显示）
  oidc: {                                           // OIDC 客户端配置
    authority: 'https://sample.ptkj.cc/sts',
    client_id: 'sample-app',
    client_secret: 'secret',
    response_type: 'code',
    redirect_uri: 'http://localhost:5174/signin-redirect-callback',
    scope: 'openid profile role sample-wildgoose-api',
    // ... 更多 OIDC 配置项
  },
}
```

Docker 部署时，`docker-entrypoint.sh` 会自动替换 `${BASE_PATH}`、`${BACKEND}`等 `${VAR}` 占位符，并对 `config.js` 做 MD5 版本化缓存。

> **注意：** `yarn.lock` 被 `.gitignore` 忽略，前端构建可能不可复现。

---

## 4. 角色与权限

### 4.1 内置角色

系统内置三个固定角色，不可添加、修改或删除：

| 角色名 | 规范化名称 | 说明 | 权限 |
|--------|-----------|------|------|
| `admin` | `ADMIN` | 超级管理员 | 最高权限，可管理所有功能。自动拥有 `SUPER` 策略 |
| `user-admin` | `USER-ADMIN` | 用户管理员（代码中常量名为 `BusinessAdmin`） | 管理用户和组织，无角色管理权限。自动拥有用户管理权限 |
| `organization-admin` | `ORGANIZATION-ADMIN` | 组织管理员 | 仅可管理所辖组织及其下级组织的用户 |

> **注意**：`user-admin` 在 `Defaults.cs` 中常量名为 `BusinessAdmin`，但在 JWT `role` 声明和授权策略中使用 `"user-admin"`。

三个系统角色的 Statement：
- **admin**：`[{"Effect":"Allow","Resource":["*"],"Action":["*"]}]` — 完全控制
- **organization-admin**：`[]` — 空声明
- **user-admin**：`[]` — 空声明

### 4.2 授权策略

系统使用 ASP.NET Core Policy 授权，通过 `AuthenticationExtensions.cs` 注册：

| Policy 名称 | 常量 | 允许的角色 | JWT 要求 |
|-------------|------|-----------|----------|
| `SUPER` | `Defaults.SuperPolicy` | `admin` | Bearer Token 或 X-AUTH-TOKEN，scope 包含 `ApiName` |
| `SUPER_OR_USER_ADMIN_OR_ORG_ADMIN` | `Defaults.SuperOrUserAdminOrOrgAdminPolicy` | `admin`, `user-admin`, `organization-admin` | 同上 |
| `USER_ADMIN` | `"USER_ADMIN"` | `user-admin` | 同上 |

授权同时支持 `JwtBearerDefaults.AuthenticationScheme` 和 `"SecurityToken"`（X-AUTH-TOKEN）两种认证方案。

### 4.3 权限声明模型

```csharp
// Statement 结构
{
  "Effect": "Allow",       // 授权效果：Allow | Deny
  "Resource": ["*"],       // 资源匹配模式（支持通配符 * 和 ?）
  "Action": ["*"]          // 操作匹配模式（支持通配符 * 和 ?）
}
```

- `*` 表示 0 个或多个英文字母、数字、`:`、`/`
- `?` 表示 1 个任意的英文字母、数字、`:`、`/`
- 例如：`ecs:Describe*` 表示所有以 `ecs:Describe` 开头的操作
- 多个 Statement 组成列表，系统逐个匹配，`Deny` 优先于 `Allow`

### 4.4 可授于角色机制

- 每个角色可以配置"可授于角色"列表（`RoleAssignableRole` 表）
- 可授于角色不考虑继承关系（不会递归查找）
- 超级管理员 (`admin`) 无需配置可授于角色
- 用户管理员 (`user-admin`) 无需配置可授于角色
- 组织管理员添加用户时，只能分配其所有角色的可授于角色列表的并集
- `admin` 和 `user-admin` 在查询可授于角色时，系统返回所有角色（`organization-admin` 除外）

---

## 5. 认证机制

### 5.1 JWT Bearer 认证

**主认证方案**。系统验证 JWT 的以下内容：

1. **签名**：通过 OIDC 发现获取 JWK，或使用本地配置的 RSA 公钥验证
2. **Scope**：JWT 必须包含 `"scope"` 声明且值等于配置的 `ApiName`
3. **Role**：根据端点要求的策略，JWT 必须包含相应的 `role` 声明（`admin` / `user-admin` / `organization-admin`）
4. **Audience/Issuer**（可选）：通过 `JwtBearer:ValidateAudience` / `JwtBearer:ValidateIssuer` 控制

**双模式密钥**：
1. **OIDC 发现模式**：配置 `Authority`，系统自动从 OIDC 元数据获取公钥
2. **本地 JWK 模式**：配置 `KeyPath` 指向本地 JWK 文件，禁用 OIDC 发现

### 5.2 X-AUTH-TOKEN 服务间认证

**辅助认证方案**，用于服务间调用（无需 OIDC 流程）。

- **认证头**：请求头 `X-AUTH-TOKEN` 中传入安全令牌
- **令牌值**：由环境变量 `WildGooseSecurityToken` 设置
- **角色指定**：通过请求头 `X-AUTH-ROLE` 指定角色（逗号分隔，经 URL 编码）
- **用户标识**：固定为 `"SecurityToken"`（sub 声明）

如果不设置 `WildGooseSecurityToken`（空字符串），此认证方案不生效。

### 5.3 请求体加密

**中间件：** `DecryptRequestMiddleware`

前端可通过配置 `window.wildgoose.enableEncryption = true` 启用请求体 AES-ECB 加密。

**加密流程：**
1. 前端生成随机 UUID 作为 AES 密钥
2. 在密钥第 10 位后插入 6 位随机字符混淆
3. 请求头添加 `Z-Encrypt-Version: v1.1` 和 `Z-Encrypt-Key: {混淆后的密钥}`
4. 请求体使用 AES-ECB 加密后发送

**解密流程（后端）：**
1. 检查 `Z-Encrypt-Version` 和 `Z-Encrypt-Key` 请求头
2. 如果 v1.1，从混淆后的密钥中提取真实密钥（去掉第 10-15 位）
3. 使用 AES-ECB 解密请求体
4. 覆盖 `HttpRequest.Body` 为解密后的内容

> **强制加密**：`/password` 结尾的请求如果没有传加密版本号和密钥，后端直接返回 403。密码修改接口必须加密传输。

### 5.4 OIDC 前端认证

前端使用 `oidc-client` 库实现 OIDC code flow：

1. 用户访问 SPA → `main.tsx` 检查是否已认证
2. 未认证 → 重定向到 OIDC Provider 登录页
3. 认证成功后 → 回调到 SPA，获取 access_token 和 id_token
4. 后续 API 请求在 Axios 拦截器中自动注入 `Authorization: Bearer {access_token}`
5. 用户角色从 `user.profile.role` 读取（支持字符串或数组）

`public/config.js` 中的 `oidc` 对象配置了完整的 OIDC 客户端参数，包括：
- `authority`：OIDC Provider 地址
- `metadata`：手动指定的端点地址（当禁用自动发现时使用）
- `signingKeys`：直接指定的 RSA 公钥（JWK 格式，用于验证 token 签名）

---

## 6. API 接口文档

### 6.1 接口版本与权限策略

| 版本 | 路径前缀 | 控制器位置 | 用途 | 默认授权 |
|------|---------|-----------|------|---------|
| V1.0 | `/api/v1.0/*` | `Controllers/V10/` | 用户自助接口（需登录） | `[Authorize]` |
| V1.0 | `/api/admin/v1.0/*` | `Controllers/Admin/V10/` | 管理员完整接口 | `[Authorize(Policy = "SUPER_OR_USER_ADMIN_OR_ORG_ADMIN")]` |
| V1.1 | `/api/admin/v1.1/*` | `Controllers/Admin/V11/` | 管理员简化接口 | `[Authorize]` + 业务角色校验 |

#### 响应格式

所有响应自动包裹为统一格式（由 `ResponseWrapperFilter` 实现）：

```json5
// 成功
{ "code": 0, "success": true, "data": { ... } }

// 失败
{ "code": 4001, "success": false, "msg": "用户不存在", "errors": [...] }
```

#### 分页参数

使用分页的接口统一接受以下查询参数：

| 参数 | 类型 | 说明 |
|------|------|------|
| `page` | int | 页码，从 1 开始 |
| `limit` | int | 每页数量 |
| `q` | string | 关键词搜索（可选） |

分页响应格式：

```json5
{
  "success": true,
  "data": {
    "data": [...],
    "page": 1,
    "limit": 10,
    "total": 100
  }
}
```

### 6.2 用户管理接口 (Admin V1.0)

**控制器：** `UserController` → `UserAdminService`  
**路由：** `/api/admin/v1.0/users`  
**授权策略：** `SUPER_OR_USER_ADMIN_OR_ORG_ADMIN`（admin、user-admin、organization-admin 均可访问）

#### 获取用户列表

```
GET /api/admin/v1.0/users?page=1&limit=10&organizationId=xxx&status=all&q=keyword&isRecursive=true
```

**查询参数：**

| 参数 | 类型 | 必需 | 说明 |
|------|------|------|------|
| `page` | int | ✅ | 页码 |
| `limit` | int | ✅ | 每页大小 |
| `organizationId` | string | — | 组织 ID。为空时 admin/user-admin 查所有用户，org-admin 查其可管理的所有用户 |
| `status` | string | — | 筛选状态：`all`（全部）、`enabled`（已启用）、`disabled`（已禁用） |
| `q` | string | — | 搜索关键词（匹配账号、手机号） |
| `isRecursive` | bool | — | 仅在 `organizationId` 不为空时生效。`true`=查询含下级组织，`false`=仅本级 |

**权限逻辑：**
- `organizationId` 为空 → admin/user-admin 查询**所有用户**，org-admin 查询其**有管理权限（含下级）的所有用户**
- `organizationId` 不为空 → `isRecursive` 控制递归查询，org-admin 需要有此组织的管理权限

#### 添加用户

```
POST /api/admin/v1.0/users
Content-Type: application/json

{
  "userName": "zhangsan",
  "name": "张三",
  "email": "zhangsan@example.com",
  "phoneNumber": "13800138000",
  "organizationIds": ["org-001", "org-002"],
  "roleNames": ["employee"],
  "password": "P@ssw0rd",
  "properties": {
    "Title": "工程师",
    "EmployeeId": "EMP001"
  }
}
```

**权限逻辑：**
- admin、user-admin：可添加任意用户，可不设置组织、角色
- organization-admin：只能添加在其管理范围内的用户（组织不可为空），且角色需在管理员所有角色的可授于角色列表中

#### 获取用户详情

```
GET /api/admin/v1.0/users/{id}
```

**权限逻辑：**
- admin、user-admin：可查询任意用户
- organization-admin：仅可查询其管理范围内的用户

**响应字段说明：**（`UserDetailDto`）

| 字段 | 类型 | 说明 |
|------|------|------|
| `userName` | string | 用户名 |
| `name` | string | 姓名 |
| `phoneNumber` | string | 手机号 |
| `email` | string | 邮箱 |
| `code` | string | 编号 |
| `title` | string | 职位（来自 UserExtension） |
| `departureTime` | number | 离职时间戳（Unix epoch 秒） |
| `hiddenSensitiveData` | bool | 是否隐藏敏感数据 |
| `organizations` | `OrganizationDto[]` | 所在的所有部门 |
| `roles` | `RoleBasicDto[]` | 拥有的所有角色 |
| `properties` | `ExtProperty[]` | 扩展属性列表 |

#### 更新用户

```
POST /api/admin/v1.0/users/{id}
Content-Type: application/json

{
  "userName": "zhangsan",
  "name": "张三",
  "email": "zhangsan@example.com",
  "phoneNumber": "13800138000",
  "organizationIds": ["org-001", "org-002"],
  "roleNames": ["employee"],
  "properties": { "Title": "高级工程师" }
}
```

**权限逻辑：**
- admin、user-admin：可修改任意用户，可不设置组织和角色
- organization-admin：只能修改其管理范围内的用户。新增的角色必须在可授于角色列表中，删除的角色不做权限校验

#### 删除用户

```
POST /api/admin/v1.0/users/{id}/remove
```

同时清除用户的所有组织映射关系、角色关联。删除时发布 `UserDeletedEvent`。

#### 启用/禁用用户

```
POST /api/admin/v1.0/users/{id}/enable
POST /api/admin/v1.0/users/{id}/disable
```

**权限逻辑：**
- admin、user-admin：可操作任意用户
- organization-admin：仅可操作其管理范围内的用户

**禁用注意事项：**
- 禁用是全局的（设置 `LockoutEnd` 为未来时间）
- 即使有其他领域共享 `wild_goose_user` 表，禁用也会影响所有领域
- 禁止禁用自己（返回错误码 `4003`）

禁用/启用时分别发布 `UserDisabledEvent` / `UserEnabledEvent`。

#### 设置头像

```
POST /api/admin/v1.0/users/{id}/picture
```

更新用户的 `Picture` 字段（当前实现中直接设置 URL 字符串）。

#### 修改密码

```
POST /api/admin/v1.0/users/{id}/password
Content-Type: application/json

{
  "newPassword": "NewP@ssw0rd",
  "confirmPassword": "NewP@ssw0rd"
}
```

**安全要求：**
- 请求体必须经过 AES-ECB 加密传输
- 请求头必须包含 `Z-Encrypt-Version` 和 `Z-Encrypt-Key`
- 未加密的请求直接返回 403

**权限逻辑：**
- admin、user-admin：可修改任意用户密码
- organization-admin：仅可修改其管理范围内用户的密码

### 6.3 用户管理接口 (Admin V1.1)

**控制器：** `UserController`（Controllers/Admin/V11）→ `UserAdminService`（V11）  
**路由：** `/api/admin/v1.1/users`  
**授权：** `[Authorize]` + 业务角色校验（`WildGooseOptions.AddUserRoles` 配置）

#### 简化添加用户

```
POST /api/admin/v1.1/users
Content-Type: application/json

{
  "userName": "zhangsan",
  "name": "张三",
  "phoneNumber": "13800138000",
  "password": "P@ssw0rd"
}
```

**与 V1.0 添加用户的区别：**
- 不需要设置组织、角色
- 字段更少（无 email、organizationIds、roleNames、properties 等）
- 适用于内部服务调用、批量同步场景

**访问控制：**
- 根据 `WildGoose:AddUserRoles` 配置判断（见 3.3 节）
- 配置为空数组 → 任何已认证用户可调用
- 配置非空数组 → 只有拥有指定角色的用户可调用

### 6.4 角色管理接口 (Admin V1.0)

**控制器：** `RoleController` → `RoleAdminService`  
**路由：** `/api/admin/v1.0/roles`  
**授权策略：** `SUPER`（仅 admin 可管理角色）。可授于角色查询接口对所有已认证用户开放

#### 获取角色列表

```
GET /api/admin/v1.0/roles?page=1&limit=10&q=
```

**权限：** 仅 admin  
**返回字段：**

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 角色 ID |
| `name` | string | 角色名称 |
| `description` | string | 角色备注 |
| `statement` | string | 权限声明 JSON |
| `version` | int | 权限策略版本号 |
| `assignableRoles` | `AssignableRoleDto[]` | 可授于角色列表 |
| `lastModificationTime` | string | 最后修改时间 |

#### 添加角色

```
POST /api/admin/v1.0/roles
Content-Type: application/json

{
  "name": "custom-role",
  "description": "自定义角色"
}
```

**限制：**
- 不允许添加已存在的角色名称
- 不允许使用系统角色名（`admin`、`organization-admin`、`user-admin`），返回错误码 `6004`

#### 获取角色详情

```
GET /api/admin/v1.0/roles/{id}
```

返回与列表相同的 `RoleDto` 结构。

#### 更新角色

```
POST /api/admin/v1.0/roles/{id}
Content-Type: application/json

{
  "name": "new-role-name",
  "description": "新的描述"
}
```

**限制：** 不允许修改三个系统角色（admin、organization-admin、user-admin），返回错误码 `6003`。

#### 删除角色

```
POST /api/admin/v1.0/roles/{id}/delete
```

**限制：**
- 不允许删除三个系统角色
- 同时删除该角色在用户角色表、可授于角色表中的所有关联记录

#### 更新角色权限声明

```
POST /api/admin/v1.0/roles/{id}/statement
Content-Type: application/json

{
  "statement": "[{\"Effect\":\"Allow\",\"Resource\":[\"*\"],\"Action\":[\"*\"]}]"
}
```

**限制：** 不允许修改三个系统角色的声明。

#### 添加可授于角色

```
POST /api/admin/v1.0/roles/{id}/assignableRoles
Content-Type: application/json

{
  "assignableRoleIds": ["role-id-1", "role-id-2"]
}
```

**限制：** 不允许将三个系统角色设为可授于角色。

#### 删除可授于角色

```
POST /api/admin/v1.0/roles/{id}/assignableRoles/{assignableRoleId}/delete
```

#### 获取当前用户可授于角色列表

```
GET /api/admin/v1.0/roles/assignableRoles
```

**权限：** 所有已认证用户均可查询  
**逻辑：**
- admin、user-admin → 返回所有角色（不含 organization-admin）
- 其他用户 → 返回其所有角色的可授于角色的并集

### 6.5 组织管理接口 (Admin V1.0)

**控制器：** `OrganizationController` → `OrganizationAdminService`  
**路由：** `/api/admin/v1.0/organizations`  
**授权策略：** `SUPER_OR_USER_ADMIN_OR_ORG_ADMIN`

#### 获取子组织列表

```
GET /api/admin/v1.0/organizations/subList?parentId=
```

**逻辑：**
- `parentId` 为空 → admin 查询顶级机构，org-admin 查询其有权管理的最顶级机构
- `parentId` 不为空 → 查询指定组织的直接子级

**响应：** `SubOrganizationDto[]`

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 组织 ID |
| `name` | string | 组织名称 |
| `parentId` | string | 父组织 ID |
| `hasChild` | bool | 是否有子组织 |
| `code` | string | 组织编号 |

#### 搜索组织

```
GET /api/admin/v1.0/organizations/search?keyword=xxx
```

**响应：** `SearchOrganizationResultItemDto[]`

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 组织 ID |
| `name` | string | 组织名称 |
| `parentId` | string | 父组织 ID |
| `hasChild` | bool | 是否有子组织 |
| `path` | string[] | 路径名称数组 |
| `fullName` | string | 完整名称路径 |

#### 添加组织

```
POST /api/admin/v1.0/organizations
Content-Type: application/json

{
  "name": "新部门",
  "code": "DEPT001",
  "parentId": "org-parent-id",
  "description": "部门描述",
  "address": "地址"
}
```

**说明：** 不指定 `parentId` 则为顶级组织。

#### 获取组织详情

```
GET /api/admin/v1.0/organizations/{id}
```

**响应：** `OrganizationDetailDto`

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 组织 ID |
| `name` | string | 名称 |
| `code` | string | 编号 |
| `address` | string | 地址 |
| `description` | string | 描述 |
| `parentId` | string | 父组织 ID |
| `scope` | string[] | 范围（如行政区代码） |
| `administrator` | `{id, name}[]` | 管理员列表 |
| `metadata` | string | 元数据 JSON |

#### 更新组织

```
POST /api/admin/v1.0/organizations/{id}
Content-Type: application/json

{
  "name": "新部门名称",
  "code": "DEPT001",
  "parentId": "new-parent-id",
  "description": "新的描述",
  "address": "新地址",
  "metadata": "{\"key\":\"value\"}"
}
```

#### 删除组织

```
POST /api/admin/v1.0/organizations/{id}/delete
```

**限制（相应错误码）：**
- 存在下级组织 → `5006`（请先删除下级机构）
- 存在关联用户 → `5007`（请先删除关联用户）
- 操作一级组织但非 admin → `5008`（仅允许超级管理员操作一级机构）

#### 添加组织管理员

```
POST /api/admin/v1.0/organizations/{id}/administrators/{userId}
```

将指定用户设为指定组织的管理员（自动获取 `organization-admin` 角色能力）。

#### 删除组织管理员

```
POST /api/admin/v1.0/organizations/{id}/administrators/{userId}/delete`
```

移除用户对指定组织的管理权限。

### 6.6 用户自助接口 (V1.0)

**控制器：** `UserController`（Controllers/V10）→ `UserService`  
**路由：** `/api/v1.0/users`  
**授权：** `[Authorize]`

#### 短信验证码重置密码

```
POST /api/v1.0/users/resetPasswordByCaptcha
Content-Type: application/json

{
  "phoneNumber": "13800138000",
  "captcha": "123456",
  "newPassword": "NewP@ssw0rd"
}
```

（当前实现为 `Task`，无返回值，成功即完成。异常通过全局异常过滤器返回。）

#### 原密码重置密码

```
POST /api/v1.0/users/resetPassword
Content-Type: application/json

{
  "oldPassword": "OldP@ssw0rd",
  "newPassword": "NewP@ssw0rd"
}
```

**注意：** 需要配置 Identity 试错锁定机制防止暴力破解。

#### 获取用户所在组织

```
GET /api/v1.0/users/{userId}/organizations?isAdministrator=false
```

**查询参数：**

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| `isAdministrator` | bool | — | `false` | 为 `true` 时仅查询用户作为管理员的组织 |

**响应：** `OrganizationDto[]`

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 组织 ID |
| `name` | string | 组织名称 |
| `parentId` | string | 父组织 ID |
| `hasChild` | bool | 是否有子组织 |

### 6.7 组织查询接口 (V1.0)

**控制器：** `OrganizationController`（Controllers/V10）→ `OrganizationService`  
**路由：** `/api/v1.0/organizations`  
**授权：** `[Authorize]`

机构信息不视为敏感信息，登录用户即可查看。

#### 获取顶级三级组织列表

```
GET /api/v1.0/organizations/base
```

**说明：** 从预生成的 JSON 文件（`wwwroot/data/organizations.json`）提供数据，支持 ETag 缓存。

- 首次请求 → 返回 `200` + JSON 内容 + `ETag` 头
- 后续请求（携带 `ETag`）→ 内容未变时返回 `304 Not Modified`

数据由后台服务 `GenerateTop3LevelOrganizationsToFileService` 每 5 分钟同步更新一次。

#### 获取子组织列表

```
GET /api/v1.0/organizations/subList?parentId=
```

与 admin 版本的接口功能相同，但无权限限制（所有登录用户均可查询）。

#### 获取组织详情

```
GET /api/v1.0/organizations/{id}
GET /api/v1.0/organizations/{id}/summary   // 别名
```

**响应：** `OrganizationDetailDto`

### 6.8 权限验证接口 (V1.0)

**控制器：** `PermissionController` → `PermissionService`  
**路由：** `/api/v1.0/permissions`  
**授权：** `[Authorize]`

#### 批量权限验证

```
POST /api/v1.0/permissions/enforce
Content-Type: application/json

[
  { "action": "user:delete", "resource": "org-001" },
  { "action": "role:create", "resource": null }
]
```

**请求体：** `EnforceQuery[]`
- `action` (string)：操作
- `resource` (string|nullable)：资源

**响应：** 纯文本 JSON 数组（Content-Type: `text/plain`），如 `[true,false]`

**说明：** 逐条检查用户是否拥有对指定资源和操作的权限。

---

## 7. 数据模型

### 7.1 核心表结构

所有数据库表使用 `wild_goose_` 前缀（可通过 `TablePrefix` 配置修改）。ID 使用 ObjectId 风格字符串（36 位）。

| 表名 | 实体 | 说明 | 主要字段 |
|------|------|------|---------|
| `wild_goose_user` | `User` | 用户表（继承 `IdentityUser`） | `id`, `name`, `given_name`, `family_name`, `user_name`, `email`, `phone_number`, `picture`, `code`, `address`等 |
| `wild_goose_role` | `Role` | 角色表（继承 `IdentityRole`） | `id`, `name`, `normalized_name`, `description`, `version`, `statement` |
| `wild_goose_user_role` | （Identity 自动） | 用户角色关联表 | `user_id`, `role_id` |
| `wild_goose_role_assignable_role` | `RoleAssignableRole` | 角色可授于角色映射表 | `role_id`, `assignable_id` |
| `wild_goose_organization` | `Organization` | 组织表（树形结构） | `id`, `name`, `code`, `parent_id`, `n_id`(自增), `address`, `description`, `metadata` |
| `wild_goose_organization_user` | `OrganizationUser` | 用户组织关联表 | `organization_id`, `user_id` |
| `wild_goose_organization_administrator` | `OrganizationAdministrator` | 组织管理员表 | `organization_id`, `user_id` |
| `wild_goose_organization_detail` | `OrganizationDetail` | 组织详情物化视图 | `id`, `name`, `code`, `parent_id`, `parent_name`, `path`, `branch`, `n_id`, `level`, `has_child` |
| `wild_goose_organization_scope` | `OrganizationScope` | 组织范围表 | `organization_id`, `scope` |
| `wild_goose_user_extension` | `UserExtension` | 用户扩展信息表 | `id`, `title`, `departure_time`, `reset_password_flag`, `hidden_sensitive_data`, `property01` ~ `property09` |

> `OrganizationRole` 实体已注释掉（代码注释说明"会把模型搞得很复杂"），未来不会使用。

### 7.2 实体审计

实体通过实现接口实现自动审计：

| 接口 | 自动填充的字段 | 填充时机 | 数据来源 |
|------|---------------|---------|---------|
| `ICreation` | `CreationTime`, `CreatorId`, `CreatorName` | 实体首次 `SaveChanges` | `ISession` 中的当前用户信息 |
| `IModification` | `LastModificationTime`, `LastModifierId`, `LastModifierName` | 实体每次 `SaveChanges` | `ISession` 中的当前用户信息 |
| `IDeletion` | `IsDeleted`, `DeleterId`, `DeleterName`, `DeletionTime` | 实体被软删除时 | `ISession` 中的当前用户信息 |

实现方式：`WildGooseDbContext.ApplyConcepts()` 在 `SaveChangesAsync` 中遍历所有变更跟踪实体，根据状态自动填充。

### 7.3 软删除

实现了 `IDeletion` 接口的实体使用软删除机制：

- 调用 `Remove()` 或设置 `IsDeleted = true` 时，`SaveChangesAsync` 自动将操作转换为 `Modify`（而非 `Delete`）
- EF Core 查询过滤器自动排除 `IsDeleted == true` 的记录
- 软删除时自动记录删除人、删除时间

### 7.4 用户扩展属性

`UserExtension` 实体提供了丰富的扩展能力：

**系统预定义字段：**

| 字段 | 类型 | 说明 |
|------|------|------|
| `Title` | string | 工作职位 |
| `DepartureTime` | DateTimeOffset? | 离职时间 |
| `HiddenSensitiveData` | bool | 隐藏敏感数据（如手机号、邮箱） |
| `ResetPasswordFlag` | bool | 是否需要重置密码 |
| `PasswordLength` | int | 密码长度（用于密码策略审计） |
| `PasswordContainsDigit` | bool | 密码是否包含数字 |
| `PasswordContainsLowercase` | bool | 密码是否包含小写 |
| `PasswordContainsUppercase` | bool | 密码是否包含大写 |
| `PasswordContainsNonAlphanumeric` | bool | 密码是否包含特殊符号 |

**扩展字段：** `Property01` ~ `Property09`（共 9 个字符串字段）

扩展字段通过 `WildGooseOptions.UserPropertyMappings` 配置映射到逻辑属性名（见 3.3 节）。

### 7.5 组织层级与物化视图

组织采用树形结构，通过以下字段维护层级关系：

- **`parent_id`**：父组织 ID，顶级组织为空字符串
- **`n_id`**：自增数字 ID（用于构建路径，ObjectId 过长不适合路径）
- **`path`**：层级路径，格式如 `/A/B/C`（在物化视图 `organization_detail` 中）

`organization_detail` 是物化视图（PostgreSQL）/ 普通表（MySQL），通过 SQL 脚本创建：

| 字段 | 说明 |
|------|------|
| `path` | 路径字符串，格式如 `/1/3/5`（使用 NId 构建） |
| `branch` | 分支路径 |
| `level` | 层级深度（从 0 开始） |
| `has_child` | 是否有子组织 |
| `parent_name` | 父组织名称 |

组织管理员的权限通过路径前缀匹配判断：

```csharp
// admin 管理 /A，则可管理 /A/B、/A/C 等
organizationDetail.Path.StartsWith(adminPath)
```

---

## 8. 集成事件

系统通过 Dapr pub/sub 发布集成事件。需要在 `docker-entrypoint.sh` 中配置 `DAPR_URL` 下载 Dapr sidecar。

**启用条件：**
1. `Dapr:pubsub` 配置不为空
2. Dapr sidecar 已运行
3. 通过 `DaprClient`（从 DI 获取）发布事件

**发布的事件：**

| 事件 | CloudEvent Type 前缀 | 数据负载 | 触发点 |
|------|---------------------|---------|--------|
| `UserAddedEvent` | `UserAdded` | `{ UserId, UserName, Name, PhoneNumber, OrganizationIds, RoleNames }` | `UserAdminService.AddAsync` |
| `UserDeletedEvent` | `UserDeleted` | `{ UserId }` | `UserAdminService.DeleteAsync` |
| `UserDisabledEvent` | `UserDisabled` | `{ UserId }` | `UserAdminService.DisableAsync` |
| `UserEnabledEvent` | `UserEnabled` | `{ UserId }` | `UserAdminService.EnableAsync` |

**发布机制：** `BaseService.PublishEventAsync` 使用 3 次重试（间隔 100ms）。

> 注意：Dapr 侧并非必需。如不配置 Dapr 或 DaprClient 不可用，系统正常运作但不发布事件。

---

## 9. Docker 部署

### 9.1 后端 Docker 构建

**Dockerfile：** `api.Dockerfile` — 多阶段 .NET 10 构建

```bash
docker build -f api.Dockerfile -t wildgoose-api .
```

**启动流程：**
1. `docker-entrypoint.sh` 执行配置模板渲染：
   - 读取 `$CONFIG_SOURCE` 环境变量指定的模板路径
   - 替换所有 `${VAR_NAME}` 占位符为对应的环境变量值
   - 输出到 `/app/appsettings.json`
2. 可选下载 Dapr sidecar（当 `$DAPR_URL` 不为空时）
3. 启动 .NET 应用

### 9.2 前端 Docker 构建

**Dockerfile：** `web.Dockerfile` — Node 20 构建 + nginx 运行

```bash
docker build -f web.Dockerfile -t wildgoose-web .
```

**运行时配置注入：** `src/WildGoose.Web/docker-entrypoint.sh`
1. 将 `public/config.js` 中的 `${VAR}` 替换为环境变量值
2. 对替换后的 `config.js` 计算 MD5，生成版本化文件名（如 `config_a1b2c3d4.js`）
3. 修改 `index.html` 中的 `<script>` 引用指向版本化文件
4. nginx 配置 `expires 6h` 确保缓存

所以 Docker 部署前端的配置方式是设置环境变量（如 `BACKEND`、`BASE_PATH` 等），而非直接修改 `config.js`。

### 9.3 nginx 配置要点

- SPA 单入口配置（`try_files $uri $uri/ /index.html`）
- HTML 文件不缓存
- 静态资源（js、css、图片）缓存 6 小时
- 启用 gzip 压缩（压缩级别 9）
- 禁用 ETag（使用版本化文件名方式）

---

## 10. 附录：错误码

完整错误码定义参见：`src/WildGoose.Domain/ErrorCodes.cs`

### 10.1 范围规划

| 范围 | 分类 | 说明 |
|------|------|------|
| 1xxx | 通用业务错误 | 参数为空、数据不存在、数据已存在等 |
| 2xxx | 权限相关错误 | 访问被拒绝、权限不足、系统保留等 |
| 3xxx | 服务器错误 | 内部错误、保存失败、服务不可用等 |
| 4xxx | 用户相关错误 | 用户不存在、禁止删除自己、无效角色等 |
| 5xxx | 组织相关错误 | 组织不存在、父组织不存在、无管理权限等 |
| 6xxx | 角色相关错误 | 角色不存在、禁止操作系统角色等 |
| 7xxx | 文件/上传相关错误 | 文件为空、文件过大、不支持格式等 |
| 8xxx | 认证相关错误 | ApiName 为空、上下文异常等 |

### 10.2 错误码速查

| 错误码 | 常量名 | 说明 |
|--------|--------|------|
| **1001** | `Required` | 参数不能为空 |
| **1002** | `NotFound` | 数据不存在 |
| **1003** | `AlreadyExists` | 数据已存在 |
| **1004** | `OperationFailed` | 操作失败 |
| **1005** | `InvalidParameter` | 无效参数 |
| **1006** | `InvalidFormat` | 格式无效 |
| **1007** | `OutOfRange` | 超出范围 |
| **2001** | `Forbidden` | 访问被拒绝 |
| **2002** | `InsufficientPermission` | 权限不足 |
| **2003** | `SystemReserved` | 禁止操作（系统保留） |
| **3001** | `InternalError` | 服务器内部错误 |
| **3002** | `SaveFailed` | 保存失败 |
| **3003** | `ServiceUnavailable` | 服务不可用 |
| **4001** | `UserNotFound` | 用户不存在 |
| **4002** | `CannotDeleteSelf` | 禁止删除自己 |
| **4003** | `CannotDisableSelf` | 禁止禁用自己 |
| **4004** | `UserDisabled` | 账户已被禁用 |
| **4005** | `InvalidRole` | 无效角色 |
| **4006** | `InvalidPassword` | 密码错误 |
| **5001** | `OrganizationNotFound` | 机构不存在 |
| **5002** | `ParentOrganizationNotFound` | 父机构不存在 |
| **5003** | `AncestorOrganizationNotFound` | 上级机构不存在 |
| **5004** | `NoOrganizationPermission` | 没有管理机构的权限 |
| **5005** | `SuperAdminOnly` | 仅允许超级管理员操作 |
| **5006** | `HasChildOrganizations` | 请先删除下级机构 |
| **5007** | `HasRelatedUsers` | 请先删除关联用户 |
| **5008** | `CannotModifyTopLevelOrganization` | 仅允许超级管理员操作一级机构 |
| **6001** | `RoleNotFound` | 角色不存在 |
| **6002** | `RoleAlreadyExists` | 角色已经存在 |
| **6003** | `CannotModifySystemRole` | 禁止操作系统角色信息 |
| **6004** | `SystemRoleNameReserved` | 禁止使用系统角色名 |
| **6005** | `NonAssignableRole` | 存在不可授于的角色 |
| **6006** | `RoleNameRequired` | 角色名称不能为空 |
| **7001** | `FileEmpty` | 上传文件为空 |
| **7002** | `FileTooLarge` | 文件过大 |
| **7003** | `UnsupportedFileFormat` | 不支持的文件格式 |
| **7004** | `UploadFailed` | 上传失败 |
| **8001** | `ApiNameRequired` | ApiName is null or empty |
| **8002** | `ContextError` | HTTP 上下文异常 |

---

## 11. 附录：请求/响应示例

### 11.1 管理端：添加用户

```http
POST /api/admin/v1.0/users
Content-Type: application/json
Authorization: Bearer <token>

{
  "userName": "zhangsan",
  "name": "张三",
  "email": "zhangsan@example.com",
  "phoneNumber": "13800138000",
  "organizationIds": ["org-001", "org-002"],
  "roleNames": ["employee"],
  "password": "P@ssw0rd",
  "properties": {
    "Title": "工程师",
    "EmployeeId": "EMP001"
  }
}
```

### 11.2 成功响应

```json
{
  "success": true,
  "code": 0,
  "data": {
    "id": "674e6f8a2f1b4c3d9e8f7a6b",
    "userName": "zhangsan",
    "name": "张三",
    "phoneNumber": "13800138000",
    "organizations": ["机构A", "机构B"],
    "roles": ["employee"],
    "creationTime": "2026-05-15 10:30:00"
  }
}
```

### 11.3 分页响应

```json
{
  "success": true,
  "code": 0,
  "data": {
    "data": [
      {
        "id": "user-001",
        "userName": "zhangsan",
        "name": "张三"
      }
    ],
    "page": 1,
    "limit": 10,
    "total": 42
  }
}
```

### 11.4 错误响应

```json
{
  "success": false,
  "code": 4001,
  "msg": "用户不存在"
}
```

带字段验证错误：
```json
{
  "success": false,
  "code": 1005,
  "msg": "无效参数",
  "errors": [
    {
      "name": "userName",
      "messages": ["用户名不能为空"]
    }
  ]
}
```

### 11.5 使用 X-AUTH-TOKEN 服务间调用

```http
POST /api/admin/v1.1/users
Content-Type: application/json
X-AUTH-TOKEN: your-security-token
X-AUTH-ROLE: admin

{
  "userName": "service-user",
  "name": "服务用户",
  "phoneNumber": "13900139000"
}
```

### 11.6 前端请求加密示例

请求头：
```
Z-Encrypt-Version: v1.1
Z-Encrypt-Key: a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6
```

请求体（加密后的 Base64 字符串）：
```
U2FsdGVkX1+...
```

---

**文档更新时间：** 2026-05-15  
**文档版本：** 2.0  
**基于提交：** dfb3893
