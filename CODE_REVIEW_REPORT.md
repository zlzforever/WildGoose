# 🔴 WildGoose 项目全局代码审查报告

> 审查范围：全项目 100+ C# 源文件  
> 审查时间：2026-03-09  
> 审查维度：架构设计 / 安全风险 / 性能与资源 / 业务逻辑 / 代码规范 / 测试质量

---

## 一、严重问题 (Critical Issues)

### 1.1 密码学安全隐患

| 位置 | 问题描述 | 风险影响 | 修复方案 |
|------|----------|----------|----------|
| `src/WildGoose.Domain/Utils/CryptographyUtil.cs:8-16` | 使用 **MD5** 计算文件哈希 | MD5 已 cryptographically broken，易受碰撞攻击 | 改用 SHA-256: `SHA256.HashData(data)` |
| `src/WildGoose.Domain/Utils/CryptographyUtil.cs:18-26` | 使用 **ECB 模式** AES 加密 | ECB 模式无随机向量，相同明文产生相同密文，易被破解 | 改用 CBC/GCM 模式并生成随机 IV |

**示例代码 - 当前问题代码:**

```csharp
// ❌ 危险: MD5 用于文件哈希
public static async Task<string> ComputeMd5Async(Stream stream)
{
    stream.Seek(0, SeekOrigin.Begin);
    var data = new byte[stream.Length];
    _ = await stream.ReadAsync(data, 0, data.Length);

    var bytes = MD5.HashData(data);  // 🔴 高危: MD5
    return Convert.ToHexString(bytes);
}

// ❌ 危险: ECB 模式
public static Aes CreateAesEcb(string key)
{
    var keyArray = Encoding.UTF8.GetBytes(key);
    var aes = Aes.Create();
    aes.Key = keyArray;
    aes.Mode = CipherMode.ECB;  // 🔴 高危: ECB 无随机向量
    aes.Padding = PaddingMode.PKCS7;
    return aes;
}
```

**修复建议:**

```csharp
// ✅ 推荐: 使用 SHA-256
public static async Task<string> ComputeSha256Async(Stream stream)
{
    stream.Seek(0, SeekOrigin.Begin);
    var data = new byte[stream.Length];
    _ = await stream.ReadAsync(data, 0, data.Length);

    var bytes = SHA256.HashData(data);
    return Convert.ToHexString(bytes);
}

// ✅ 推荐: 使用 CBC 模式
public static Aes CreateAesCbc(string key)
{
    var keyArray = Encoding.UTF8.GetBytes(key.PadRight(32, '0').Substring(0, 32));
    var aes = Aes.Create();
    aes.Key = keyArray;
    aes.Mode = CipherMode.CBC;  // ✅ CBC 模式
    aes.Padding = PaddingMode.PKCS7;
    aes.GenerateIV();  // ✅ 随机 IV
    return aes;
}
```

---

### 1.2 业务逻辑潜在 Bug

| 位置 | 问题描述 | 风险影响 | 修复方案 |
|------|----------|----------|----------|
| `src/WildGoose.Application/BaseService.cs:231-239` | Admin 组织缓存 1 分钟过期，**无缓存击穿保护** | 高并发时可能造成缓存雪崩，DB 压力骤增 | 实现 mutex 或 distributed lock |
| `src/WildGoose.Application/GenerateTopThreeLevelOrganizationsCacheFileService.cs:28-68` | **Task.Run 内使用 DbContext**，跨线程访问 | EF Core DbContext 非线程安全，可能导致数据竞争 | 使用 `IAsyncEnumerable` 或 scoped service |

**问题代码:**

```csharp
// ❌ BaseService.cs:231-239 - 无缓存击穿保护
return await MemoryCache.GetOrCreateAsync($"WILDGOOSE:AdminOrganizationList:{userId}", async entry =>
{
    var list = await GetAdminOrganizationsQueryable(userId)
        .Select(x => OrganizationEntity.Build(x.Detail))
        .ToListAsync();
    entry.SetValue(list);
    entry.SetSlidingExpiration(TimeSpan.FromMinutes(1));  // 🔴 1分钟过期，高并发时击穿
    return list;
});

// ❌ GenerateTopThreeLevelOrganizationsCacheFileService.cs:28-68
Task.Run(async () =>
{
    await using var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();
    // 🔴 危险: DbContext 在 Task.Run 线程池线程中使用，非线程安全
    var list = await GetListAsync(dbContext, parentIdList);
    // ...
});
```

