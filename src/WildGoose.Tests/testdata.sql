-- 清理现有数据（按依赖关系逆序删除）
TRUNCATE TABLE wild_goose_organization_user CASCADE;
TRUNCATE TABLE wild_goose_organization_administrator CASCADE;
TRUNCATE TABLE wild_goose_organization CASCADE;
TRUNCATE TABLE wild_goose_role_assignable_role CASCADE;
TRUNCATE TABLE wild_goose_user_role CASCADE;
TRUNCATE TABLE wild_goose_role CASCADE;
TRUNCATE TABLE wild_goose_user CASCADE;

-- 重置序列
SELECT setval('wild_goose_organization_n_id_seq', 1, false);

-- ============================================================================
-- 1. 角色数据 (wild_goose_role)
-- ============================================================================
INSERT INTO wild_goose_role (id, name, normalized_name, description, version, statement, creation_time, creator_id,
                             creator_name, last_modifier_id, last_modifier_name, last_modification_time)
VALUES
    -- 基础角色
    ('507f1f77bcf86cd799439011', 'admin', 'ADMIN', '超级管理员', 1,
     '[{"effect":"Allow","resource":["*"],"action":["*"]}]', 110, 'system', 'system', 'system', 'system', 110),
    ('507f1f77bcf86cd799439012', 'organization-admin', 'ORGANIZATION-ADMIN', '组织管理员', 1, '[]', 110, 'system',
     'system', 'system', 'system', 110),
    ('507f1f77bcf86cd799439013', 'user-admin', 'USER-ADMIN', '用户管理员', 1, '[]', 110, 'system', 'system', 'system',
     'system', 110),
    -- 业务角色
    ('507f1f77bcf86cd799439014', 'manager', 'MANAGER', '部门经理', 1, '[]', 110, 'system', 'system', 'system', 'system',
     110),
    ('507f1f77bcf86cd799439015', 'employee', 'EMPLOYEE', '普通员工', 1, '[]', 110, 'system', 'system', 'system',
     'system', 110),
    ('507f1f77bcf86cd799439016', 'intern', 'INTERN', '实习生', 1, '[]', 110, 'system', 'system', 'system', 'system',
     110);

-- ============================================================================
-- 2. 角色可授予角色关系 (wild_goose_role_assignable_role)
-- 说明: 定义某角色可以授予哪些角色给其他用户
-- ============================================================================
INSERT INTO wild_goose_role_assignable_role (role_id, assignable_id)
VALUES
    -- organization-admin 可以授予部分角色
    ('507f1f77bcf86cd799439012', '507f1f77bcf86cd799439014'), -- organization-admin -> manager
    ('507f1f77bcf86cd799439012', '507f1f77bcf86cd799439015'), -- organization-admin -> employee
    ('507f1f77bcf86cd799439012', '507f1f77bcf86cd799439016'), -- organization-admin -> intern

    -- user-admin 可以授予业务角色
    ('507f1f77bcf86cd799439013', '507f1f77bcf86cd799439015'), -- user-admin -> employee
    ('507f1f77bcf86cd799439013', '507f1f77bcf86cd799439016');
-- user-admin -> intern

-- ============================================================================
-- 3. 用户数据 (wild_goose_user)
-- 说明: 密码哈希值使用 ASP.NET Core Identity 的默认哈希算法
--      默认密码统一为: Test@123456
--      实际使用时需要通过 Identity 框架创建用户来生成正确的 PasswordHash
-- ============================================================================
INSERT INTO wild_goose_user (id, user_name, normalized_user_name, email, normalized_email, email_confirmed,
                             password_hash, security_stamp, concurrency_stamp, phone_number, phone_number_confirmed,
                             two_factor_enabled, lockout_end, lockout_enabled, access_failed_count,
                             name, given_name, family_name, middle_name, nick_name, picture, address, code,
                             creation_time, creator_id, creator_name, is_deleted)
