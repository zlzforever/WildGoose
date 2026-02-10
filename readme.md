# WildGoose 用户、权限系统

[![Backend Docker Image CI](https://github.com/zlzforever/WildGoose/actions/workflows/backend.yml/badge.svg)](https://github.com/zlzforever/WildGoose/actions/workflows/backend.yml)

本项目是一个基于 Asp.Net Core Identity 扩展的用户、角色(权限)、组织（层级）管理系统。

## 基础数据表

以下是其部分核心表与核心字段， 其中除 n_id 是自增数字外， 其它的 id 都是类 ObjectId 风格的字符串 ID。n_id 仅用于树型结构的
path 构建， 因为 ObjectId 过于长了。

| 名称             | 表名                                  | 字段                                                                 | 备注                         |
| ---------------- | ------------------------------------- | -------------------------------------------------------------------- | ---------------------------- |
| 用户表           | wild_goose_user                       | id, name, given_name, family_name, user_name, email, phone_number    |                              |
| 用户角色表       | wild_goose_user_role                  | user_id， role_id                                                    |                              |
| 角色表           | wild_goose_role                       | id, name, normalized_name                                            |                              |
| 角色可授于角色表 | wild_goose_role_assignable_role       | role_id, assignable_id                                               | 映射某个角色可以授于哪些角色 |
| 组织表           | wild_goose_organization               | id, name, code, parent_id, n_id                                      | n_id 是自增的数字            |
| 组织用户表       | wild_goose_organization_user          | organization_id， user_id                                            |                              |
| 组织管理员表     | wild_goose_organization_administrator | organization_id, user_id                                             |                              |
| 组织信息归并表   | wild_goose_organization_detail        | id, name, code, parent_id, n_id, parent_name, has_child, path, level | 物化表                       |

## 基本设计

* 组织是用于你业务中的核心组织管理， 用户在全组织下的信息是基本透明的。比如类似飞书，
  通讯录下所有人都可以被查看其角色、所在的所有部门等信息，而不需要去考虑不同部门的信息隔离。
* 如果在实际业务中，还有额外的用户组织逻辑，需要额外自己实现。比如： 本系统你公司用于管理内部人员、组织结构， 同时还有大量第三方、供应商为服务，
  它们是另一个管理领域，只是允许复用 wild_goose_user 表（表示相同的人， 相同的人可以在不同的供应商来回切换）。而不同服务范围（角色）由另一个领域管理，并提供给
  STS 授权时使用。
* 有三个基本角色可以访问本系统： 超级管理员(admin)、组织管理员(organization_admin)、用户管理员(user_admin)。
* 每个角色可以设置： 可授于角色， 可授于角色不考虑继承关系。
* 超级管理员这个角色名就含了最高级别权限， 超级管理员不需要再添加它的可授于角色。
* 用户管理员是用于管理用户、机构等， 仅没有角色的管理权限。因为使用超级太危险了， 特设定这个角色。
* 用户管理员这个角色名就含了用户管理的权限， 用户管理员不需要再添加它的可授于角色。
* 普通用户都可以读取任意组织的信息（全组织透明），只是超级管理员、用户管理可允许修改组织的信息。

## 功能接口

考虑某些环境只允许 GET/POST， 所有接口只允许使用 GET/POST 两种方法。

### 角色管理

只有超级管理员可以管理角色， 普通用户都能够查询角色的可授于角色列表。

#### 角色列表查询接口

GET api/admin/v1.0/roles?page=&limit=&q=

+ 仅超级管理员可查询所有角色。
+ 返回结构中包含角色的可授于角色列表。

#### 添加角色接口

POST api/admin/v1.0/roles

+ 仅超级管理员可添加角色。
+ 请求体中包含角色名称、可授于角色列表。
+ 不允许添加已存在的角色名称。
+ 不允许添加特殊角色（如 admin、organization_admin、user_admin）。
+ 返回新添加的角色的标识。

#### 删除角色接口

POST api/admin/v1.0/roles/{id}/delete

+ 仅超级管理员可删除角色。
+ 请求体中包含角色标识。
+ 不允许删除特殊角色（如 admin、organization_admin、user_admin）。
+ 同时删除该角色在用户角色表中的所有记录。

#### 查询角色信息接口

GET api/admin/v1.0/roles/{id}

+ 仅超级管理员可查询任意角色的信息。
+ 返回角色名称、权限定义。

#### 修改角色接口

POST api/admin/v1.0/roles/{id}

+ 仅超级管理员可修改角色。
+ 请求体中包含角色名称、描述。
+ 不允许修改特殊角色（如 admin、organization_admin、user_admin）。
  
#### 修改角色权限声明

POST api/admin/v1.0/roles/{id}/statement

+ 仅超级管理员可修改角色的权限声明。
+ 请求体中包含新的权限声明。
+ 不允许修改特殊角色（如 admin、organization_admin、user_admin）。

#### 添加可授于角色

POST api/admin/v1.0/roles/{id}/assignableRoles

+ 仅超级管理员可添加角色的可授于角色。
+ 请求体中包含可授于角色列表。
+ 不允许添加特殊角色（如 admin、organization_admin、user_admin）。

#### 删除可授于角色

POST api/admin/v1.0/roles/{id}/assignableRoles/{roleId}/delete

+ 仅超级管理员可删除角色的可授于角色。

#### 查询登录用户的可授于角色列表

GET api/admin/v1.0/roles/assignableRoles

+ 登录用户可查询其可授于角色列表。
+ 返回结构中包含角色的可授于角色列表。
+ 如果是超级管理员、用户管理员，则返回所有角色（机构管理员除外）作为可授于角色列表。
+ 如果是其他用户， 则查询其所有角色的可授于角色列表， 并合并返回。

### 用户管理

#### 用户列表查询接口

GET api/admin/v1.0/users?page=&limit=&organizationId=&status=&q=&isRecursive=

+ 若 organizationId 为空， 组织管理员则查询其有管理权限（含下级）的所有用户， 超级管理员、用户管理员则查询所有用户。
+ 若 organizationId 不为空， isRecursive 参数才生效， 表示查询的时候是否仅查询本级组织。组织管理员需要检查是否有管理 organizationId 的权限。

#### 用户查询接口

GET api/admin/v1.0/users/{id}

+ 超级管理员、用户管理员可查询任意用户的信息。
+ 组织管理员只允许查询其有管理权限（含下级）的用户。
+ 返回用户基本信息（用户名、邮件、电话）、所在的全部部门、所有的角色。

#### 添加用户

POST api/admin/v1.0/users

+ 超级管理员、用户管理员可添加任意用户。用户可以不设置组织、角色。
+ 组织管理员只能添加在其管理范围（含下级）的用户（即用户表单设置时组织不得为空）、设置的角色必须在登录的管理员的所有角色的可授于角色列表。

POST api/admin/v1.1/users

+ 仅用于用户管理员创建简易用户（信息不如v1.0的全）， 一般用于内部服务调用。

#### 修改用户

POST api/admin/v1.0/users/{id}

+ 超级管理员、用户管理员可修改任意用户。用户可以不设置组织、角色。
+ 组织管理员只能修改其管理范围（含下级）的用户，
  设置的用户所属组织必须全部在其管理范围（含下级）、新增的角色必须在登录的管理员的所有角色的可授于角色列表，删除的角色不做权限校验（即若是有角色是其它机构管理员授于的，
  也可以被删除）。

#### 删除用户

POST api/admin/v1.0/users/{id}/remove

+ 超级管理员、用户管理员可以删除任意用户。删除时所有组织映射关系、角色等信息一并清除。
+ 组织管理员只能删除其管理范围（含下级）的用户。

#### 禁用用户

POST api/admin/v1.0/users/{id}/disable

+ 超级管理员、用户管理员可以禁用任意用户。
+ 组织管理员只能禁用其管理范围（含下级）下的用户。
+ 用户一旦被禁用是全局的， 比如额外添加了供应商领域模型， 用户在某一个供应商中， 也会被禁掉这个用户。
  正常来说， 两边人员应该不会重复的，所以应该不存问题。

#### 启用用户

POST api/admin/v1.0/users/{id}/enable

+ 超级管理员、用户管理员可以启用任意用户。
+ 组织管理员只能启用其管理范围（含下级）下的用户。
+ 用户一旦被启用是全局的。

#### 修改密码

POST api/admin/v1.0/users/{id}/password

+ 超级管理员、用户管理员可以修改任意用户密码。
+ 组织管理员只能修改其管理范围（含下级）下的用户的密码。


### 组织管理

#### 查询组织下的子组织列表

GET api/admin/v1.0/organizations/subList?parentId=

