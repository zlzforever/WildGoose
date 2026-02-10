create table if not exists cerberus_organization_detail
(
    id                     varchar(36)   not null,
    name                   varchar(255)  null,
    code                   varchar(255)  null,
    address                varchar(255)  null,
    description            varchar(1024) null,
    parent_id              varchar(36)   null,
    metadata               varchar(2000) null,
    n_id                   bigint        null,
    creation_time          int           null,
    creator_id             varchar(36)   null,
    creator_name           varchar(255)  null,
    last_modifier_id       varchar(36)   null,
    last_modifier_name     varchar(255)  null,
    last_modification_time int           null,
    is_deleted             tinyint       null,
    deleter_id             varchar(36)   null,
    deleter_name           varchar(255)  null,
    deletion_time          int           null,
    has_child              tinyint       null,
    parent_name            varchar(255)  null,
    path                   varchar(255)  null,
    branch                 varchar(1024) null,
    level                  int           null,
    PRIMARY KEY (id)
    ) ENGINE = InnoDB;
create index cerberus_organization_detail_path_index
    on cerberus_organization_detail (path);
create index cerberus_organization_detail_parent_id_index
    on cerberus_organization_detail (parent_id);

create table if not exists cerberus_organization_detail_tmp
(
    id                     varchar(36)   not null,
    name                   varchar(255)  null,
    code                   varchar(255)  null,
    address                varchar(255)  null,
    description            varchar(1024) null,
    parent_id              varchar(36)   null,
    metadata               varchar(2000) null,
    n_id                   bigint        null,
    creation_time          int           null,
    creator_id             varchar(36)   null,
    creator_name           varchar(255)  null,
    last_modifier_id       varchar(36)   null,
    last_modifier_name     varchar(255)  null,
    last_modification_time int           null,
    is_deleted             tinyint       null,
    deleter_id             varchar(36)   null,
    deleter_name           varchar(255)  null,
    deletion_time          int           null,
    has_child              tinyint       null,
    parent_name            varchar(255)  null,
    path                   varchar(255)  null,
    branch                 varchar(1024) null,
    level                  int           null,
    PRIMARY KEY (id)
    ) ENGINE = InnoDB;
create index cerberus_organization_detail_path_index
    on cerberus_organization_detail_tmp (path);
create index cerberus_organization_detail_parent_id_index
    on cerberus_organization_detail_tmp (parent_id);

CREATE PROCEDURE if not exists refresh_organization_detail()
BEGIN
-- 步骤1：清空备用表（此时主表仍对外提供数据，无影响）
TRUNCATE TABLE cerberus_organization_detail_tmp;
-- 步骤2：将新数据写入备用表
insert into `cerberus_organization_detail_tmp`
WITH
    RECURSIVE cte AS (SELECT id,
                             name,
                             code,
                             address,
                             description,
                             parent_id,
                             metadata,
                             n_id,
                             creation_time,
                             creator_id,
                             creator_name,
                             last_modifier_id,
                             last_modifier_name,
                             last_modification_time,
                             is_deleted,
                             deleter_id,
                             deleter_name,
                             deletion_time,
                             -- EXISTS 查询判断是否有子节点（MySQL 语法）
                             (SELECT EXISTS(SELECT 1 FROM `cerberus_organization` WHERE parent_id = t0.id)) AS has_child,
                             -- 字符串类型转换（MySQL 用 CAST 替代 ::）
                             CAST('' AS CHAR(255))                                                          AS parent_name,
                             CAST(CONCAT(t0.n_id, '/') AS CHAR)                                             AS path,
                             CAST(t0.name AS CHAR)                                                          AS branch
                      FROM `cerberus_organization` t0
                      WHERE t0.parent_id IS NULL
                        AND t0.is_deleted != 1 -- MySQL 中布尔值实际存储为 1/0，用 != 1 替代 <> true
UNION ALL
SELECT origin.id,
       origin.name,
       origin.code,
       origin.address,
       origin.description,
       origin.parent_id,
       origin.metadata,
       origin.n_id,
       origin.creation_time,
       origin.creator_id,
       origin.creator_name,
       origin.last_modifier_id,
       origin.last_modifier_name,
       origin.last_modification_time,
       origin.is_deleted,
       origin.deleter_id,
       origin.deleter_name,
       origin.deletion_time,
       (SELECT EXISTS(SELECT 1 FROM `cerberus_organization` WHERE parent_id = origin.id)) AS has_child,
       CAST(cte_1.name AS CHAR)                                                           AS parent_name,
       CAST(CONCAT(cte_1.path, origin.n_id, '/') AS CHAR)                                 AS path,
       CAST(CONCAT(cte_1.branch, '/', origin.name) AS CHAR)                               AS branch
FROM cte cte_1
         JOIN `cerberus_organization` origin
              ON origin.parent_id = cte_1.id
                  AND origin.is_deleted != 1)
SELECT cte.*,
       -- 计算层级（MySQL 中 LENGTH 是字节长度，CHAR_LENGTH 是字符长度，这里用 CHAR_LENGTH 更准确）
       CHAR_LENGTH(cte.branch) - CHAR_LENGTH(REPLACE(cte.branch, '/', '')) AS level
FROM cte;

-- 步骤3：原子性切换表（RENAME 是原子操作，耗时微秒级）
RENAME TABLE
        cerberus_organization_detail TO z_cerberus_organization_detail_old,
        cerberus_organization_detail_tmp TO cerberus_organization_detail,
        z_cerberus_organization_detail_old TO cerberus_organization_detail_tmp;
-- 步骤4：清空备用表
TRUNCATE TABLE cerberus_organization_detail_tmp;
END;