VALUES
    -- 超级管理员
    ('507f1f77bcf86cd799439020', 'admin', 'ADMIN', 'admin@wildgoose.com', 'ADMIN@WILDGOOSE.COM', true,
     'AQAAAAIAAYagAAAAEM+gVJkgN1IJO3VKK5FfKw2QqC3qFKJ4FnYGvK0OWOZXt7Yy9/8Xh4cWT2H1OzE5EA==', -- Test@123456
     'TESTSTAMP', 'teststamp', NULL, false, false, NULL, false, 0,
     '系统管理员', '系统', '管理员', NULL, 'SuperAdmin', NULL, NULL, 'ADM001',
     110, 'system', 'system', false),

    -- 用户管理员
    ('507f1f77bcf86cd799439021', 'user_admin', 'USER_ADMIN', 'user_admin@wildgoose.com', 'USER_ADMIN@WILDGOOSE.COM',
     true,
     'AQAAAAIAAYagAAAAEM+gVJkgN1IJO3VKK5FfKw2QqC3qFKJ4FnYGvK0OWOZXt7Yy9/8Xh4cWT2H1OzE5EA==',
     'TESTSTAMP', 'teststamp', '13800138001', true, false, NULL, false, 0,
     '张三', '三', '张', NULL, 'UserAdmin', NULL, '北京市朝阳区', 'USR001',
     110, 'system', 'system', false),

    -- 总公司组织管理员
    ('507f1f77bcf86cd799439022', 'org_admin_hq', 'ORG_ADMIN_HQ', 'org_admin_hq@wildgoose.com',
     'ORG_ADMIN_HQ@WILDGOOSE.COM', true,
     'AQAAAAIAAYagAAAAEM+gVJkgN1IJO3VKK5FfKw2QqC3qFKJ4FnYGvK0OWOZXt7Yy9/8Xh4cWT2H1OzE5EA==',
     'TESTSTAMP', 'teststamp', '13800138002', true, false, NULL, false, 0,
     '李四', '四', '李', NULL, 'HQAdmin', NULL, '北京市海淀区', 'ORG001',
     110, 'system', 'system', false),

    -- 技术部管理员
    ('507f1f77bcf86cd799439023', 'tech_manager', 'TECH_MANAGER', 'tech_manager@wildgoose.com',
     'TECH_MANAGER@WILDGOOSE.COM', true,
     'AQAAAAIAAYagAAAAEM+gVJkgN1IJO3VKK5FfKw2QqC3qFKJ4FnYGvK0OWOZXt7Yy9/8Xh4cWT2H1OzE5EA==',
     'TESTSTAMP', 'teststamp', '13800138003', true, false, NULL, false, 0,
     '王五', '五', '王', NULL, 'TechLead', NULL, '北京市朝阳区', 'TEC001',
     110, 'system', 'system', false),

    -- 销售部管理员
    ('507f1f77bcf86cd799439024', 'sales_manager', 'SALES_MANAGER', 'sales_manager@wildgoose.com',
     'SALES_MANAGER@WILDGOOSE.COM', true,
     'AQAAAAIAAYagAAAAEM+gVJkgN1IJO3VKK5FfKw2QqC3qFKJ4FnYGvK0OWOZXt7Yy9/8Xh4cWT2H1OzE5EA==',
     'TESTSTAMP', 'teststamp', '13800138004', true, false, NULL, false, 0,
     '赵六', '六', '赵', NULL, 'SalesLead', NULL, '北京市西城区', 'SAL001',
     110, 'system', 'system', false),

    -- 普通员工
    ('507f1f77bcf86cd799439025', 'developer_01', 'DEVELOPER_01', 'developer_01@wildgoose.com',
     'DEVELOPER_01@WILDGOOSE.COM', true,
     'AQAAAAIAAYagAAAAEM+gVJkgN1IJO3VKK5FfKw2QqC3qFKJ4FnYGvK0OWOZXt7Yy9/8Xh4cWT2H1OzE5EA==',
     'TESTSTAMP', 'teststamp', '13800138005', true, false, NULL, false, 0,
     '小明', '明', '孙', NULL, 'Coder', NULL, '北京市朝阳区', 'DEV001',
     110, 'system', 'system', false),

    ('507f1f77bcf86cd799439026', 'developer_02', 'DEVELOPER_02', 'developer_02@wildgoose.com',
     'DEVELOPER_02@WILDGOOSE.COM', true,
     'AQAAAAIAAYagAAAAEM+gVJkgN1IJO3VKK5FfKw2QqC3qFKJ4FnYGvK0OWOZXt7Yy9/8Xh4cWT2H1OzE5EA==',
     'TESTSTAMP', 'teststamp', '13800138006', true, false, NULL, false, 0,
     '小红', '红', '周', NULL, 'DevGirl', NULL, '北京市朝阳区', 'DEV002',
     110, 'system', 'system', false),

    ('507f1f77bcf86cd799439027', 'sales_01', 'SALES_01', 'sales_01@wildgoose.com', 'SALES_01@WILDGOOSE.COM', true,
     'AQAAAAIAAYagAAAAEM+gVJkgN1IJO3VKK5FfKw2QqC3qFKJ4FnYGvK0OWOZXt7Yy9/8Xh4cWT2H1OzE5EA==',
     'TESTSTAMP', 'teststamp', '13800138007', true, false, NULL, false, 0,
     '小刚', '刚', '吴', NULL, 'SalesBoy', NULL, '北京市西城区', 'SAL002',
     110, 'system', 'system', false),

    -- 实习生
    ('507f1f77bcf86cd799439028', 'intern_01', 'INTERN_01', 'intern_01@wildgoose.com', 'INTERN_01@WILDGOOSE.COM', true,
     'AQAAAAIAAYagAAAAEM+gVJkgN1IJO3VKK5FfKw2QqC3qFKJ4FnYGvK0OWOZXt7Yy9/8Xh4cWT2H1OzE5EA==',
     'TESTSTAMP', 'teststamp', '13800138008', true, false, NULL, false, 0,
     '小华', '华', '郑', NULL, 'InternNewbie', NULL, '北京市朝阳区', 'INT001',
     110, 'system', 'system', false),

    -- 被禁用的用户（用于测试禁用功能）
    ('507f1f77bcf86cd799439029', 'disabled_user', 'DISABLED_USER', 'disabled_user@wildgoose.com',
     'DISABLED_USER@WILDGOOSE.COM', true,
     'AQAAAAIAAYagAAAAEM+gVJkgN1IJO3VKK5FfKw2QqC3qFKJ4FnYGvK0OWOZXt7Yy9/8Xh4cWT2H1OzE5EA==',
     'TESTSTAMP', 'teststamp', '13800138009', true, false, '2025-12-01 00:00:00', true, 5,
     '被禁用用户', '禁用', '用户', NULL, 'Disabled', NULL, '北京市海淀区', 'DIS001',
     110, 'system', 'system', false);

