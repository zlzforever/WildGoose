create materialized view wild_goose_organization_detail
    with (autovacuum_enabled=true) as
WITH RECURSIVE cte AS (SELECT t0.*,
                              (SELECT EXISTS(SELECT 1 FROM wild_goose_organization WHERE parent_id = t0.id)) AS has_child,
                              ''::varchar(255)                                                               AS parent_name,
                              t0.n_id::varchar(255)                                                          AS path,
                              t0.name::varchar(1024)                                                         AS branch
                       FROM wild_goose_organization t0
                       WHERE t0.parent_id IS NULL
                         AND t0.is_deleted <> true
                       UNION ALL
                       SELECT origin.*,
                              (SELECT EXISTS(SELECT 1 FROM wild_goose_organization WHERE parent_id = origin.id)) AS has_child,
                              cte_1.name::varchar(255)                                               AS parent_name,
                              ((cte_1.path || '/') || origin.n_id)::varchar(255),
                              ((cte_1.branch || '/') || origin.name)::varchar(1024)
                       FROM cte cte_1
                                JOIN wild_goose_organization origin
                                     ON origin.parent_id = cte_1.id AND origin.is_deleted <> true)
SELECT cte.*,
       length(cte.branch) - length(replace(cte.branch, '/', '')) AS level
FROM cte;

drop index wild_goose_organization_detail_primary_key;
create unique index wild_goose_organization_detail_primary_key
    on wild_goose_organization_detail (id);

