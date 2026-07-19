using System.Runtime.InteropServices;
using TraceMap.Core;

namespace TraceMap.Access;

public sealed class AccessComReader
{
    private const int ForceDisableAutomationSecurity = 3;
    private readonly AccessLimits _limits;

    public AccessComReader(AccessLimits? limits = null) => _limits = limits ?? AccessLimits.Default;

    public AccessDatabaseProjection Read(string databaseCopyPath, string databaseHash, string databaseIdentitySeed, string databaseExtension, Action<int> processStarted)
    {
        var accessType = AccessEnvironmentProbe.RequireAccessApplicationType();

        dynamic? application = null;
        dynamic? database = null;
        var job = WindowsJobObject.TryCreateForCurrentProcess();
        try
        {
            application = Activator.CreateInstance(accessType) ?? throw new AccessScanException("AccessComUnavailable");
            application.AutomationSecurity = ForceDisableAutomationSecurity;
            application.Visible = false;
            var accessPid = AccessProcessId(application);
            if (accessPid <= 0) throw new AccessScanException("AccessOwnedProcessIdentityUnavailable");
            _ = job?.TryAssign(accessPid);
            processStarted(accessPid);

            var accessVersion = BoundedString(() => (string)application.Version, 64, "AccessVersionUnavailable");
            application.OpenCurrentDatabase(databaseCopyPath, false);
            database = application.CurrentDb();
            return ReadDatabase((object)application, database, databaseHash, databaseIdentitySeed, databaseExtension, accessVersion, accessPid);
        }
        catch (AccessScanException) { throw; }
        catch { throw new AccessScanException("AccessDatabaseOpenOrCatalogFailed"); }
        finally
        {
            if (database is not null) Release(database);
            if (application is not null)
            {
                try { application.CloseCurrentDatabase(); } catch { }
                try { application.Quit(2); } catch { }
                Release(application);
            }
            GC.KeepAlive(job);
        }
    }

    private AccessDatabaseProjection ReadDatabase(object application, dynamic database, string databaseHash, string databaseIdentitySeed, string extension, string accessVersion, int accessPid)
    {
        object databaseObject = database;
        var gaps = new List<AccessGapProjection>();
        var external = new List<AccessExternalLinkProjection>();
        var tables = ReadTables(databaseObject, databaseIdentitySeed, gaps, external, out var tableLookup, out var fieldLookups, out var systemCount);
        var queryIdentities = ReadQueryIdentities(databaseObject, databaseIdentitySeed, gaps);
        var known = BuildKnownObjects(tables, tableLookup, queryIdentities);
        var relationships = ReadRelationships(databaseObject, databaseIdentitySeed, tableLookup, fieldLookups, gaps, ref systemCount);
        var queries = ReadQueries(databaseObject, databaseIdentitySeed, queryIdentities, known, gaps, external);
        var uiInventory = ReadUiInventoryCounts(application, gaps);
        var vbaInventory = ReadVbaInventoryCounts(application, gaps);

        var gapsTruncated = gaps.Count > _limits.MaxGaps;
        var boundedGaps = gaps
            .OrderBy(item => item.Classification, StringComparer.Ordinal)
            .ThenBy(item => item.StableScopeKey, StringComparer.Ordinal)
            .Take(gapsTruncated ? Math.Max(0, _limits.MaxGaps - 1) : _limits.MaxGaps)
            .ToList();
        if (gapsTruncated)
            boundedGaps.Add(new("AccessGapLimitReached", "database", null));
        return new AccessDatabaseProjection(
            "tracemap.access-projection.v1",
            databaseHash,
            extension,
            accessVersion,
            accessPid,
            RowDataRead: false,
            ExecutionPerformed: false,
            systemCount,
            tables.OrderBy(item => item.Identity.StableKey, StringComparer.Ordinal).ToArray(),
            relationships.OrderBy(item => item.Identity.StableKey, StringComparer.Ordinal).ToArray(),
            queries.OrderBy(item => item.Identity.StableKey, StringComparer.Ordinal).ToArray(),
            external.OrderBy(item => item.Identity.StableKey, StringComparer.Ordinal).ToArray(),
            boundedGaps,
            [
                new("schemaCatalog", "observed"),
                new("savedQueries", "observed"),
                new("formsReports", uiInventory.Coverage),
                new("vbaModules", vbaInventory.Coverage),
                new("macros", "startup-suppressed-not-inventoried"),
                new("externalLinks", "hash-only"),
                new("startupSuppression", "force-disable-requested"),
                new("rowDataRead", "false"),
                new("executionPerformed", "false")
            ],
            UiSurfaces: [],
            VbaModules: [],
            EventBindings: [],
            UiInventory: uiInventory,
            VbaInventory: vbaInventory);
    }