---

## 二、一般问题 (Medium Issues)

### 2.1 架构设计

| 位置 | 问题描述 | 风险影响 | 修复方案 |
|------|----------|----------|----------|
| `src/WildGoose.Application/User/Admin/V10/UserAdminService.cs` | **God Class**: ~944 行，处理过多职责 | 维护困难，单一职责违反 | 按功能拆分为: UserQueryService, UserCommandService, UserRoleService |
| `src/WildGoose.Application/BaseService.cs` | **直接依赖 DbContext**，违反 DIP | 难以单元测试，耦合高 | 引入 Repository 接口或 CQRS |

**建议拆分方案:**

```
UserAdminService (~944行)
├── UserQueryService      (GetAsync, GetListAsync)
├── UserCommandService    (AddAsync, UpdateAsync, DeleteAsync)
├── UserPasswordService   (ChangePasswordAsync)
├── UserRoleService       (UpdateRolesAsync)
└── UserOrganizationService (UpdateOrganizationsAsync)
```

---

### 2.2 性能问题

| 位置 | 问题描述 | 风险影响 | 修复方案 |
|------|----------|----------|----------|
| `src/WildGoose.Application/Organization/Admin/V10/OrganizationAdminService.cs:434` | **HasChild 查询使用 Any()**，每行执行子查询 | O(n²) 复杂度，数据量大时慢 | 预计算 has_child 字段或使用原生 SQL |
| `src/WildGoose.Application/BaseService.cs:111-123` | `CanManageAll` 方法 **Path.StartsWith 每次全表扫描** | 组织层级深时性能差 | 改用 Path 前缀索引或存储层级信息 |

**问题代码:**

```csharp
// ❌ OrganizationAdminService.cs:434 - N+1 查询
.Select(x => new SubOrganizationDto
{
    // ...
    HasChild = dbContext.Set<Organization>()
        .Any(y => y.Parent.Id == x.Id)  // 🔴 每行执行一次查询，O(n²)
});
```

**优化建议:**

```csharp
// ✅ 方案1: 预计算字段
// 在 OrganizationDetail 表添加 has_child 布尔字段，插入/删除时更新

// ✅ 方案2: 一次查询 + 内存处理
var allChildIds = dbContext.Set<Organization>()
    .Where(x => parentIds.Contains(x.Parent.Id))
    .Select(x => x.Parent.Id)
    .Distinct()
    .ToHashSet();

// 然后在内存中判断 HasChild
```

---

### 2.3 输入验证不足

| 位置 | 问题描述 | 风险影响 | 修复方案 |
|------|----------|----------|----------|
| `src/WildGoose.Application/User/Admin/V10/Queries/GetUserListQuery.cs` | **缺少分页参数校验** (page/limit 可为负数) | 非法输入可能导致异常 | 添加 `[Range(1, int.MaxValue)]` |
| `src/WildGoose.Application/Role/Admin/V10/Command/AddRoleCommand.cs` | 角色名无格式校验 | 可能接受非法字符 | 添加 `[RegularExpression]` 限制 |

---

## 三、优化建议 (Optimization)

### 3.1 代码规范

| 位置 | 问题描述 | 优化建议 |
|------|----------|----------|
| `src/WildGoose.Application/User/Admin/V10/UserAdminService.cs:35` | **魔法值**: `".webp", ".bmp", ".jpg"...` | 提取为常量 |
| `src/WildGoose.Application/User/Admin/V10/UserAdminService.cs:600` | **魔法值**: `2 * 1024 * 1024` (2MB) | 提取为 `const long MaxImageSizeBytes` |
| 多处 | 错误码 **code=1** 含义不明确 | 创建 ErrorCodes 枚举 |