-- ============================================================================
-- 4. 用户角色关联 (wild_goose_user_role)
-- ============================================================================
INSERT INTO wild_goose_user_role (user_id, role_id)
VALUES
    -- user_admin 拥有用户管理员角色
    ('507f1f77bcf86cd799439021', '507f1f77bcf86cd799439013'),

    -- org_admin_hq 拥有组织管理员角色
    ('507f1f77bcf86cd799439022', '507f1f77bcf86cd799439012'),

    -- tech_manager 同时拥有组织管理员和经理角色
    ('507f1f77bcf86cd799439023', '507f1f77bcf86cd799439012'), -- organization-admin
    ('507f1f77bcf86cd799439023', '507f1f77bcf86cd799439014'), -- manager

    -- sales_manager 拥有经理角色
    ('507f1f77bcf86cd799439024', '507f1f77bcf86cd799439014'), -- manager

    -- 普通员工
    ('507f1f77bcf86cd799439025', '507f1f77bcf86cd799439015'), -- developer_01 -> employee
    ('507f1f77bcf86cd799439026', '507f1f77bcf86cd799439015'), -- developer_02 -> employee
    ('507f1f77bcf86cd799439027', '507f1f77bcf86cd799439015'), -- sales_01 -> employee

    -- 实习生
    ('507f1f77bcf86cd799439028', '507f1f77bcf86cd799439016');
-- intern_01 -> intern

-- ============================================================================
-- 5. 组织数据 (wild_goose_organization)
-- 组织层级结构:
--   总公司 (n_id=1)
--     ├── 技术部 (n_id=2)
--     │   ├── 前端组 (n_id=5)
--     │   └── 后端组 (n_id=6)
--     └── 销售部 (n_id=3)
--         └── 华北区 (n_id=4)
-- 研发中心 (n_id=7) - 独立部门
-- ============================================================================
INSERT INTO wild_goose_organization (id, name, code, parent_id, n_id, address, description,
                                     creation_time, creator_id, creator_name, is_deleted)