    internal AccessVbaInventoryProjection ReadVbaInventoryCounts(
        object applicationObject,
        List<AccessGapProjection> gaps)
    {
        var loadedBefore = ReadLoadedModuleCount(applicationObject);
        var moduleCount = ReadVbaCatalogModuleCount(applicationObject, gaps);
        var loadedAfter = ReadLoadedModuleCount(applicationObject);
        if (loadedBefore.HasValue && loadedAfter.HasValue && loadedBefore.Value != loadedAfter.Value)
            throw new AccessScanException("AccessVbaModuleStateChanged");

        bool? loadedCountUnchanged = loadedBefore.HasValue && loadedAfter.HasValue ? true : null;
        gaps.Add(new("AccessVbaProjectUnavailable", "vba-project", null, RuleIds.LegacyAccessVba));
        var coverage = moduleCount.HasValue
            ? loadedCountUnchanged == true
                ? "count-observed-source-unavailable"
                : "count-observed-source-unavailable-canary-unavailable"
            : "count-unavailable-source-unavailable";
        return new(moduleCount, loadedCountUnchanged, coverage);
    }

    private int? ReadVbaCatalogModuleCount(
        object applicationObject,
        List<AccessGapProjection> gaps)
    {
        object? projectObject = null;
        object? modulesObject = null;
        try
        {
            dynamic application = applicationObject;
            projectObject = application.CurrentProject;
            dynamic project = projectObject;
            modulesObject = project.AllModules;
            return BoundedCount((dynamic)modulesObject, "AccessVbaModuleCollectionLimitReached");
        }
        catch (AccessScanException ex)
        {
            gaps.Add(new(ex.Classification, "vba-project", null, RuleIds.LegacyAccessVba));
            return null;
        }
        catch { return null; }
        finally
        {
            Release(modulesObject);
            Release(projectObject);
        }
    }

    private int? ReadLoadedModuleCount(object applicationObject)
    {
        object? modulesObject = null;
        try
        {
            dynamic application = applicationObject;
            modulesObject = application.Modules;
            return BoundedCount((dynamic)modulesObject, "AccessVbaLoadedModuleCountUnavailable");
        }
        catch { return null; }
        finally { Release(modulesObject); }
    }

    internal AccessUiInventoryProjection ReadUiInventoryCounts(
        object applicationObject,
        List<AccessGapProjection> gaps)
    {
        dynamic? project = null;
        try
        {
            dynamic application = applicationObject;
            project = application.CurrentProject;
            var formCount = ReadProjectSurfaceCollectionCount((object)project, "form", gaps);
            var reportCount = ReadProjectSurfaceCollectionCount((object)project, "report", gaps);
            var coverage = formCount is null || reportCount is null
                ? "count-partial-identity-unavailable"
                : formCount > 0 || reportCount > 0
                    ? "count-observed-identity-unavailable"
                    : "count-observed-empty";
            return new(formCount, reportCount, coverage);
        }
        catch (AccessScanException ex)
        {
            gaps.Add(new(ex.Classification, "forms-reports", null, RuleIds.LegacyAccessUiSurface));
            return new(null, null, "count-unavailable");
        }
        catch
        {
            gaps.Add(new("AccessFormReportCoverageUnavailable", "forms-reports", null, RuleIds.LegacyAccessUiSurface));
            return new(null, null, "count-unavailable");
        }
        finally { Release(project); }
    }

