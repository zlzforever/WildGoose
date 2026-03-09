# WildGoose 用户权限系统使用文档

> 文档版本：1.0  
> 项目：WildGoose - 基于 ASP.NET Core 的用户、组织、权限管理系统  
> 注意：本系统仅支持 GET 和 POST 两种 HTTP 方法

---

## 目录

1. [系统概述](#1-系统概述)
2. [配置说明](#2-配置说明)
   - [2.1 关键环境变量](#21-关键环境变量)
   - [2.2 DISABLE_PASSWORD_LOGIN 配置](#22-disable_password_login-配置)
   - [2.3 数据库配置](#23-数据库配置)
   - [2.4 Identity 密码策略配置](#24-identity-密码策略配置)
   - [2.5 WildGoose 业务配置](#25-wildgoose-业务配置)
   - [2.6 JWT 配置](#26-jwt-配置)
3. [角色与权限](#3-角色与权限)
4. [API 接口文档](#4-api-接口文档)
5. [数据模型](#5-数据模型)

---

## 1. 系统概述

### 1.1 核心功能

WildGoose 是一个多租户用户和权限管理系统，提供以下核心功能：

| 功能模块 | 说明 |
|---------|------|
| **用户管理** | 用户的增删改查、密码重置、头像设置、禁用/启用 |
| **组织管理** | 层级组织架构管理（树形结构），支持多级嵌套 |
| **角色管理** | 角色定义、可授于角色配置、权限声明 |
| **权限控制** | 基于角色的访问控制 (RBAC)，支持组织级管理员 |

### 1.2 设计理念

- **组织层级透明**：用户在全组织下的信息基本透明，任何登录用户都可以查看组织信息
- **三种管理角色**：
  - `admin` (超级管理员) - 最高权限
  - `user_admin` (用户管理员) - 管理用户和组织，无角色管理权限
  - `organization_admin` (组织管理员) - 管理所辖组织及下级组织的用户

---

## 2. 配置说明

### 2.1 关键环境变量

| 环境变量 | 说明 | 默认值 |
|---------|------|--------|
| `DISABLE_PASSWORD_LOGIN` | 禁用密码登录（启用第三方认证时使用） | `false` |
| `ENABLE_SM3_PASSWORD_HASHER` | 启用国密 SM3 密码哈希 | `false` |
| `HEALTH_CHECK_PATH` | 健康检查路径 | `/healthz` |

### 2.2 DISABLE_PASSWORD_LOGIN 配置

当设置 `DISABLE_PASSWORD_LOGIN=true` 时，系统将：

1. **禁用密码验证**：用户无法使用密码登录
2. **禁用密码策略**：不需要密码长度、复杂度等要求
3. **使用无密码存储**：用户只能通过第三方认证登录

**配置示例：**

```bash
# 禁用密码登录
export DISABLE_PASSWORD_LOGIN=true

# 同时启用国密 SM3 密码哈希（可选）
export ENABLE_SM3_PASSWORD_HASHER=true
```

在 `Program.cs` 中的实现：

```csharp
Defaults.DisablePasswordLogin = "true".Equals(builder.Configuration["DISABLE_PASSWORD_LOGIN"]);
if (Defaults.DisablePasswordLogin)
{
    identityBuilder.AddPasswordValidator<NoopPasswordValidator<User>>();
    identityBuilder.AddUserStore<NoPasswordStore>();
}
```

### 2.3 数据库配置

在 `appsettings.json` 中配置：

```json5
{
  "DbContext": {
    "DatabaseType": "PostgreSql",  // 或 MySql
    "AutoMigrationEnabled": true,
    "TablePrefix": "wild_goose_",
    "UseUnderScoreCase": true,     // 是否使用下划线命名
    "EnableSensitiveDataLogging": false,
    "ConnectionString": "User ID=postgres;Password=xxx;Host=localhost;Database=wildgoose;",
    "TableMapper": {               // 表名映射（用于兼容现有数据库）
      "organization_user2": "user_organization"
    }
  }
}
```

#### DbContext:TableMapper 说明

`TableMapper` 用于当现有数据库表名与代码中的实体名不一致时，进行映射转换。

例如：代码中使用 `user_organization`，但现有数据库表名是 `organization_user2`，则配置：
```json
{
  "TableMapper": {
    "organization_user2": "user_organization"
  }
}
```

### 2.4 Identity 密码策略配置

```json5
{
  "Identity": {
    "Password": {
      "RequireDigit": false,         // 是否需要数字
      "RequireLowercase": false,     // 是否需要小写字母
      "RequireNonAlphanumeric": false, // 是否需要特殊字符
      "RequireUppercase": false,    // 是否需要大写字母
      "RequiredLength": 0,          // 最小密码长度（0表示不限制）
      "RequiredUniqueChars": 1      // 唯一字符数量
    },
    "SignIn": {
      "RequireConfirmedEmail": false,     // 是否需要邮箱验证
      "RequireConfirmedAccount": false,   // 是否需要账户确认
      "RequireConfirmedPhoneNumber": false // 是否需要手机验证
    },
    "Lockout": {
      "DefaultLockoutTimeSpan": "00:05:00",  // 锁定时长
      "MaxFailedAccessAttempts": 5,          // 最大失败次数
      "AllowedForNewUsers": false             // 新用户是否可锁定
    },
    "User": {
      "AllowedUserNameCharacters": "^[a-zA-Z0-9\\u4e00-\\u9fa5@]+$", // 允许的用户名字符
      "RequireUniqueEmail": false                                    // 是否要求邮箱唯一
    }
  }
}
```

#### Identity:Password 说明

| 配置项 | 说明 | 典型值 |
|--------|------|--------|
| `RequiredLength` | 最小密码长度，设为 0 表示不限制 | `0` 或 `8` |
| `RequireDigit` | 是否必须包含数字 | `false` |
| `RequireLowercase` | 是否必须包含小写字母 | `false` |
| `RequireUppercase` | 是否必须包含大写字母 | `false` |
| `RequireNonAlphanumeric` | 是否必须包含特殊字符 | `false` |

> ⚠️ 当 `DISABLE_PASSWORD_LOGIN=true` 时，密码策略不生效。

### 2.5 WildGoose 业务配置

```json5
{
  "WildGoose": {
    "AddUserRoles": ["内部添加用户"]
  }
}
```

#### WildGoose:AddUserRoles 说明

定义哪些角色的用户可以使用 **V1.1 简化添加用户接口** (`POST /api/admin/v1.1/users`)。

- **为空数组 `[]`**：任何已认证用户都可以调用
- **非空数组**：`["角色1", "角色2"]`，只有拥有这些角色的用户才能调用

示例：配置 `["内部添加用户"]` 后，只有拥有 "内部添加用户" 角色的用户才能调用简化添加用户接口，否则返回 403 访问受限。

实现逻辑 (`UserAdminService.V11`)：
```csharp
var options = wildGooseOptions.Value;
if (options.AddUserRoles.Length == 0 || !Session.Roles.Any(x => options.AddUserRoles.Contains(x)))
{
    throw new WildGooseFriendlyException(403, "访问受限");
}
```

### 2.6 JWT 配置

```json5
{
  "JwtBearer": {
    "ValidateAudience": false,
    "ValidateIssuer": false,
    "KeyPath": ""
  }
}
```

---

## 3. 角色与权限

### 3.1 内置角色

| 角色名 | 说明 | 权限 |
|--------|------|------|
| `admin` | 超级管理员 | 最高权限，可管理所有功能 |
| `user_admin` | 用户管理员 | 管理用户和组织，无角色管理权限 |
| `organization_admin` | 组织管理员 | 仅可管理所辖组织（含下级）的用户 |

### 3.2 权限策略

系统使用 ASP.NET Core Policy 进行授权：

| Policy 名称 | 允许角色 |
|-------------|----------|
| `SUPER` | admin |
| `SUPER_OR_USER_ADMIN_OR_ORG_ADMIN` | admin, user_admin, organization_admin |

---

## 4. API 接口文档

### 4.1 接口版本

| 版本 | 路径 | 说明 |
|------|------|------|
| V1.0 | `/api/v1.0/*` | 用户自助接口（需登录） |
| V1.0 | `/api/admin/v1.0/*` | 管理员完整接口（需管理员角色） |
| V1.1 | `/api/admin/v1.1/*` | 管理员简化接口（通常用于内部服务调用） |

---

### 4.2 用户管理接口 (Admin)

> **V1.0 vs V1.1 接口区别**：
> - **V1.0** (`/api/admin/v1.0/users`)：完整用户管理接口，支持设置角色、组织、密码等
> - **V1.1** (`/api/admin/v1.1/users`)：简化添加用户接口，通常用于内部服务调用，无需设置角色和组织

#### 用户管理 - 基础接口

| 方法 | 路径 | 说明 | 权限 |
|------|------|------|------|
| GET | `/api/admin/v1.0/users` | 获取用户列表 | 管理员 |
| POST | `/api/admin/v1.0/users` | 添加用户 | 管理员 |
| GET | `/api/admin/v1.0/users/{id}` | 获取用户详情 | 管理员 |
| POST | `/api/admin/v1.0/users/{id}` | 更新用户 | 管理员 |
| POST | `/api/admin/v1.0/users/{id}/remove` | 删除用户 | 管理员 |
| POST | `/api/admin/v1.0/users/{id}/enable` | 启用用户 | 管理员 |
| POST | `/api/admin/v1.0/users/{id}/disable` | 禁用用户 | 管理员 |
| POST | `/api/admin/v1.0/users/{id}/password` | 修改密码 | 管理员 |
| POST | `/api/admin/v1.0/users/{id}/picture` | 设置头像 | 管理员 |

#### 用户管理 - V1.1 简化接口

> ⚠️ 调用此接口需要当前用户具有 `WildGoose:AddUserRoles` 配置中指定的角色。

| 方法 | 路径 | 说明 | 权限 |
|------|------|------|------|
| POST | `/api/admin/v1.1/users` | 简化添加用户（无需设置角色/组织） | 需要特定角色（见配置） |

---

### 4.3 角色管理接口

| 方法 | 路径 | 说明 | 权限 |
|------|------|------|------|
| GET | `/api/admin/v1.0/roles` | 获取角色列表 | 仅超级管理员 |
| POST | `/api/admin/v1.0/roles` | 添加角色 | 仅超级管理员 |
| GET | `/api/admin/v1.0/roles/{id}` | 获取角色详情 | 仅超级管理员 |
| POST | `/api/admin/v1.0/roles/{id}` | 更新角色 | 仅超级管理员 |
| POST | `/api/admin/v1.0/roles/{id}/delete` | 删除角色 | 仅超级管理员 |
| POST | `/api/admin/v1.0/roles/{id}/statement` | 更新角色权限声明 | 仅超级管理员 |
| POST | `/api/admin/v1.0/roles/{id}/assignableRoles` | 添加可授于角色 | 仅超级管理员 |
| POST | `/api/admin/v1.0/roles/{id}/assignableRoles/{roleId}/delete` | 删除可授于角色 | 仅超级管理员 |
| GET | `/api/admin/v1.0/roles/assignableRoles` | 获取当前用户可授于角色列表 | 登录用户 |

---

### 4.4 组织管理接口 (Admin)

| 方法 | 路径 | 说明 | 权限 |
|------|------|------|------|
| GET | `/api/admin/v1.0/organizations/subList` | 获取子组织列表 | 管理员 |
| GET | `/api/admin/v1.0/organizations/search` | 搜索组织 | 管理员 |
| POST | `/api/admin/v1.0/organizations` | 添加组织 | 管理员 |
| GET | `/api/admin/v1.0/organizations/{id}` | 获取组织详情 | 管理员 |
| POST | `/api/admin/v1.0/organizations/{id}` | 更新组织 | 管理员 |
| POST | `/api/admin/v1.0/organizations/{id}/delete` | 删除组织 | 管理员 |
| POST | `/api/admin/v1.0/organizations/{id}/administrators/{userId}` | 添加组织管理员 | 管理员 |
| POST | `/api/admin/v1.0/organizations/{id}/administrators/{userId}/delete` | 删除组织管理员 | 管理员 |

---

### 4.5 用户自助接口

| 方法 | 路径 | 说明 | 权限 |
|------|------|------|------|
| POST | `/api/v1.0/users/resetPasswordByCaptcha` | 短信验证码重置密码 | 任意用户 |
| POST | `/api/v1.0/users/resetPassword` | 原密码重置密码 | 登录用户 |
| GET | `/api/v1.0/users/{userId}/organizations` | 获取用户所在组织 | 登录用户 |

---

### 4.6 组织查询接口 (用户)

| 方法 | 路径 | 说明 | 权限 |
|------|------|------|------|
| GET | `/api/v1.0/organizations/base` | 获取顶级三级组织列表 | 登录用户 |
| GET | `/api/v1.0/organizations/subList` | 获取子组织列表 | 登录用户 |
| GET | `/api/v1.0/organizations/{id}` | 获取组织详情 | 登录用户 |

---

### 4.7 权限验证接口

| 方法 | 路径 | 说明 | 权限 |
|------|------|------|------|
| POST | `/api/v1.0/permissions/enforce` | 批量权限验证 | 登录用户 |

---

## 5. 数据模型

### 5.1 核心表结构

| 表名 | 说明 |
|------|------|
| `wild_goose_user` | 用户表 |
| `wild_goose_role` | 角色表 |
| `wild_goose_user_role` | 用户角色关联表 |
| `wild_goose_organization` | 组织表 |
| `wild_goose_organization_user` | 用户组织关联表 |
| `wild_goose_organization_administrator` | 组织管理员表 |
| `wild_goose_organization_detail` | 组织详情物化表（含路径信息） |
| `wild_goose_role_assignable_role` | 角色可授于角色映射表 |

### 5.2 组织层级

组织采用树形结构，通过以下字段维护层级关系：

- `parent_id`：父组织 ID
- `n_id`：自增数字 ID（用于构建路径）
- `path`：层级路径，格式如 `/A/B/C`

组织管理员的权限通过路径前缀匹配判断，例如：管理员管理 `/A`，则可管理 `/A/B`、`/A/C` 等。

---

## 附录 A：请求/响应示例

### 添加用户

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
  "roleNames": ["employee"]
}
```

### 响应

```json
{
  "success": true,
  "data": {
    "id": "user-123",
    "userName": "zhangsan",
    "name": "张三",
    "email": "zhangsan@example.com",
    "phoneNumber": "13800138000",
    "organizations": ["org-001", "org-002"],
    "roles": ["employee"]
  }
}
```

### 错误响应

```json
{
  "success": false,
  "code": 1,
  "msg": "用户不存在"
}
```

---

## 附录 B：错误码

| 错误码 | 说明 |
|--------|------|
| 1xxx | 通用业务错误（参数为空、数据不存在、数据已存在等） |
| 2xxx | 权限相关错误（访问被拒绝、权限不足、系统保留等） |
| 3xxx | 服务器错误（内部错误、保存失败、服务不可用等） |
| 4xxx | 用户相关错误（用户不存在、禁止删除自己、无效角色等） |
| 5xxx | 组织相关错误（组织不存在、父组织不存在、无管理权限等） |
| 6xxx | 角色相关错误（角色不存在、禁止操作系统角色等） |
| 7xxx | 文件/上传相关错误（文件为空、文件过大、不支持格式等） |
| 8xxx | 认证相关错误（ApiName 为空、上下文异常等） |

详细错误码定义请参见：`src/WildGoose.Domain/ErrorCodes.cs`

### 常见错误码示例

| 错误码 | 说明 |
|--------|------|
| 1002 | 数据不存在 |
| 1003 | 数据已存在 |
| 2001 | 访问被拒绝 |
| 2002 | 权限不足 |
| 3001 | 服务器内部错误 |
| 3002 | 保存失败 |
| 4001 | 用户不存在 |
| 4002 | 禁止删除自己 |
| 5001 | 机构不存在 |
| 5004 | 没有管理机构的权限 |
| 6001 | 角色不存在 |
| 7001 | 上传文件为空 |
| 7002 | 文件过大 |

---

**文档更新时间**：2026-03-09
