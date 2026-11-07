# Synthetic regular WebForms bundle discovery

Projectless source fixture, not a runnable website. No Angular dependency.
`Default.aspx` references `Site.master`, whose server output expression refers to
`~/bundles/app`. `BundleConfig.vb` declares two explicit member paths. The inline
click names a function in `main.js` that contains a synthetic HTTP request.

Expected new evidence: one bundle declaration candidate, two ordered member
candidates and one master-page bundle-reference candidate. Member facts link to
the declaration by fact ID. References do not automatically join registrations
by virtual-path equality. No startup registration or runtime ordering is proven.

WebForms inventories now include standalone JavaScript. This original fixture
uses a plain HTML onclick and verifies bundle discovery. The separate
`samples/webforms-angularjs-click` fixture exercises AngularJS click ownership.

To inspect the declared members in the emitted SQLite database:

```sql
SELECT d.file_path AS declaration_file,
       d.target_symbol AS declared_bundle_path,
       m.target_symbol AS declared_member_path,
       json_extract(m.properties_json, '$.declaredOrdinal') AS declared_ordinal
FROM facts AS m
JOIN facts AS d
  ON d.fact_id = json_extract(m.properties_json, '$.bundleDeclarationFactId')
WHERE m.fact_type = 'WebFormsScriptBundleMemberCandidate'
ORDER BY declaration_file, declared_ordinal;
```

Page references can be inspected separately with
`WHERE fact_type = 'WebFormsScriptBundleReferenceCandidate'`. Matching their
`target_symbol` to a declaration's virtual path identifies candidates for review,
not proof that the declaration belongs to the same application or executed.