    private int? ReadProjectSurfaceCollectionCount(
        object projectObject,
        string surfaceKind,
        List<AccessGapProjection> gaps)
    {
        try
        {
            dynamic project = projectObject;
            object collection = surfaceKind == "form"
                ? (object)project.AllForms
                : (object)project.AllReports;
            return ReadSurfaceCollectionCount(collection, surfaceKind, gaps);
        }
        catch (AccessScanException ex)
        {
            gaps.Add(new(ex.Classification, $"{surfaceKind}-catalog", null, RuleIds.LegacyAccessUiSurface));
            return null;
        }
        catch
        {
            gaps.Add(new("AccessFormReportCoverageUnavailable", $"{surfaceKind}-catalog", null, RuleIds.LegacyAccessUiSurface));
            return null;
        }
    }

    internal int? ReadSurfaceCollectionCount(
        object collectionObject,
        string surfaceKind,
        List<AccessGapProjection> gaps)
    {
        dynamic collection = collectionObject;
        try
        {
            var count = BoundedCount(collection, surfaceKind == "form" ? "AccessFormCollectionLimit" : "AccessReportCollectionLimit");
            if (count > 0)
                gaps.Add(new("AccessFormReportCoverageUnavailable", $"{surfaceKind}-catalog", null, RuleIds.LegacyAccessUiSurface));
            return count;
        }
        catch (AccessScanException ex) { gaps.Add(new(ex.Classification, $"{surfaceKind}-catalog", null, RuleIds.LegacyAccessUiSurface)); return null; }
        catch { gaps.Add(new("AccessFormReportCoverageUnavailable", $"{surfaceKind}-catalog", null, RuleIds.LegacyAccessUiSurface)); return null; }
        finally { Release(collectionObject); }
    }

    private IReadOnlyList<AccessTableProjection> ReadTables(
        dynamic database,
        string databaseIdentitySeed,
        List<AccessGapProjection> gaps,
        List<AccessExternalLinkProjection> external,
        out Dictionary<string, List<AccessTableProjection>> lookup,
        out Dictionary<string, Dictionary<string, List<AccessFieldProjection>>> fieldLookups,
        out int systemCount)
    {
        var result = new List<AccessTableProjection>();
        lookup = new Dictionary<string, List<AccessTableProjection>>(StringComparer.OrdinalIgnoreCase);
        fieldLookups = new Dictionary<string, Dictionary<string, List<AccessFieldProjection>>>(StringComparer.Ordinal);
        systemCount = 0;
        dynamic? collection = null;
        try
        {
            collection = database.TableDefs;
            var count = BoundedCount(collection, "AccessTableCollectionLimit");
            for (var index = 0; index < count; index++)
            {
                dynamic? table = null;
                try
                {
                    table = collection[index];
                    var name = BoundedString(() => (string)table.Name, 512, "AccessTableNameUnavailable");
                    var attributes = SafeInt(() => (int)table.Attributes);
                    if (name.StartsWith("MSys", StringComparison.OrdinalIgnoreCase) || (attributes & unchecked((int)0x80000000)) != 0)
                    {
                        systemCount++;
                        continue;
                    }

                    var identity = AccessSafeValues.Identity(databaseIdentitySeed, "table", name);
                    var connect = BoundedOptionalString(() => (string)table.Connect, _limits.MaxStringLength, "AccessExternalSourceMetadataUnavailable");
                    if (!string.IsNullOrWhiteSpace(connect))
                    {
                        external.Add(new AccessExternalLinkProjection(identity, AccessSafeValues.ProviderFamily(connect), AccessSafeValues.RoleHash("access-linked-source", connect), "linked-table"));
                        continue;
                    }

                    var rawFieldLookup = new Dictionary<string, List<AccessFieldProjection>>(StringComparer.OrdinalIgnoreCase);
                    var fields = ReadFields(table, databaseIdentitySeed, identity, rawFieldLookup, gaps);
                    var indexes = ReadIndexes(table, databaseIdentitySeed, identity, rawFieldLookup, gaps);
                    var projection = new AccessTableProjection(identity, fields, indexes);
                    result.Add(projection);
                    fieldLookups[identity.StableKey] = rawFieldLookup;
                    if (!lookup.TryGetValue(name, out var candidates)) lookup[name] = candidates = [];
                    candidates.Add(projection);
                }
                catch (AccessScanException ex) { gaps.Add(new(ex.Classification, "table", null)); }
                catch { gaps.Add(new("AccessObjectMetadataUnavailable", "table", null)); }
                finally { Release(table); }
            }
        }
        catch (AccessScanException ex) { gaps.Add(new(ex.Classification, "database-tables", null)); }
        catch { gaps.Add(new("AccessTableCatalogUnavailable", "database-tables", null)); }
        finally { Release(collection); }
        return result;
    }

