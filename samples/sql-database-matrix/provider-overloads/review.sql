-- Run on the single public fixture index. Use names to select the roots;
-- use the artifact, module, metadata token and target identity for the joins.
WITH methods AS (
    SELECT scan_id, target_symbol AS identity,
           properties_json ->> '$.metadataName' AS name,
           properties_json ->> '$.metadataToken' AS token,
           properties_json ->> '$.moduleMvid' AS module,
           properties_json ->> '$.rawFileSha256' AS artifact
    FROM facts WHERE fact_type = 'ManagedMethodDeclared'
), calls AS (
    SELECT m.*, c.properties_json ->> '$.targetIdentity' AS target,
           c.properties_json ->> '$.referenceToken' AS target_token,
           c.properties_json ->> '$.opcode' AS opcode
    FROM facts c JOIN methods m
      ON c.scan_id = m.scan_id AND c.properties_json ->> '$.rawFileSha256' = m.artifact
     AND c.properties_json ->> '$.moduleMvid' = m.module
     AND c.properties_json ->> '$.metadataToken' = m.token
    WHERE c.fact_type = 'ManagedIlCallObserved'
)
SELECT entry.name AS root, via_list.token AS list_overload, terminal.token AS array_overload,
       ctor.target AS adapter_constructor, terminal.target AS fill_target
FROM calls entry
JOIN calls via_list ON via_list.scan_id = entry.scan_id AND via_list.artifact = entry.artifact
  AND via_list.module = entry.module AND via_list.token = entry.target_token AND via_list.identity = entry.target
JOIN calls terminal ON terminal.scan_id = via_list.scan_id AND terminal.artifact = via_list.artifact
  AND terminal.module = via_list.module AND terminal.token = via_list.target_token AND terminal.identity = via_list.target
JOIN calls ctor ON ctor.scan_id = terminal.scan_id AND ctor.artifact = terminal.artifact
  AND ctor.module = terminal.module AND ctor.token = terminal.token
WHERE entry.name IN ('FromLedger', 'FromDriver')
  AND via_list.name = 'ReadBatch' AND terminal.name = 'ReadBatch'
  AND terminal.target LIKE '%|member:4:Fill|%'
  AND ctor.opcode = 'newobj' AND ctor.target LIKE '%DataAdapter)|member:5:.ctor|%'
ORDER BY entry.name;
