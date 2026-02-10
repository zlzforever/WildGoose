// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace WildGoose.Application.User.Admin.V10.Dto;

/// <summary>
/// 用户数据传输对象（DTO）
/// 用于系统中用户信息的传输、展示、入参/出参封装，解耦领域模型与外部交互
/// </summary>
public class UserDto
{
    /// <summary>
    /// 用户唯一标识
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// 系统登录用户名
    /// 唯一不可重复，用于账号登录验证
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// 用户真实姓名/显示名称
    /// 用于系统内展示、消息通知等业务场景
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 用户手机号码
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// 用户所属机构编码/标识集合
    /// 一个用户可归属多个机构，存储机构唯一标识（如机构ID、编码）
    /// </summary>
    public IEnumerable<string> Organizations { get; set; }

    /// <summary>
    /// 用户创建时间
    /// </summary>
    public string CreationTime { get; set; }

    /// <summary>
    /// 账号启用状态
    /// true-启用（可正常登录），false-禁用（账号锁定，无法登录）
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 是否为系统管理员
    /// 可空布尔值：null-未配置/未判定，true-超级管理员，false-普通用户
    /// 管理员通常拥有系统最高操作权限
    /// </summary>
    public bool? IsAdministrator { get; set; }

    /// <summary>
    /// 用户所属角色编码/标识集合
    /// 一个用户可拥有多个角色，基于角色实现权限控制（RBAC），存储角色唯一标识
    /// </summary>
    public IEnumerable<string> Roles { get; set; }
}