**示例:**

```csharp
// ✅ 推荐: 提取魔法值
public static class FileConstants
{
    public const long MaxImageSizeBytes = 2 * 1024 * 1024;  // 2 MB
    public static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".webp", ".bmp", ".jpg", ".jpeg", ".png", ".gif"
    };
}

public enum ErrorCode
{
    NotFound = 1,
    ValidationFailed = 2,
    Unauthorized = 403,
    ServerError = 500
}
```

---

### 3.2 日志与可观测性

| 位置 | 问题描述 | 优化建议 |
|------|----------|----------|
| `src/WildGoose.Application/BaseService.cs:159` | 异常日志打印完整 Event JSON | 移除敏感字段或使用结构化日志 |
| 多处 | 缺少请求追踪 ID | 添加 CorrelationId 用于链路追踪 |

---

### 3.3 可维护性

| 位置 | 问题描述 | 优化建议 |
|------|----------|----------|
| `src/WildGoose.Application/BaseService.cs:72` | TODO: "需要缓存，如果机构太多会导致循环调用数据库" | 实现分布式缓存或预加载 |
| 多处服务 | 缺少 XML 文档注释 | 为 Public API 添加 `/// <summary>` |

---

## 四、短期必改清单 (Priority Order)

```
[P0 - 安全修复 - 必须立即处理]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
☐ 1. 替换 CryptographyUtil.cs 中 MD5 → SHA-256
☐ 2. 替换 ECB 模式 → CBC/GCM 模式
☐ 3. 检查 appsettings.json 中是否有硬编码密钥

[P1 - 稳定性修复]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
☐ 4. 修复 Task.Run 内使用 DbContext 问题
☐ 5. 添加分页参数校验 (page ≥ 1, limit ≤ 100)
☐ 6. 实现缓存击穿保护

[P2 - 代码质量]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
☐ 7. 拆分 UserAdminService (按读/写/角色拆分)
☐ 8. 提取魔法值为常量/枚举
☐ 9. 添加缺失的 XML 文档

[P3 - 性能优化]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
☐ 10. 优化 HasChild 查询 (预计算字段)
☐ 11. 优化 Path 权限检查 (添加索引)
```

---

## 五、架构优化总建议

### 5.1 引入 Repository 模式

```csharp
// 现状: 直接依赖 DbContext
public class UserAdminService(WildGooseDbContext dbContext, ...) { }

// 推荐: 引入 IRepository
public interface IUserRepository
{
    Task<User> GetByIdAsync(string id);
    Task<PagedResult<User>> GetListAsync(GetUserListQuery query);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
}

public class UserAdminService(IUserRepository userRepository, ...) { }
```

### 5.2 完善单元测试

- 当前测试以集成测试为主
- 建议添加针对 BaseService 权限逻辑的单元测试
- 目标覆盖率: 60% → 80%+

### 5.3 审计日志

建议添加关键操作审计:
- 用户禁用/删除
- 角色分配/撤销
- 组织架构变更
- 权限提升操作

---

## 六、已验证良好的设计

| 方面 | 评价 |
|------|------|
| 项目分层 | ✅ 清晰的 Domain/Application/Infrastructure/Web 分离 |
| 错误处理 | ✅ GlobalExceptionFilter + WildGooseFriendlyException 统一模式 |
| 异步编程 | ✅ 正确使用 async/await，无 .Result 阻塞 |
| 全局异常 | ✅ 无空 catch 块 (catch {}) |
| 软删除 | ✅ 使用 IsDeleted 查询过滤器 |
| 测试覆盖 | ✅ 充足的集成测试用例 (200+) |

---

## 附录: 文件统计

| 指标 | 数量 |
|------|------|
| C# 源文件 | 100+ |
| 项目数 | 5 (Domain, Application, Infrastructure, Web, Tests) |
| 控制器 | 10+ |
| Service | 8+ |
| 测试用例 | 200+ |

---

**审查完成**  
Generated by Sisyphus Code Review Agent
