// ReSharper disable UnusedAutoPropertyAccessor.Global

using System.Text.Json.Serialization;
using WildGoose.Domain.Entity;

namespace WildGoose.Application.User.Admin.V10.Dto;

/// <summary>
/// 用户详情数据传输对象
/// 用于封装用户的基础信息、关联组织、角色及敏感信息控制等数据
/// </summary>
public class UserDetailDto
{
    /// <summary>
    /// 用户唯一标识
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// 用户名（登录名）
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// 用户真实姓名
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 手机号码
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// 用户关联的组织列表
    /// </summary>
    public IEnumerable<OrganizationEntity> Organizations { get; set; }

    /// <summary>
    /// 用户拥有的角色列表
    /// </summary>
    public List<RoleDto> Roles { get; set; }

    /// <summary>
    /// 是否隐藏敏感数据
    /// 用于控制用户敏感信息（手机号、邮箱等）的展示权限
    /// </summary>
    public bool HiddenSensitiveData { get; set; }

    /// <summary>
    /// 离职时间
    /// 时间戳格式，可为空（在职用户无此值）
    /// </summary>
    public long? DepartureTime { get; set; }

    /// <summary>
    /// 职位
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// 邮箱地址
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// 用户编码（唯一业务编码）
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// 角色数据传输对象
    /// 封装角色的基础标识和名称信息
    /// </summary>
    public class RoleDto
    {
        /// <summary>
        /// 角色唯一标识
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 角色名称
        /// </summary>
        public string Name { get; set; }
    }
}