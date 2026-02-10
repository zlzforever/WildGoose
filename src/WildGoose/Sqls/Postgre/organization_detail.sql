create materialized view if not exists ${table_prefix}organization_detail
    with (autovacuum_enabled=true) as
WITH RECURSIVE cte AS (SELECT t0.*,
                              (SELECT EXISTS(SELECT 1 FROM ${table_prefix}organization WHERE parent_id = t0.id)) AS has_child,
                              ''::varchar(255)                                                               AS parent_name,
                              (t0.n_id || '/')::varchar(255)                                               AS path,
                              t0.name::varchar(1024)                                              AS branch
                       FROM ${table_prefix}organization t0
                       WHERE t0.parent_id IS NULL
                         AND t0.is_deleted <> true
                       UNION ALL
                       SELECT origin.*,
                              (SELECT EXISTS(SELECT 1 FROM ${table_prefix}organization WHERE parent_id = origin.id)) AS has_child,
                              cte_1.name::varchar(255)                                               AS parent_name,
                              (cte_1.path || origin.n_id || '/')::varchar(255),
                              ((cte_1.branch || '/') || origin.name)::varchar(1024)
                       FROM cte cte_1
                                JOIN ${table_prefix}organization origin
                                     ON origin.parent_id = cte_1.id AND origin.is_deleted <> true)
SELECT cte.*,
       length(cte.branch) - length(replace(cte.branch, '/', '')) AS level
FROM cte;

create unique index if not exists ${table_prefix}organization_detail_primary_key
    on ${table_prefix}organization_detail (id);

create index if not exists ${table_prefix}organization_detail_path_index
    on ${table_prefix}organization_detail (path);

create index if not exists ${table_prefix}organization_detail_parent_id_index
    on ${table_prefix}organization_detail (parent_id);

-- 创建刷新物化视图的函数
CREATE OR REPLACE FUNCTION refresh_${table_prefix}organization_detail() 
RETURNS TRIGGER AS $$
BEGIN
    REFRESH MATERIALIZED VIEW CONCURRENTLY ${table_prefix}organization_detail;
RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DO $$
BEGIN
        IF NOT EXISTS (
            SELECT 1
            FROM pg_trigger
            WHERE tgrelid = '${table_prefix}organization'::regclass  -- 表名
              AND tgname = 'refresh_${table_prefix}organization_detail_trigger'  -- 触发器名
        ) THEN
CREATE TRIGGER  refresh_${table_prefix}organization_detail_trigger
    AFTER INSERT OR UPDATE OR DELETE OR TRUNCATE
                    ON ${table_prefix}organization
                        FOR EACH STATEMENT
                        EXECUTE FUNCTION refresh_${table_prefix}organization_detail();
END IF;
END $$;  
