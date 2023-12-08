drop materialized view wild_goose_organization_tree;
create materialized view wild_goose_organization_tree
            (id, name, code, address, description, parent_id, creation_time, creator_id, creator_name, last_modifier_id,
             last_modifier_name, last_modification_time, is_deleted, deleter_id, deleter_name, deletion_time,
             parent_name, path, branch, level)
            WITH (autovacuum_enabled = false)
as
(
WITH RECURSIVE cte AS (SELECT wild_goose_organization.id,
                              wild_goose_organization.name,
                              wild_goose_organization.code,
                              wild_goose_organization.address,
                              wild_goose_organization.description,
                              wild_goose_organization.parent_id,
                              wild_goose_organization.creation_time,
                              wild_goose_organization.creator_id,
                              wild_goose_organization.creator_name,
                              wild_goose_organization.last_modifier_id,
                              wild_goose_organization.last_modifier_name,
                              wild_goose_organization.last_modification_time,
                              wild_goose_organization.is_deleted,
                              wild_goose_organization.deleter_id,
                              wild_goose_organization.deleter_name,
                              wild_goose_organization.deletion_time,
                              ''::text                           AS parent_name,
                              wild_goose_organization.id::text   AS path,
                              wild_goose_organization.name::text AS branch
                       FROM wild_goose_organization
                       WHERE wild_goose_organization.parent_id IS NULL
                         AND wild_goose_organization.is_deleted <> true
                       UNION ALL
                       SELECT origin.id,
                              origin.name,
                              origin.code,
                              origin.address,
                              origin.description,
                              cte_1.id   AS parent_id,
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
                              cte_1.name AS parent_name,
                              (cte_1.path || '/'::text) || origin.id::text,
                              (cte_1.branch || '/'::text) || origin.name::text
                       FROM cte cte_1
                                JOIN wild_goose_organization origin
                                     ON origin.parent_id::text = cte_1.id::text AND origin.is_deleted <> true)
SELECT cte.id,
       cte.name,
       cte.code,
       cte.address,
       cte.description,
       cte.parent_id,
       cte.creation_time,
       cte.creator_id,
       cte.creator_name,
       cte.last_modifier_id,
       cte.last_modifier_name,
       cte.last_modification_time,
       cte.is_deleted,
       cte.deleter_id,
       cte.deleter_name,
       cte.deletion_time,
       cte.parent_name,
       cte.path,
       cte.branch,
       length(cte.branch) - length(replace(cte.branch, '/'::text, ''::text)) AS level
FROM cte );

CREATE UNIQUE INDEX wild_goose_organization_tree_primary_key ON wild_goose_organization_tree (id);
CREATE INDEX wild_goose_organization_tree_parent_id ON wild_goose_organization_tree (parent_id);

create or replace function refresh_wild_goose_organization_tree_func() returns trigger as
$$
declare
begin
    REFRESH MATERIALIZED VIEW CONCURRENTLY wild_goose_organization_tree;
return null;
end;
$$ language plpgsql;

create trigger refresh_wild_goose_organization_tree_trigger
    after insert or update or delete
                    on wild_goose_organization
                        for each row
                        execute procedure refresh_wild_goose_organization_tree_func();