    private IReadOnlyList<AccessFieldProjection> ReadFields(
        dynamic table,
        string databaseIdentitySeed,
        AccessSafeIdentity tableIdentity,
        Dictionary<string, List<AccessFieldProjection>> rawFieldLookup,
        List<AccessGapProjection> gaps)
    {
        var result = new List<AccessFieldProjection>();
        dynamic? fields = null;
        try
        {
            fields = table.Fields;
            var count = BoundedChildCount(fields, "AccessFieldCollectionLimit");
            for (var index = 0; index < count; index++)
            {
                dynamic? field = null;
                try
                {
                    field = fields[index];
                    var name = BoundedString(() => (string)field.Name, 512, "AccessFieldNameUnavailable");
                    var projection = new AccessFieldProjection(
                        AccessSafeValues.Identity(databaseIdentitySeed, $"field-{tableIdentity.StableKey}", name),
                        index,
                        AccessSafeValues.DaoTypeFamily(SafeInt(() => (int)field.Type)),
                        SafeInt(() => (int)field.Size),
                        SafeBool(() => (bool)field.Required));
                    result.Add(projection);
                    if (!rawFieldLookup.TryGetValue(name, out var candidates)) rawFieldLookup[name] = candidates = [];
                    candidates.Add(projection);
                }
                catch (AccessScanException ex) { gaps.Add(new(ex.Classification, "field", tableIdentity.StableKey)); }
                catch { gaps.Add(new("AccessObjectMetadataUnavailable", "field", tableIdentity.StableKey)); }
                finally { Release(field); }
            }
        }
        finally { Release(fields); }
        return result.OrderBy(item => item.Ordinal).ToArray();
    }

    private IReadOnlyList<AccessIndexProjection> ReadIndexes(
        dynamic table,
        string databaseIdentitySeed,
        AccessSafeIdentity tableIdentity,
        IReadOnlyDictionary<string, List<AccessFieldProjection>> rawFieldLookup,
        List<AccessGapProjection> gaps)
    {
        var result = new List<AccessIndexProjection>();
        dynamic? indexes = null;
        try
        {
            indexes = table.Indexes;
            var count = BoundedChildCount(indexes, "AccessIndexCollectionLimit");
            for (var index = 0; index < count; index++)
            {
                dynamic? item = null;
                dynamic? indexFields = null;
                try
                {
                    item = indexes[index];
                    var name = BoundedString(() => (string)item.Name, 512, "AccessIndexNameUnavailable");
                    indexFields = item.Fields;
                    var fieldKeys = new List<string>();
                    var fieldCount = BoundedChildCount(indexFields, "AccessIndexFieldCollectionLimit");
                    for (var ordinal = 0; ordinal < fieldCount; ordinal++)
                    {
                        dynamic? indexField = null;
                        try
                        {
                            indexField = indexFields[ordinal];
                            var fieldName = BoundedString(() => (string)indexField.Name, 512, "AccessIndexFieldNameUnavailable");
                            if (!UniqueField(rawFieldLookup, fieldName, out var match)) throw new AccessScanException("AccessSchemaAmbiguous");
                            fieldKeys.Add(match.Identity.StableKey);
                        }
                        finally { Release(indexField); }
                    }
                    result.Add(new(AccessSafeValues.Identity(databaseIdentitySeed, $"index-{tableIdentity.StableKey}", name), SafeBool(() => (bool)item.Primary), SafeBool(() => (bool)item.Unique), fieldKeys));
                }
                catch (AccessScanException ex) { gaps.Add(new(ex.Classification, "index", tableIdentity.StableKey)); }
                catch { gaps.Add(new("AccessObjectMetadataUnavailable", "index", tableIdentity.StableKey)); }
                finally { Release(indexFields); Release(item); }
            }
        }
        finally { Release(indexes); }
        return result.OrderBy(item => item.Identity.StableKey, StringComparer.Ordinal).ToArray();
    }