VALUES
    -- 根组织: 总公司
    ('507f1f77bcf86cd799439030', 'WildGoose总公司', 'HQ', NULL, 1, '北京市海淀区中关村', '公司总部', 110, 'system',
     'system', false),

    -- 二级组织
    ('507f1f77bcf86cd799439031', '技术部', 'TECH', '507f1f77bcf86cd799439030', 2, '北京市朝阳区望京', '负责技术研发',
     110, 'system', 'system', false),
    ('507f1f77bcf86cd799439032', '销售部', 'SALES', '507f1f77bcf86cd799439030', 3, '北京市西城区金融街', '负责销售业务',
     110, 'system', 'system', false),
    ('507f1f77bcf86cd799439038', '研发中心', 'R&D', NULL, 7, '上海市浦东新区', '独立研发部门', 110, 'system', 'system',
     false),

    -- 三级组织 - 技术部下级
    ('507f1f77bcf86cd799439033', '前端组', 'FRONTEND', '507f1f77bcf86cd799439031', 5, '北京市朝阳区望京SOHO',
     '前端开发团队', 110, 'system', 'system', false),
    ('507f1f77bcf86cd799439034', '后端组', 'BACKEND', '507f1f77bcf86cd799439031', 6, '北京市朝阳区望京SOHO',
     '后端开发团队', 110, 'system', 'system', false),

    -- 三级组织 - 销售部下级
    ('507f1f77bcf86cd799439035', '华北区', 'NORTH', '507f1f77bcf86cd799439032', 4, '北京市西城区金融街', '华北销售区域',
     110, 'system', 'system', false);

-- ============================================================================
-- 6. 组织用户关联 (wild_goose_organization_user)
-- 说明: 用户可以属于多个组织
-- ============================================================================
INSERT INTO wild_goose_organization_user (organization_id, user_id)
VALUES
    -- user_admin 属于总公司
    ('507f1f77bcf86cd799439030', '507f1f77bcf86cd799439021'),

    -- org_admin_hq 是总公司的组织管理员
    ('507f1f77bcf86cd799439030', '507f1f77bcf86cd799439022'),

    -- tech_manager 属于技术部和前端组（多组织）
    ('507f1f77bcf86cd799439031', '507f1f77bcf86cd799439023'),
    ('507f1f77bcf86cd799439033', '507f1f77bcf86cd799439023'),

    -- sales_manager 属于销售部和华北区
    ('507f1f77bcf86cd799439032', '507f1f77bcf86cd799439024'),
    ('507f1f77bcf86cd799439035', '507f1f77bcf86cd799439024'),

    -- 开发人员
    ('507f1f77bcf86cd799439033', '507f1f77bcf86cd799439025'), -- developer_01 -> 前端组
    ('507f1f77bcf86cd799439034', '507f1f77bcf86cd799439026'), -- developer_02 -> 后端组

    -- 销售人员
    ('507f1f77bcf86cd799439035', '507f1f77bcf86cd799439027'), -- sales_01 -> 华北区

    -- 实习生属于技术部（用于测试组织管理员权限）
    ('507f1f77bcf86cd799439031', '507f1f77bcf86cd799439028'), -- intern_01 -> 技术部

    -- 禁用用户
    ('507f1f77bcf86cd799439031', '507f1f77bcf86cd799439029');
-- disabled_user -> 技术部

-- ============================================================================
-- 7. 组织管理员 (wild_goose_organization_administrator)
-- 说明: 定义哪些用户是哪些组织的管理员
-- ============================================================================
INSERT INTO wild_goose_organization_administrator (organization_id, user_id)
VALUES
    -- org_admin_hq 是总公司的管理员
    ('507f1f77bcf86cd799439030', '507f1f77bcf86cd799439022'),

    -- tech_manager 是技术部的管理员（可以管理技术部及其下级组织）
    ('507f1f77bcf86cd799439031', '507f1f77bcf86cd799439023'),

    -- sales_manager 是销售部的管理员
    ('507f1f77bcf86cd799439032', '507f1f77bcf86cd799439024');