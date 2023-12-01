CREATE MATERIALIZED VIEW {table}
AS
with recursive cte as
                   (select id,
                           name,
                           code,
                           parent_id,
                           cast('' as text)   as parent_name,
                           cast(id as text)   as path,
                           cast(name as text) as branch
                    from cerberus_organization
                    where parent_id is null
                      and is_deleted != 't'
                    union all
-- 通过cte递归查询root节点的直接子节点
                    select origin.id,
                           origin.name,
                           origin.code,
                           cte.id   as parent_id,
                           cte.name as parent_name,
                           cte.path || '/' || origin.id,
                           cte.branch || '/' || origin.name
                    from cte
                             join cerberus_organization as origin
                                  on origin.parent_id = cte.id and origin.is_deleted != 't')
select id,
       name,
       parent_id,
       parent_name,
       path,
       branch,
       -- 通过计算分隔符的个数，模拟计算出树形的深度
       (length(branch) - length(replace(branch, '/', ''))) as level
from cte;

CREATE UNIQUE INDEX uk_{table} ON {table} (id);
CREATE INDEX uk_{table}_parent_id ON {table} (parent_id);