    private IReadOnlyList<AccessRelationshipProjection> ReadRelationships(
        dynamic database,
        string databaseIdentitySeed,
        Dictionary<string, List<AccessTableProjection>> tables,
        IReadOnlyDictionary<string, Dictionary<string, List<AccessFieldProjection>>> fieldLookups,
        List<AccessGapProjection> gaps,
        ref int systemCount)
    {
        var result = new List<AccessRelationshipProjection>();
        dynamic? relations = null;
        try
        {
            relations = database.Relations;
            var count = BoundedCount(relations, "AccessRelationshipCollectionLimit");
            for (var index = 0; index < count; index++)
            {
                dynamic? relation = null;
                dynamic? relationFields = null;
                try
                {
                    relation = relations[index];
                    var name = BoundedString(() => (string)relation.Name, 512, "AccessRelationshipNameUnavailable");
                    var sourceName = BoundedString(() => (string)relation.Table, 512, "AccessRelationshipSourceUnavailable");
                    var targetName = BoundedString(() => (string)relation.ForeignTable, 512, "AccessRelationshipTargetUnavailable");
                    if (sourceName.StartsWith("MSys", StringComparison.OrdinalIgnoreCase)
                        || targetName.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
                    {
                        systemCount++;
                        continue;
                    }
                    if (!UniqueTable(tables, sourceName, out var source) || !UniqueTable(tables, targetName, out var target))
                    {
                        gaps.Add(new("AccessSchemaAmbiguous", "relationship", AccessSafeValues.Identity(databaseIdentitySeed, "relationship", name).StableKey));
                        continue;
                    }
                    relationFields = relation.Fields;
                    var fieldCount = BoundedChildCount(relationFields, "AccessRelationshipFieldCollectionLimit");
                    var fields = new List<AccessRelationshipFieldProjection>();
                    for (var ordinal = 0; ordinal < fieldCount; ordinal++)
                    {
                        dynamic? relationField = null;
                        try
                        {
                            relationField = relationFields[ordinal];
                            var sourceField = BoundedString(() => (string)relationField.Name, 512, "AccessRelationshipFieldUnavailable");
                            var targetField = BoundedString(() => (string)relationField.ForeignName, 512, "AccessRelationshipFieldUnavailable");
                            if (!fieldLookups.TryGetValue(source.Identity.StableKey, out var sourceLookup)
                                || !fieldLookups.TryGetValue(target.Identity.StableKey, out var targetLookup)
                                || !UniqueField(sourceLookup, sourceField, out var sourceProjection)
                                || !UniqueField(targetLookup, targetField, out var targetProjection))
                                throw new AccessScanException("AccessSchemaAmbiguous");
                            fields.Add(new(sourceProjection.Identity.StableKey, targetProjection.Identity.StableKey, ordinal));
                        }
                        finally { Release(relationField); }
                    }
                    result.Add(new(AccessSafeValues.Identity(databaseIdentitySeed, "relationship", name), source.Identity.StableKey, target.Identity.StableKey, SafeInt(() => (int)relation.Attributes), fields));
                }
                catch (AccessScanException ex) { gaps.Add(new(ex.Classification, "relationship", null)); }
                catch { gaps.Add(new("AccessObjectMetadataUnavailable", "relationship", null)); }
                finally { Release(relationFields); Release(relation); }
            }
        }
        catch (AccessScanException ex) { gaps.Add(new(ex.Classification, "database-relationships", null)); }
        catch { gaps.Add(new("AccessRelationshipCatalogUnavailable", "database-relationships", null)); }
        finally { Release(relations); }
        return result;
    }

    private Dictionary<string, AccessSafeIdentity> ReadQueryIdentities(dynamic database, string databaseIdentitySeed, List<AccessGapProjection> gaps)
    {
        var result = new Dictionary<string, AccessSafeIdentity>(StringComparer.OrdinalIgnoreCase);
        dynamic? queries = null;
        try
        {
            queries = database.QueryDefs;
            var count = BoundedCount(queries, "AccessQueryCollectionLimit");
            for (var index = 0; index < count; index++)
            {
                dynamic? query = null;
                try
                {
                    query = queries[index];
                    var name = BoundedString(() => (string)query.Name, 512, "AccessQueryNameUnavailable");
                    if (name.StartsWith('~')) continue;
                    if (!result.TryAdd(name, AccessSafeValues.Identity(databaseIdentitySeed, "query", name)))
                        gaps.Add(new("AccessSchemaAmbiguous", "query", AccessSafeValues.RoleHash("access-query-name", name)));
                }
                finally { Release(query); }
            }
        }
        catch (AccessScanException ex) { gaps.Add(new(ex.Classification, "database-queries", null)); }
        catch { gaps.Add(new("AccessQueryCatalogUnavailable", "database-queries", null)); }
        finally { Release(queries); }
        return result;
    }

    private IReadOnlyList<AccessQueryProjection> ReadQueries(
        dynamic database,
        string databaseIdentitySeed,
        IReadOnlyDictionary<string, AccessSafeIdentity> identities,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> known,
        List<AccessGapProjection> gaps,
        List<AccessExternalLinkProjection> external)
    {
        var result = new List<AccessQueryProjection>();
        dynamic? queries = null;
        try
        {
            queries = database.QueryDefs;
            var count = BoundedCount(queries, "AccessQueryCollectionLimit");
            for (var index = 0; index < count; index++)
            {
                dynamic? query = null;
                dynamic? parameters = null;
                try
                {
                    query = queries[index];
                    var name = BoundedString(() => (string)query.Name, 512, "AccessQueryNameUnavailable");
                    if (!identities.TryGetValue(name, out var identity)) continue;
                    var type = SafeInt(() => (int)query.Type);
                    var kind = AccessSafeValues.QueryKind(type);
                    var sql = BoundedString(() => (string)query.SQL, _limits.MaxQueryTextLength, "AccessQueryTextLimitReached");
                    var sqlHash = AccessSafeValues.RoleHash("access-query-sql", sql);
                    var dependencyProjection = AccessQueryProjector.ProjectDependencies(sql, known);
                    var referenceCoverage = type switch
                    {
                        0 => dependencyProjection.Coverage,
                        112 => "unknown",
                        _ => "partial"
                    };
                    if (dependencyProjection.UnsupportedShape || type != 0)
                        gaps.Add(new(type == 112 ? "AccessQueryDependencyUnknown" : "AccessQueryDependencyPartial", "query", identity.StableKey));

                    parameters = query.Parameters;
                    var parameterCount = BoundedChildCount(parameters, "AccessQueryParameterCollectionLimit");
                    var parameterRows = new List<AccessQueryParameterProjection>();
                    for (var ordinal = 0; ordinal < parameterCount; ordinal++)
                    {
                        dynamic? parameter = null;
                        try
                        {
                            parameter = parameters[ordinal];
                            var parameterName = BoundedString(() => (string)parameter.Name, 512, "AccessQueryParameterNameUnavailable");
                            parameterRows.Add(new(AccessSafeValues.Identity(databaseIdentitySeed, $"parameter-{identity.StableKey}", parameterName), ordinal, AccessSafeValues.DaoTypeFamily(SafeInt(() => (int)parameter.Type))));
                        }
                        finally { Release(parameter); }
                    }

                    var isPassThrough = type == 112;
                    string? connectHash = null;
                    string? provider = null;
                    if (isPassThrough)
                    {
                        var connect = BoundedOptionalString(() => (string)query.Connect, _limits.MaxStringLength, "AccessExternalSourceMetadataUnavailable");
                        if (!string.IsNullOrWhiteSpace(connect))
                        {
                            connectHash = AccessSafeValues.RoleHash("access-pass-through-connection", connect);
                            provider = AccessSafeValues.ProviderFamily(connect);
                            external.Add(new(identity, provider, connectHash, "pass-through-query"));
                        }
                    }

                    result.Add(new(identity, kind, sqlHash, sql.Length, referenceCoverage, parameterRows,
                        isPassThrough ? [] : dependencyProjection.Dependencies, isPassThrough, connectHash, provider));
                }
                catch (AccessScanException ex) { gaps.Add(new(ex.Classification, "query", null)); }
                catch { gaps.Add(new("AccessObjectMetadataUnavailable", "query", null)); }
                finally { Release(parameters); Release(query); }
            }
        }
        catch (AccessScanException ex) { gaps.Add(new(ex.Classification, "database-queries", null)); }
        catch { gaps.Add(new("AccessQueryCatalogUnavailable", "database-queries", null)); }
        finally { Release(queries); }
        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> BuildKnownObjects(
        IReadOnlyList<AccessTableProjection> tables,
        IReadOnlyDictionary<string, List<AccessTableProjection>> tableLookup,
        IReadOnlyDictionary<string, AccessSafeIdentity> queries)
    {
        var result = new Dictionary<string, IReadOnlyList<(string, string)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in tableLookup) result[pair.Key] = pair.Value.Select(table => (table.Identity.StableKey, "table")).ToArray();
        foreach (var pair in queries)
        {
            if (result.TryGetValue(pair.Key, out var existing)) result[pair.Key] = [.. existing, (pair.Value.StableKey, "query")];
            else result[pair.Key] = [(pair.Value.StableKey, "query")];
        }
        return result;
    }

    private int BoundedCount(dynamic collection, string classification)
    {
        int count;
        try { count = (int)collection.Count; }
        catch { throw new AccessScanException(classification); }
        if (count < 0 || count > _limits.MaxObjectsPerCollection) throw new AccessScanException(classification);
        return count;
    }

    private int BoundedChildCount(dynamic collection, string classification)
    {
        int count;
        try { count = (int)collection.Count; }
        catch { throw new AccessScanException(classification); }
        if (count < 0 || count > _limits.MaxChildrenPerObject) throw new AccessScanException(classification);
        return count;
    }

    private static bool UniqueTable(IReadOnlyDictionary<string, List<AccessTableProjection>> lookup, string name, out AccessTableProjection table)
    {
        table = null!;
        if (!lookup.TryGetValue(name, out var candidates) || candidates.Count != 1) return false;
        table = candidates[0];
        return true;
    }

    internal static bool UniqueField(IReadOnlyDictionary<string, List<AccessFieldProjection>> lookup, string name, out AccessFieldProjection field)
    {
        field = null!;
        if (!lookup.TryGetValue(name, out var candidates) || candidates.Count != 1) return false;
        field = candidates[0];
        return true;
    }

    private static int AccessProcessId(dynamic application)
    {
        try
        {
            var hwnd = new IntPtr(Convert.ToInt64(application.hWndAccessApp()));
            _ = GetWindowThreadProcessId(hwnd, out var pid);
            return checked((int)pid);
        }
        catch { return 0; }
    }

    internal static string BoundedString(Func<string> read, int limit, string classification)
    {
        try
        {
            var value = read() ?? string.Empty;
            if (value.Length > limit) throw new AccessScanException(classification);
            return value;
        }
        catch (AccessScanException) { throw; }
        catch { throw new AccessScanException(classification); }
    }

    private static string? BoundedOptionalString(Func<string> read, int limit, string classification)
    {
        try
        {
            var value = read();
            if (value is not null && value.Length > limit) throw new AccessScanException(classification);
            return value;
        }
        catch (AccessScanException) { throw; }
        catch { throw new AccessScanException(classification); }
    }

    private static int SafeInt(Func<int> read) { try { return read(); } catch { return 0; } }
    private static bool SafeBool(Func<bool> read) { try { return read(); } catch { return false; } }

    private static void Release(object? value)
    {
        if (value is null || !OperatingSystem.IsWindows()) return;
        try { if (Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); } catch { }
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
