namespace WildGoose.Domain;

/// <summary>
/// 统一错误代码定义
/// 错误码范围规划：
/// - 1xxx: 通用业务错误（验证失败、资源不存在等）
/// - 2xxx: 权限相关错误
/// - 3xxx: 服务器错误
/// - 4xxx: 用户相关错误
/// - 5xxx: 组织相关错误
/// - 6xxx: 角色相关错误
/// - 7xxx: 文件/上传相关错误
/// </summary>
public static class ErrorCodes
{
    #region 通用业务错误 (1xxx)

    /// <summary>通用：参数不能为空</summary>
    public const int Required = 1001;

    /// <summary>通用：数据不存在</summary>
    public const int NotFound = 1002;

    /// <summary>通用：数据已存在</summary>
    public const int AlreadyExists = 1003;

    /// <summary>通用：操作失败</summary>
    public const int OperationFailed = 1004;

    /// <summary>通用：无效参数</summary>
    public const int InvalidParameter = 1005;

    /// <summary>通用：格式无效</summary>
    public const int InvalidFormat = 1006;

    /// <summary>通用：超出范围</summary>
    public const int OutOfRange = 1007;

    #endregion

    #region 权限相关 (2xxx)

    /// <summary>权限：访问被拒绝</summary>
    public const int Forbidden = 2001;

    /// <summary>权限：权限不足</summary>
    public const int InsufficientPermission = 2002;

    /// <summary>权限：禁止操作（系统保留）</summary>
    public const int SystemReserved = 2003;

    #endregion

    #region 服务器错误 (3xxx)

    /// <summary>服务器：内部错误</summary>
    public const int InternalError = 3001;

    /// <summary>服务器：保存失败</summary>
    public const int SaveFailed = 3002;

    /// <summary>服务器：服务不可用</summary>
    public const int ServiceUnavailable = 3003;

    #endregion

    #region 用户相关 (4xxx)

    /// <summary>用户：不存在</summary>
    public const int UserNotFound = 4001;

    /// <summary>用户：禁止删除自己</summary>
    public const int CannotDeleteSelf = 4002;

    /// <summary>用户：禁止禁用自己</summary>
    public const int CannotDisableSelf = 4003;

    /// <summary>用户：账户已被禁用</summary>
    public const int UserDisabled = 4004;

    /// <summary>用户：无效角色</summary>
    public const int InvalidRole = 4005;

    /// <summary>用户：密码错误</summary>
    public const int InvalidPassword = 4006;

    #endregion

    #region 组织相关 (5xxx)

    /// <summary>组织：不存在</summary>
    public const int OrganizationNotFound = 5001;

    /// <summary>组织：父组织不存在</summary>
    public const int ParentOrganizationNotFound = 5002;

    /// <summary>组织：上级组织不存在</summary>
    public const int AncestorOrganizationNotFound = 5003;

    /// <summary>组织：没有管理权限</summary>
    public const int NoOrganizationPermission = 5004;

    /// <summary>组织：仅允许超级管理员操作</summary>
    public const int SuperAdminOnly = 5005;

    /// <summary>组织：存在下级组织</summary>
    public const int HasChildOrganizations = 5006;

    /// <summary>组织：存在关联用户</summary>
    public const int HasRelatedUsers = 5007;

    /// <summary>组织：禁止操作顶级组织</summary>
    public const int CannotModifyTopLevelOrganization = 5008;

    #endregion

    #region 角色相关 (6xxx)

    /// <summary>角色：不存在</summary>
    public const int RoleNotFound = 6001;

    /// <summary>角色：已存在</summary>
    public const int RoleAlreadyExists = 6002;

    /// <summary>角色：禁止操作系统角色</summary>
    public const int CannotModifySystemRole = 6003;

    /// <summary>角色：禁止使用系统角色名</summary>
    public const int SystemRoleNameReserved = 6004;

    /// <summary>角色：存在不可授于的角色</summary>
    public const int NonAssignableRole = 6005;

    /// <summary>角色：角色名称不能为空</summary>
    public const int RoleNameRequired = 6006;

    #endregion

    #region 文件/上传相关 (7xxx)

    /// <summary>文件：文件为空</summary>
    public const int FileEmpty = 7001;

    /// <summary>文件：文件过大</summary>
    public const int FileTooLarge = 7002;

    /// <summary>文件：不支持的格式</summary>
    public const int UnsupportedFileFormat = 7003;

    /// <summary>文件：上传失败</summary>
    public const int UploadFailed = 7004;

    #endregion

    #region 认证相关 (8xxx)

    /// <summary>认证：ApiName 为空</summary>
    public const int ApiNameRequired = 8001;

    /// <summary>认证：上下文异常</summary>
    public const int ContextError = 8002;

    #endregion

    public static string GetMessage(int code)
    {
        return code switch
        {
            Required => "参数不能为空",
            NotFound => "数据不存在",
            AlreadyExists => "数据已存在",
            OperationFailed => "操作失败",
            InvalidParameter => "无效参数",
            InvalidFormat => "格式无效",
            OutOfRange => "超出范围",
            Forbidden => "访问被拒绝",
            InsufficientPermission => "权限不足",
            SystemReserved => "禁止操作（系统保留）",
            InternalError => "服务器内部错误",
            SaveFailed => "保存失败",
            ServiceUnavailable => "服务不可用",
            UserNotFound => "用户不存在",
            CannotDeleteSelf => "禁止删除自己",
            CannotDisableSelf => "禁止禁用自己",
            UserDisabled => "用户已被禁用",
            InvalidRole => "无效角色",
            InvalidPassword => "密码错误",
            OrganizationNotFound => "机构不存在",
            ParentOrganizationNotFound => "父机构不存在",
            AncestorOrganizationNotFound => "上级机构不存在",
            NoOrganizationPermission => "没有管理机构的权限",
            SuperAdminOnly => "仅允许超级管理员操作",
            HasChildOrganizations => "请先删除下级机构",
            HasRelatedUsers => "请先删除关联用户",
            CannotModifyTopLevelOrganization => "仅允许超级管理员操作一级机构",
            RoleNotFound => "角色不存在",
            RoleAlreadyExists => "角色已经存在",
            CannotModifySystemRole => "禁止操作系统角色信息",
            SystemRoleNameReserved => "禁止使用系统角色名",
            NonAssignableRole => "存在不可授于的角色",
            RoleNameRequired => "角色名称不能为空",
            FileEmpty => "上传文件为空",
            FileTooLarge => "文件过大",
            UnsupportedFileFormat => "不支持的文件格式",
            UploadFailed => "上传失败",
            ApiNameRequired => "ApiName is null or empty",
            ContextError => "HTTP 上下文异常",
            _ => "未知错误"
        };
    }
}
