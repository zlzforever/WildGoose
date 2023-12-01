namespace WildGoose.Application.User.Admin.V10.Dto;

public class UserDetailDto
{
    public string Id { get; set; }
    public string UserName { get; set; }
    public string Name { get; set; }
    public string PhoneNumber { get; set; }
    public List<OrganizationDto> Organizations { get; set; }
    public List<RoleDto> Roles { get; set; }

    /// <summary>
    ///  
    /// </summary>
    public bool HiddenSensitiveData { get; set; }

    /// <summary>
    /// 离职时间
    /// </summary>
    public long? DepartureTime { get; set; }

    /// <summary>
    /// 职位
    /// </summary>

    public string Title { get; set; }

    /// <summary>
    /// 邮箱
    /// </summary>

    public string Email { get; set; }

    public string Code { get; set; }

    public class OrganizationDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ParentId { get; set; }
        public bool HasChild { get; set; }
    }

    public class RoleDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}