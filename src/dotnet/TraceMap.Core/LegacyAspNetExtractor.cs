using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace TraceMap.Core;

public static partial class LegacyAspNetExtractor
{
    private const string ExtractorId = "LegacyAspNetExtractor";

    private static readonly HashSet<string> CSharpKinds = new(StringComparer.Ordinal)
    {
        "CSharp",
        "WebFormsCodeBehind"
    };

    private static readonly HashSet<string> NavigationAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "NavigateUrl",
        "PostBackUrl",
        "Action"
    };

    private static readonly HashSet<string> PageMethodAttributes = new(StringComparer.Ordinal)
    {
        "WebMethod",
        "ScriptMethod",
        "ScriptService"
    };

    private static readonly HashSet<string> IdentityControlTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ChangePassword", "CreateUserWizard", "Login", "LoginName", "LoginStatus", "LoginView", "PasswordRecovery"
    };

    private static readonly HashSet<string> KnownIdentityPipelineTypes = new(StringComparer.Ordinal)
    {
        "System.Web.Profile.ProfileModule",
        "System.Web.Security.AnonymousIdentificationModule",
        "System.Web.Security.DefaultAuthenticationModule",
        "System.Web.Security.FileAuthorizationModule",
        "System.Web.Security.FormsAuthenticationModule",
        "System.Web.Security.RoleManagerModule",
        "System.Web.Security.UrlAuthorizationModule",
        "System.Web.Security.WindowsAuthenticationModule",
        "System.Web.SessionState.SessionStateModule"
    };

    public static IReadOnlyList<CodeFact> Extract(
        string repoPath,
        ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory,
        IReadOnlyList<CodeFact> existingFacts)
    {
        var facts = new List<CodeFact>();
        var pageFacts = existingFacts
            .Where(fact => fact.FactType == FactTypes.WebFormsPageDeclared)
            .GroupBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(fact => fact.FactId, StringComparer.Ordinal).First(), StringComparer.Ordinal);
        var semanticTypesByFile = BuildSemanticLookup(existingFacts, FactTypes.TypeDeclared);
        var semanticMethodsByFile = BuildSemanticLookup(existingFacts, FactTypes.MethodDeclared);
        var asmxOperationsByFile = BuildFactLookup(existingFacts, FactTypes.AsmxOperationDeclared);
        var hasAspNetCandidateInventory = HasAspNetCandidateInventory(inventory);

        AddDesignerOrphanGaps(manifest, inventory, pageFacts, facts);

        foreach (var item in inventory.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            switch (item.Kind)
            {
                case "AspNetApplication":
                    ExtractApplicationFile(repoPath, manifest, item, facts);
                    break;
                case "AspNetHandler":
                    ExtractHandlerFile(repoPath, manifest, item, facts);
                    break;
                case "Config":
                    ExtractConfigFile(repoPath, manifest, item, facts);
                    break;
                case "AspNetSiteMap":
                    ExtractSiteMap(repoPath, manifest, item, facts);
                    break;
                case "WebFormsMarkup":
                    ExtractMarkupNavigation(repoPath, manifest, item, pageFacts.GetValueOrDefault(item.RelativePath), facts);
                    break;
            }
        }

        foreach (var item in inventory.Where(item => CSharpKinds.Contains(item.Kind)).OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            ExtractCSharpFile(repoPath, manifest, item, semanticTypesByFile, semanticMethodsByFile, asmxOperationsByFile, existingFacts, facts, hasAspNetCandidateInventory);
        }

        AddNavigationEdges(manifest, pageFacts, facts);
        AddIdentityControlFacts(manifest, existingFacts, facts);
        if (hasAspNetCandidateInventory)
        {
            AddIdentityTypeFacts(manifest, existingFacts, facts);
        }

        if (manifest.BuildStatus != "Succeeded" && hasAspNetCandidateInventory)
        {
            facts.Add(CreateGap(
                manifest,
                ".",
                1,
                RuleIds.LegacyAspNetSurface,
                "ReducedSemanticCoverage",
                "ASP.NET route/navigation extraction used static syntax, markup, and config fallback because semantic coverage is reduced.",
                null));
        }

        return facts
            .GroupBy(fact => fact.FactId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(fact => fact.FactType, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.TargetSymbol, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasAspNetCandidateInventory(IReadOnlyList<FileInventoryItem> inventory)
    {
        return inventory.Any(item => item.Kind is "WebFormsMarkup" or "WebFormsCodeBehind" or "WebFormsDesigner" or "AspNetApplication" or "AspNetHandler" or "AspNetSiteMap");
    }

    private static IReadOnlyDictionary<string, CodeFact[]> BuildSemanticLookup(IReadOnlyList<CodeFact> existingFacts, string factType)
    {
        return existingFacts
            .Where(fact => fact.FactType == factType && fact.EvidenceTier == EvidenceTiers.Tier1Semantic)
            .GroupBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, CodeFact[]> BuildFactLookup(IReadOnlyList<CodeFact> existingFacts, string factType)
    {
        return existingFacts
            .Where(fact => fact.FactType == factType)
            .GroupBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
    }

    private static void AddDesignerOrphanGaps(
        ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory,
        IReadOnlyDictionary<string, CodeFact> pageFacts,
        List<CodeFact> facts)
    {
        foreach (var designer in inventory.Where(item => item.Kind == "WebFormsDesigner").OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            var expectedMarkup = designer.RelativePath[..^".designer.cs".Length];
            if (!pageFacts.ContainsKey(expectedMarkup))
            {
                facts.Add(CreateGap(
                    manifest,
                    designer.RelativePath,
                    1,
                    RuleIds.LegacyAspNetSurface,
                    "DesignerWithoutMarkupSurface",
                    "Designer file is supporting identity evidence only and no matching checked-in page/control/master surface was found.",
                    null));
            }
        }
    }

    private static void ExtractApplicationFile(string repoPath, ScanManifest manifest, FileInventoryItem item, List<CodeFact> facts)
    {
        if (!TryRead(repoPath, item.RelativePath, out var text))
        {
            facts.Add(CreateGap(manifest, item.RelativePath, 1, RuleIds.LegacyAspNetSurface, "UnreadableAspNetApplicationFile", "Unable to read ASP.NET application file.", null));
            return;
        }

        var source = SourceText.From(text);
        var match = ApplicationDirectiveRegex().Match(text);
        var attrs = match.Success ? ParseAttributes(match.Groups["attrs"].Value) : new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var line = match.Success ? LineAt(source, match.Index) : 1;
        var properties = BaseProperties(manifest, RuleIds.LegacyAspNetSurface, "ASP.NET application surface inventory is static file evidence and does not prove runtime application startup.");
        properties["surfaceKind"] = "application";
        AddSafeCodeName(properties, "inheritsTypeName", attrs.GetValueOrDefault("Inherits"), "surface", "inherits-type");
        AddSafeMarkupPath(properties, "codeBehindFile", attrs.GetValueOrDefault("CodeBehind"), "surface", "codebehind-path");
        AddSafeMarkupPath(properties, "codeFile", attrs.GetValueOrDefault("CodeFile"), "surface", "codefile-path");

        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.AspNetSurfaceDeclared,
            RuleIds.LegacyAspNetSurface,
            EvidenceTiers.Tier2Structural,
            Evidence(item.RelativePath, line, line),
            targetSymbol: properties.GetValueOrDefault("inheritsTypeName"),
            contractElement: Path.GetFileName(item.RelativePath),
            properties: properties));

        if (!match.Success)
        {
            facts.Add(CreateGap(manifest, item.RelativePath, 1, RuleIds.LegacyAspNetSurface, "MalformedAspNetApplicationDirective", "Unable to parse ASP.NET Application directive.", null));
        }
    }

    private static void ExtractHandlerFile(string repoPath, ScanManifest manifest, FileInventoryItem item, List<CodeFact> facts)
    {
        if (!TryRead(repoPath, item.RelativePath, out var text))
        {
            facts.Add(CreateGap(manifest, item.RelativePath, 1, RuleIds.LegacyAspNetHandler, "UnreadableAspNetHandlerFile", "Unable to read ASP.NET handler file.", null));
            return;
        }

        var source = SourceText.From(text);
        var match = HandlerDirectiveRegex().Match(text);
        var attrs = match.Success ? ParseAttributes(match.Groups["attrs"].Value) : new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var line = match.Success ? LineAt(source, match.Index) : 1;

        var surfaceProperties = BaseProperties(manifest, RuleIds.LegacyAspNetSurface, "ASP.NET handler surface inventory is static file evidence and does not prove runtime request handling.");
        surfaceProperties["surfaceKind"] = "handler-file";
        AddSafeCodeName(surfaceProperties, "handlerTypeName", attrs.GetValueOrDefault("Class"), "handler", "handler-type");
        AddSafeIdentifier(surfaceProperties, "language", attrs.GetValueOrDefault("Language"), "surface", "language");
        AddSafeMarkupPath(surfaceProperties, "codeBehindFile", attrs.GetValueOrDefault("CodeBehind"), "surface", "codebehind-path");
        AddSafeMarkupPath(surfaceProperties, "codeFile", attrs.GetValueOrDefault("CodeFile"), "surface", "codefile-path");
        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.AspNetSurfaceDeclared,
            RuleIds.LegacyAspNetSurface,
            EvidenceTiers.Tier2Structural,
            Evidence(item.RelativePath, line, line),
            targetSymbol: surfaceProperties.GetValueOrDefault("handlerTypeName"),
            contractElement: Path.GetFileName(item.RelativePath),
            properties: surfaceProperties));

        if (!match.Success)
        {
            facts.Add(CreateGap(manifest, item.RelativePath, 1, RuleIds.LegacyAspNetHandler, "MalformedAspNetHandlerDirective", "Unable to parse ASP.NET WebHandler directive.", null));
            return;
        }

        var handlerProperties = BaseProperties(manifest, RuleIds.LegacyAspNetHandler, "Handler evidence is static declaration only and does not prove request execution, deployment, pipeline order, auth, or factory result.");
        handlerProperties["handlerKind"] = "ashx-directive";
        handlerProperties["surfaceFile"] = item.RelativePath;
        AddSafeCodeName(handlerProperties, "handlerTypeName", attrs.GetValueOrDefault("Class"), "handler", "handler-type");
        AddSafeIdentifier(handlerProperties, "language", attrs.GetValueOrDefault("Language"), "handler", "language");
        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.AspNetHandlerDeclared,
            RuleIds.LegacyAspNetHandler,
            EvidenceTiers.Tier2Structural,
            Evidence(item.RelativePath, line, line),
            sourceSymbol: item.RelativePath,
            targetSymbol: handlerProperties.GetValueOrDefault("handlerTypeName"),
            contractElement: Path.GetFileName(item.RelativePath),
            properties: handlerProperties));

    }

    private static void ExtractConfigFile(string repoPath, ScanManifest manifest, FileInventoryItem item, List<CodeFact> facts)
    {
        XDocument document;
        try
        {
            document = SafeXml.LoadDocument(Path.Combine(repoPath, item.RelativePath));
        }
        catch (SafeXmlException ex)
        {
            facts.Add(CreateGap(manifest, item.RelativePath, 1, RuleIds.LegacyAspNetConfig, $"ConfigXml{ex.FailureKind}", "Unable to parse checked-in ASP.NET config with the safe XML parser.", null));
            return;
        }
        catch (Exception ex) when (IsXmlIoException(ex))
        {
            facts.Add(CreateGap(manifest, item.RelativePath, 1, RuleIds.LegacyAspNetConfig, "ConfigXmlIoFailure", "Unable to read checked-in ASP.NET config for safe XML parsing.", null));
            return;
        }

        var elements = document.Descendants().Where(element => IsConfigCandidate(element)).OrderBy(LineNumber).ThenBy(element => element.Name.LocalName, StringComparer.Ordinal).ToArray();
        foreach (var element in elements)
        {
            var properties = BaseProperties(manifest, RuleIds.LegacyAspNetConfig, "Checked-in ASP.NET config evidence is static only and does not prove runtime transforms, machine.config inheritance, pipeline behavior, deployment, reachability, or auth.");
            properties["sectionKind"] = SectionKind(element);
            AddLocationScope(properties, element);
            AddConfigElementProperties(properties, element);
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.AspNetConfigSurfaceDeclared,
                RuleIds.LegacyAspNetConfig,
                EvidenceTiers.Tier2Structural,
                Evidence(item.RelativePath, LineNumber(element), LineNumber(element)),
                targetSymbol: properties.GetValueOrDefault("typeName"),
                contractElement: properties.GetValueOrDefault("sectionKind"),
                properties: properties));
        }

        foreach (var element in document.Descendants().Where(HasUnsupportedConfigSource).OrderBy(LineNumber))
        {
            facts.Add(CreateGap(manifest, item.RelativePath, LineNumber(element), RuleIds.LegacyAspNetConfig, "ExternalConfigSourceUnsupported", "Config section uses external configSource/file evidence that is not loaded or executed.", null));
        }

        foreach (var element in document.Descendants().Where(element => element.Name.LocalName.Equals("EncryptedData", StringComparison.OrdinalIgnoreCase)).OrderBy(LineNumber))
        {
            facts.Add(CreateGap(manifest, item.RelativePath, LineNumber(element), RuleIds.LegacyAspNetConfig, "EncryptedConfigUnsupported", "Encrypted config sections cannot be interpreted by static extraction.", null));
        }

        ExtractIdentityStateConfig(manifest, item, document, facts);
    }

    private static void ExtractIdentityStateConfig(ScanManifest manifest, FileInventoryItem item, XDocument document, List<CodeFact> facts)
    {
        foreach (var (element, declarationOrdinal) in document.Descendants()
                     .Select((element, index) => (element, declarationOrdinal: index + 1))
                     .OrderBy(item => LineNumber(item.element))
                     .ThenBy(item => item.declarationOrdinal))
        {
            var identityKind = IdentityConfigKind(element);
            if (identityKind is null)
            {
                continue;
            }

            var properties = BaseProperties(manifest, RuleIds.LegacyAspNetIdentityState,
                "Identity/state rows are checked-in static declarations only and do not prove effective runtime policy, authenticated identity, authorization outcome, session behavior, cookie behavior, provider availability, or security quality.");
            properties["identityKind"] = identityKind;
            properties["declarationStatus"] = "declared-in-checked-in-config";
            properties["declarationOrdinal"] = declarationOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            AddIdentityConfigProperties(properties, element, identityKind);
            var fact = FactFactory.Create(
                manifest,
                FactTypes.AspNetIdentityStateDeclared,
                RuleIds.LegacyAspNetIdentityState,
                EvidenceTiers.Tier2Structural,
                Evidence(item.RelativePath, LineNumber(element), LineNumber(element)),
                targetSymbol: properties.GetValueOrDefault("typeName"),
                contractElement: identityKind,
                properties: properties);
            facts.Add(fact);

            if (identityKind is "membership-provider" or "role-provider"
                && properties.GetValueOrDefault("providerClassification") == "custom-or-unresolved")
            {
                facts.Add(CreateGap(
                    manifest,
                    item.RelativePath,
                    LineNumber(element),
                    RuleIds.LegacyAspNetIdentityState,
                    "UnsupportedCustomIdentityProvider",
                    "A custom or unresolved identity provider is declared; TraceMap records its static presence but does not inspect or grade provider behavior.",
                    null,
                    fact.FactId));
            }
        }

        foreach (var element in document.Descendants().Where(HasUnsupportedConfigSource).Where(IsIdentityConfigScope).OrderBy(LineNumber))
        {
            facts.Add(CreateGap(
                manifest,
                item.RelativePath,
                LineNumber(element),
                RuleIds.LegacyAspNetIdentityState,
                "ExternalIdentityConfigUnsupported",
                "An identity/state section uses external configSource/file evidence that is not loaded or interpreted.",
                null));
        }

        foreach (var element in document.Descendants()
                     .Where(element => element.Name.LocalName.Equals("EncryptedData", StringComparison.OrdinalIgnoreCase))
                     .Where(element => element.Ancestors().Any(IsIdentityConfigScope))
                     .OrderBy(LineNumber))
        {
            facts.Add(CreateGap(
                manifest,
                item.RelativePath,
                LineNumber(element),
                RuleIds.LegacyAspNetIdentityState,
                "EncryptedIdentityConfigUnsupported",
                "An encrypted identity/state section cannot be interpreted by static extraction.",
                null));
        }
    }

    private static string? IdentityConfigKind(XElement element)
    {
        var name = element.Name.LocalName;
        if (ParentNameEquals(element, "system.web"))
        {
            if (name.Equals("authentication", StringComparison.OrdinalIgnoreCase)) return "authentication";
            if (name.Equals("authorization", StringComparison.OrdinalIgnoreCase)) return "authorization-section";
            if (name.Equals("identity", StringComparison.OrdinalIgnoreCase)) return "impersonation";
            if (name.Equals("machineKey", StringComparison.OrdinalIgnoreCase)) return "machine-key-presence";
            if (name.Equals("membership", StringComparison.OrdinalIgnoreCase)) return "membership";
            if (name.Equals("roleManager", StringComparison.OrdinalIgnoreCase)) return "role-manager";
            if (name.Equals("sessionState", StringComparison.OrdinalIgnoreCase)) return "session-state";
            if (name.Equals("httpCookies", StringComparison.OrdinalIgnoreCase)) return "cookie-policy";
            if (name.Equals("anonymousIdentification", StringComparison.OrdinalIgnoreCase)) return "anonymous-identification";
            if (name.Equals("profile", StringComparison.OrdinalIgnoreCase)) return "profile";
        }

        if (name.Equals("forms", StringComparison.OrdinalIgnoreCase) && HasAncestorNamed(element, "authentication"))
            return "forms-authentication";
        if ((name.Equals("allow", StringComparison.OrdinalIgnoreCase) || name.Equals("deny", StringComparison.OrdinalIgnoreCase))
            && HasAncestorNamed(element, "authorization"))
            return "authorization-rule";
        if (name.Equals("add", StringComparison.OrdinalIgnoreCase) && HasAncestorNamed(element, "membership"))
            return "membership-provider";
        if (name.Equals("add", StringComparison.OrdinalIgnoreCase) && HasAncestorNamed(element, "roleManager"))
            return "role-provider";
        if (name.Equals("add", StringComparison.OrdinalIgnoreCase) && element.Parent?.Name.LocalName is "httpModules" or "modules")
        {
            var typeName = TypeName(element);
            if (typeName is not null && (KnownIdentityPipelineTypes.Contains(typeName) || IdentityPipelineNameRegex().IsMatch(typeName)))
                return "identity-pipeline-component";
        }

        return null;
    }

    private static void AddIdentityConfigProperties(SortedDictionary<string, string> properties, XElement element, string identityKind)
    {
        switch (identityKind)
        {
            case "authentication":
                AddClosedValue(properties, "authenticationMode", AttributeValue(element, "mode"), ["None", "Windows", "Forms", "Passport"]);
                break;
            case "forms-authentication":
                AddCookielessSetting(properties, element);
                AddBooleanSetting(properties, element, "enableCrossAppRedirects", "crossAppRedirectSetting");
                AddBooleanSetting(properties, element, "requireSSL", "requireSslSetting");
                AddBooleanSetting(properties, element, "slidingExpiration", "slidingExpirationSetting");
                properties["loginTargetDeclared"] = ValueDeclared(element, "loginUrl");
                properties["cookieNameDeclared"] = ValueDeclared(element, "name");
                break;
            case "authorization-rule":
                properties["authorizationAction"] = element.Name.LocalName.Equals("allow", StringComparison.OrdinalIgnoreCase) ? "allow" : "deny";
                AddSubjectPresence(properties, element, "users");
                AddSubjectPresence(properties, element, "roles");
                AddSubjectPresence(properties, element, "verbs");
                break;
            case "impersonation":
                AddBooleanSetting(properties, element, "impersonate", "impersonationSetting");
                properties["credentialAttributesDeclared"] = ValueDeclared(element, "userName") == "true" || ValueDeclared(element, "password") == "true" ? "true" : "false";
                break;
            case "machine-key-presence":
                properties["validationKeyDeclared"] = ValueDeclared(element, "validationKey");
                properties["decryptionKeyDeclared"] = ValueDeclared(element, "decryptionKey");
                AddClosedValue(properties, "validationAlgorithm", AttributeValue(element, "validation"), ["AES", "HMACSHA256", "HMACSHA384", "HMACSHA512", "MD5", "SHA1", "TripleDES"]);
                AddClosedValue(properties, "decryptionAlgorithm", AttributeValue(element, "decryption"), ["3DES", "AES", "Auto", "DES", "TripleDES"]);
                break;
            case "membership":
            case "role-manager":
                properties["defaultProviderDeclared"] = ValueDeclared(element, "defaultProvider");
                AddBooleanSetting(properties, element, "enabled", "enabledSetting");
                break;
            case "membership-provider":
            case "role-provider":
            case "identity-pipeline-component":
                var typeName = TypeName(element);
                AddSafeCodeName(properties, "typeName", typeName, "identity-state", identityKind);
                properties["providerClassification"] = typeName is not null && typeName.StartsWith("System.Web.", StringComparison.Ordinal)
                    ? "framework-declared"
                    : "custom-or-unresolved";
                break;
            case "session-state":
                AddClosedValue(properties, "sessionMode", AttributeValue(element, "mode"), ["Custom", "InProc", "Off", "SQLServer", "StateServer"]);
                AddCookielessSetting(properties, element);
                properties["timeoutDeclared"] = ValueDeclared(element, "timeout");
                properties["stateConnectionDeclared"] = ValueDeclared(element, "stateConnectionString") == "true" || ValueDeclared(element, "sqlConnectionString") == "true" ? "true" : "false";
                break;
            case "cookie-policy":
                AddBooleanSetting(properties, element, "httpOnlyCookies", "httpOnlySetting");
                AddBooleanSetting(properties, element, "requireSSL", "requireSslSetting");
                AddClosedValue(properties, "sameSite", AttributeValue(element, "sameSite"), ["Lax", "None", "Strict", "Unspecified"]);
                properties["domainDeclared"] = ValueDeclared(element, "domain");
                break;
            case "anonymous-identification":
            case "profile":
                AddBooleanSetting(properties, element, "enabled", "enabledSetting");
                break;
        }
    }

    private static bool IsIdentityConfigScope(XElement element) => IdentityConfigKind(element) is not null;

    private static string? TypeName(XElement element)
    {
        var value = AttributeValue(element, "type");
        return string.IsNullOrWhiteSpace(value) ? null : value.Split(',')[0].Trim();
    }

    private static string ValueDeclared(XElement element, string attributeName) => string.IsNullOrWhiteSpace(AttributeValue(element, attributeName)) ? "false" : "true";

    private static void AddBooleanSetting(SortedDictionary<string, string> properties, XElement element, string attributeName, string propertyName)
    {
        var value = AttributeValue(element, attributeName);
        properties[$"{propertyName}Declared"] = string.IsNullOrWhiteSpace(value) ? "false" : "true";
        if (bool.TryParse(value, out var parsed)) properties[propertyName] = parsed ? "true" : "false";
    }

    private static void AddCookielessSetting(SortedDictionary<string, string> properties, XElement element)
    {
        var value = AttributeValue(element, "cookieless");
        properties["cookielessSettingDeclared"] = string.IsNullOrWhiteSpace(value) ? "false" : "true";
        AddClosedValue(properties, "cookielessSetting", value, ["AutoDetect", "UseCookies", "UseDeviceProfile", "UseUri", "true", "false"]);
    }

    private static void AddClosedValue(SortedDictionary<string, string> properties, string propertyName, string? value, IReadOnlyList<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        properties[propertyName] = allowed.FirstOrDefault(candidate => candidate.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "unsupported-or-custom";
    }

    private static void AddSubjectPresence(SortedDictionary<string, string> properties, XElement element, string attributeName)
    {
        var value = AttributeValue(element, attributeName);
        properties[$"{attributeName}Declared"] = string.IsNullOrWhiteSpace(value) ? "false" : "true";
        if (string.IsNullOrWhiteSpace(value)) return;
        var values = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        properties[$"{attributeName}EntryCount"] = values.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        properties[$"{attributeName}WildcardDeclared"] = values.Contains("*", StringComparer.Ordinal) ? "true" : "false";
        if (attributeName == "users") properties["anonymousUserMarkerDeclared"] = values.Contains("?", StringComparer.Ordinal) ? "true" : "false";
    }

    private static void ExtractSiteMap(string repoPath, ScanManifest manifest, FileInventoryItem item, List<CodeFact> facts)
    {
        XDocument document;
        try
        {
            document = SafeXml.LoadDocument(Path.Combine(repoPath, item.RelativePath));
        }
        catch (SafeXmlException ex)
        {
            facts.Add(CreateGap(manifest, item.RelativePath, 1, RuleIds.LegacyAspNetNavigation, $"SiteMapXml{ex.FailureKind}", "Unable to parse checked-in sitemap with the safe XML parser.", null));
            return;
        }
        catch (Exception ex) when (IsXmlIoException(ex))
        {
            facts.Add(CreateGap(manifest, item.RelativePath, 1, RuleIds.LegacyAspNetNavigation, "SiteMapXmlIoFailure", "Unable to read checked-in sitemap for safe XML parsing.", null));
            return;
        }

        foreach (var node in document.Descendants().Where(element => element.Name.LocalName.Equals("siteMapNode", StringComparison.OrdinalIgnoreCase)).OrderBy(LineNumber))
        {
            var url = AttributeValue(node, "url");
            if (string.IsNullOrWhiteSpace(url))
            {
                facts.Add(CreateGap(manifest, item.RelativePath, LineNumber(node), RuleIds.LegacyAspNetNavigation, "SiteMapNodeMissingUrl", "SiteMap node has no url attribute; navigation target cannot be resolved statically.", null));
                continue;
            }

            AddNavigationReference(manifest, facts, item.RelativePath, LineNumber(node), "SiteMapNode", item.RelativePath, url);
        }
    }

    private static void ExtractMarkupNavigation(
        string repoPath,
        ScanManifest manifest,
        FileInventoryItem item,
        CodeFact? pageFact,
        List<CodeFact> facts)
    {
        if (!TryRead(repoPath, item.RelativePath, out var text))
        {
            facts.Add(CreateGap(manifest, item.RelativePath, 1, RuleIds.LegacyAspNetNavigation, "UnreadableNavigationMarkup", "Unable to read WebForms markup for navigation extraction.", null));
            return;
        }

        var source = SourceText.From(text);
        var scanText = MaskMarkupComments(text);
        foreach (Match tag in MarkupTagRegex().Matches(scanText).Cast<Match>())
        {
            var attrs = ParseAttributes(tag.Groups["attrs"].Value);
            foreach (var (name, value) in attrs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (NavigationAttributes.Contains(name))
                {
                    AddNavigationReference(manifest, facts, item.RelativePath, LineAt(source, tag.Index), $"Markup{name}", pageFact?.FactId ?? item.RelativePath, ResolveSourceRelativeNavigationTarget(item.RelativePath, value));
                }
            }
        }
    }

    private static void ExtractCSharpFile(
        string repoPath,
        ScanManifest manifest,
        FileInventoryItem item,
        IReadOnlyDictionary<string, CodeFact[]> semanticTypesByFile,
        IReadOnlyDictionary<string, CodeFact[]> semanticMethodsByFile,
        IReadOnlyDictionary<string, CodeFact[]> asmxOperationsByFile,
        IReadOnlyList<CodeFact> existingFacts,
        List<CodeFact> facts,
        bool hasAspNetCandidateInventory)
    {
        if (!TryRead(repoPath, item.RelativePath, out var text))
        {
            facts.Add(CreateGap(manifest, item.RelativePath, 1, RuleIds.LegacyAspNetSurface, "UnreadableAspNetCodeFile", "Unable to read C# file for ASP.NET extraction.", null));
            return;
        }

        SyntaxTree tree;
        CompilationUnitSyntax root;
        try
        {
            tree = CSharpSyntaxTree.ParseText(SourceText.From(text), path: item.RelativePath);
            root = tree.GetCompilationUnitRoot();
        }
        catch (Exception ex) when (ex is ArgumentException)
        {
            facts.Add(CreateGap(manifest, item.RelativePath, 1, RuleIds.LegacyAspNetSurface, "MalformedAspNetCodeFile", "Unable to parse C# file for ASP.NET extraction.", null));
            return;
        }

        ExtractRoutes(manifest, item, tree, root, facts);
        ExtractHandlersAndPageMethods(manifest, item, tree, root, semanticTypesByFile, semanticMethodsByFile, asmxOperationsByFile, facts);
        ExtractCodeNavigation(manifest, item, tree, root, facts);
        if (hasAspNetCandidateInventory)
        {
            ExtractIdentityCodeGaps(manifest, item, tree, root, existingFacts, facts);
        }
    }

    private static void ExtractIdentityCodeGaps(
        ScanManifest manifest,
        FileInventoryItem item,
        SyntaxTree tree,
        CompilationUnitSyntax root,
        IReadOnlyList<CodeFact> existingFacts,
        List<CodeFact> facts)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                     .Where(invocation => InvocationName(invocation).Equals("RegisterModule", StringComparison.Ordinal))
                     .Where(invocation => invocation.Expression.ToString().Contains("DynamicModuleUtility", StringComparison.Ordinal)))
        {
            facts.Add(CreateGap(
                manifest,
                item.RelativePath,
                SpanLine(tree, invocation),
                RuleIds.LegacyAspNetIdentityState,
                "DynamicIdentityPipelineRegistrationUnsupported",
                "Runtime module registration was observed, but TraceMap does not execute registration code or infer effective identity-pipeline ordering.",
                null));
        }

        if (manifest.BuildStatus == "Succeeded")
        {
            return;
        }

        foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var identityInterface = type.BaseList?.Types
                .Select(baseType => baseType.Type.ToString().Split('.').Last())
                .FirstOrDefault(baseName => baseName is "IPrincipal" or "IIdentity");
            if (identityInterface is null)
            {
                continue;
            }

            var typeName = QualifiedTypeName(type);
            var hasSemanticRelationship = existingFacts.Any(fact =>
                fact.FactType == FactTypes.SymbolRelationship
                && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
                && fact.Evidence.FilePath == item.RelativePath
                && IsIdentitySourceSymbol(fact.SourceSymbol, typeName)
                && IsIdentityInterfaceSymbol(fact.TargetSymbol));
            if (hasSemanticRelationship)
            {
                continue;
            }

            facts.Add(CreateGap(
                manifest,
                item.RelativePath,
                SpanLine(tree, type),
                RuleIds.LegacyAspNetIdentityState,
                "IdentitySemanticDependencyUnavailable",
                $"A syntax-level {identityInterface} candidate could not be confirmed with compiler-resolved relationship evidence under reduced build coverage.",
                null));
        }
    }

    private static void ExtractRoutes(
        ScanManifest manifest,
        FileInventoryItem item,
        SyntaxTree tree,
        CompilationUnitSyntax root,
        List<CodeFact> facts)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var name = InvocationName(invocation);
            if (name.Equals("MapPageRoute", StringComparison.Ordinal))
            {
                ExtractMapPageRoute(manifest, item, tree, invocation, facts);
            }
            else if (name.Equals("Add", StringComparison.Ordinal) && LooksLikeRouteAdd(invocation))
            {
                ExtractRouteAdd(manifest, item, tree, invocation, facts);
            }
        }
    }

    private static void ExtractMapPageRoute(
        ScanManifest manifest,
        FileInventoryItem item,
        SyntaxTree tree,
        InvocationExpressionSyntax invocation,
        List<CodeFact> facts)
    {
        var args = invocation.ArgumentList.Arguments;
        var line = SpanLine(tree, invocation);
        if (args.Count < 3)
        {
            facts.Add(CreateGap(manifest, item.RelativePath, line, RuleIds.LegacyAspNetRoute, "UnsupportedRouteRegistrationShape", "MapPageRoute call has fewer than the supported static arguments.", null));
            return;
        }

        var routeName = StringLiteral(args[0].Expression);
        var routePattern = StringLiteral(args[1].Expression);
        var mappedPage = StringLiteral(args[2].Expression);
        if (routeName is null || routePattern is null || mappedPage is null)
        {
            facts.Add(CreateGap(manifest, item.RelativePath, line, RuleIds.LegacyAspNetRoute, "DynamicRouteRegistration", "Route registration uses dynamic strings or unsupported expressions; TraceMap does not invent concrete endpoints.", null));
            return;
        }

        AddRouteFact(manifest, facts, item.RelativePath, line, "MapPageRoute", routeName, routePattern, mappedPage);
    }

    private static void ExtractRouteAdd(
        ScanManifest manifest,
        FileInventoryItem item,
        SyntaxTree tree,
        InvocationExpressionSyntax invocation,
        List<CodeFact> facts)
    {
        var line = SpanLine(tree, invocation);
        var args = invocation.ArgumentList.Arguments;
        if (args.Count < 2 || args[1].Expression is not ObjectCreationExpressionSyntax routeCreation)
        {
            facts.Add(CreateGap(manifest, item.RelativePath, line, RuleIds.LegacyAspNetRoute, "UnsupportedRouteAddShape", "RouteCollection.Add call does not use the supported static Route construction shape.", null));
            return;
        }

        var routeName = StringLiteral(args[0].Expression);
        var routePattern = routeCreation.ArgumentList?.Arguments.Count > 0 ? StringLiteral(routeCreation.ArgumentList.Arguments[0].Expression) : null;
        var mappedPage = routeCreation.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            .Where(creation => creation.Type.ToString().EndsWith("PageRouteHandler", StringComparison.Ordinal))
            .Select(creation => creation.ArgumentList?.Arguments.Count > 0 ? StringLiteral(creation.ArgumentList.Arguments[0].Expression) : null)
            .FirstOrDefault(value => value is not null);
        if (routeName is null || routePattern is null || mappedPage is null)
        {
            facts.Add(CreateGap(manifest, item.RelativePath, line, RuleIds.LegacyAspNetRoute, "DynamicRouteRegistration", "RouteCollection.Add uses dynamic or unsupported route construction; TraceMap does not simulate route tables.", null));
            return;
        }

        AddRouteFact(manifest, facts, item.RelativePath, line, "RouteCollectionAddPageRouteHandler", routeName, routePattern, mappedPage);
    }

    private static void AddRouteFact(
        ScanManifest manifest,
        List<CodeFact> facts,
        string filePath,
        int line,
        string routeShape,
        string routeName,
        string routePattern,
        string mappedPage)
    {
        var properties = BaseProperties(manifest, RuleIds.LegacyAspNetRoute, "Route evidence is static registration evidence only and does not prove route table execution, request matching, URL rewriting, deployment, reachability, or auth.");
        properties["routeShape"] = routeShape;
        AddSafeIdentifier(properties, "routeName", routeName, "route", "route-name");
        AddAspNetHash(properties, "routePatternHash", "route", "route-pattern", routePattern);
        AddSafeNavigationTarget(properties, "mappedPagePath", mappedPage, "route", "mapped-page");
        properties["defaultsPresent"] = "unknown";
        properties["constraintsPresent"] = "unknown";
        properties["dataTokensPresent"] = "unknown";

        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.AspNetRouteDeclared,
            RuleIds.LegacyAspNetRoute,
            EvidenceTiers.Tier3SyntaxOrTextual,
            Evidence(filePath, line, line),
            targetSymbol: properties.GetValueOrDefault("mappedPagePath"),
            contractElement: properties.GetValueOrDefault("routeName") ?? properties.GetValueOrDefault("routeNameHash"),
            properties: properties));
    }

    private static void ExtractHandlersAndPageMethods(
        ScanManifest manifest,
        FileInventoryItem item,
        SyntaxTree tree,
        CompilationUnitSyntax root,
        IReadOnlyDictionary<string, CodeFact[]> semanticTypesByFile,
        IReadOnlyDictionary<string, CodeFact[]> semanticMethodsByFile,
        IReadOnlyDictionary<string, CodeFact[]> asmxOperationsByFile,
        List<CodeFact> facts)
    {
        foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var typeName = QualifiedTypeName(type);
            var baseNames = type.BaseList?.Types.Select(baseType => baseType.Type.ToString()).ToArray() ?? [];
            var handlerBase = baseNames.FirstOrDefault(IsHandlerInterfaceOrFactory);
            if (handlerBase is not null)
            {
                var isFactory = handlerBase.EndsWith("HandlerFactory", StringComparison.Ordinal);
                var properties = BaseProperties(manifest, RuleIds.LegacyAspNetHandler, "Handler interface evidence is static type evidence only and does not prove request execution, factory result, pipeline ordering, auth, deployment, or reachability.");
                properties["handlerKind"] = isFactory ? "handler-factory-type" : "handler-interface-type";
                properties["handlerInterface"] = handlerBase.Split('.').Last();
                properties["typeName"] = typeName;
                if (isFactory)
                {
                    properties["factoryTarget"] = "unresolved";
                }

                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.AspNetHandlerDeclared,
                    RuleIds.LegacyAspNetHandler,
                    FindSemanticType(semanticTypesByFile, item.RelativePath, typeName) is not null ? EvidenceTiers.Tier1Semantic : EvidenceTiers.Tier3SyntaxOrTextual,
                    Evidence(item.RelativePath, SpanLine(tree, type), EndLine(tree, type)),
                    targetSymbol: typeName,
                    contractElement: handlerBase.Split('.').Last(),
                    properties: properties));
            }

            var scriptService = type.AttributeLists.SelectMany(list => list.Attributes).Any(attribute => AttributeNameMatches(attribute, "ScriptService"));
            if (scriptService)
            {
                var properties = BaseProperties(manifest, RuleIds.LegacyAspNetPageMethod, "ScriptService class evidence is static attribute evidence only and does not prove AJAX execution, generated script reachability, serialization behavior, auth, deployment, or runtime impact.");
                properties["attributeNames"] = "ScriptService";
                properties["containingTypeName"] = typeName;
                properties["pageMethodKind"] = "script-service-class";
                properties["scriptServiceClass"] = "True";

                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.AspNetPageMethodDeclared,
                    RuleIds.LegacyAspNetPageMethod,
                    FindSemanticType(semanticTypesByFile, item.RelativePath, typeName) is not null ? EvidenceTiers.Tier1Semantic : EvidenceTiers.Tier3SyntaxOrTextual,
                    Evidence(item.RelativePath, SpanLine(tree, type), SpanLine(tree, type)),
                    targetSymbol: typeName,
                    contractElement: type.Identifier.ValueText,
                    properties: properties));
            }

            foreach (var method in type.Members.OfType<MethodDeclarationSyntax>())
            {
                var attrs = method.AttributeLists.SelectMany(list => list.Attributes).Where(IsPageMethodAttribute).ToArray();
                if (attrs.Length == 0)
                {
                    continue;
                }

                var attributeNames = attrs.Select(attribute => AttributeShortName(attribute)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var methodName = method.Identifier.ValueText;
                var symbol = $"{typeName}.{methodName}";
                if (IsAsmxOperationMethod(asmxOperationsByFile, item.RelativePath, typeName, methodName, symbol))
                {
                    continue;
                }

                var properties = BaseProperties(manifest, RuleIds.LegacyAspNetPageMethod, "PageMethod and ScriptMethod evidence is static attribute evidence only and does not prove AJAX execution, serialization behavior, auth, script reachability, or ASMX hosting.");
                properties["attributeNames"] = string.Join(",", attributeNames);
                properties["containingTypeName"] = typeName;
                properties["isStatic"] = method.Modifiers.Any(SyntaxKind.StaticKeyword).ToString();
                properties["methodName"] = methodName;
                properties["scriptServiceClass"] = scriptService.ToString();
                if (!method.Modifiers.Any(SyntaxKind.StaticKeyword))
                {
                    properties["classificationCap"] = "NeedsReview";
                }

                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.AspNetPageMethodDeclared,
                    RuleIds.LegacyAspNetPageMethod,
                    FindSemanticMethod(semanticMethodsByFile, item.RelativePath, methodName) is not null ? EvidenceTiers.Tier1Semantic : EvidenceTiers.Tier3SyntaxOrTextual,
                    Evidence(item.RelativePath, SpanLine(tree, method), EndLine(tree, method)),
                    sourceSymbol: typeName,
                    targetSymbol: symbol,
                    contractElement: methodName,
                    properties: properties));
            }
        }
    }

    private static void ExtractCodeNavigation(
        ScanManifest manifest,
        FileInventoryItem item,
        SyntaxTree tree,
        CompilationUnitSyntax root,
        List<CodeFact> facts)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var expression = invocation.Expression.ToString();
            var name = InvocationName(invocation);
            if (name is "Redirect" or "Transfer" && (expression.Contains("Response.", StringComparison.Ordinal) || expression.Contains("Server.", StringComparison.Ordinal)))
            {
                var line = SpanLine(tree, invocation);
                var first = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
                var target = first is null ? null : StringLiteral(first);
                if (target is null)
                {
                    facts.Add(CreateGap(manifest, item.RelativePath, line, RuleIds.LegacyAspNetNavigation, "DynamicCodeNavigationTarget", "Navigation API target is dynamic or unsupported; no concrete target edge is emitted.", null));
                }
                else
                {
                    AddNavigationReference(manifest, facts, item.RelativePath, line, $"Code{name}", item.RelativePath, ResolveSourceRelativeNavigationTarget(item.RelativePath, target));
                }
            }
        }

        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            var leftName = assignment.Left is MemberAccessExpressionSyntax member
                ? member.Name.Identifier.ValueText
                : string.Empty;
            if (!NavigationAttributes.Contains(leftName))
            {
                continue;
            }

            var line = SpanLine(tree, assignment);
            var target = StringLiteral(assignment.Right);
            if (target is null)
            {
                facts.Add(CreateGap(manifest, item.RelativePath, line, RuleIds.LegacyAspNetNavigation, "DynamicCodeNavigationTarget", "Navigation property target is dynamic or unsupported; no concrete target edge is emitted.", null));
            }
            else
            {
                AddNavigationReference(manifest, facts, item.RelativePath, line, $"Code{leftName}", item.RelativePath, ResolveSourceRelativeNavigationTarget(item.RelativePath, target));
            }
        }
    }

    private static void AddNavigationReference(
        ScanManifest manifest,
        List<CodeFact> facts,
        string filePath,
        int line,
        string referenceKind,
        string sourceSurface,
        string target)
    {
        var properties = BaseProperties(manifest, RuleIds.LegacyAspNetNavigation, "Navigation evidence is a static reference candidate only and does not prove browser behavior, JavaScript execution, data binding, role trimming, auth, page rendering, user reachability, or backend impact.");
        properties["referenceKind"] = referenceKind;
        properties["sourceSurface"] = sourceSurface;

        if (LooksSecretLike(target))
        {
            properties["targetOmitted"] = "secret-like";
        }
        else if (LooksJavaScriptNavigation(target))
        {
            facts.Add(CreateGap(manifest, filePath, line, RuleIds.LegacyAspNetNavigation, "JavaScriptNavigationUnsupported", "JavaScript-generated navigation is not executed or resolved by static extraction.", null));
            return;
        }
        else if (LooksDynamicMarkupValue(target))
        {
            facts.Add(CreateGap(manifest, filePath, line, RuleIds.LegacyAspNetNavigation, "DynamicNavigationTarget", "Navigation target is data-bound, expression-based, or otherwise dynamic; no concrete target edge is emitted.", null));
            return;
        }
        else
        {
            AddSafeNavigationTarget(properties, "targetPath", target, "navigation", "navigation-target");
        }

        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.AspNetNavigationReferenceDeclared,
            RuleIds.LegacyAspNetNavigation,
            EvidenceTiers.Tier3SyntaxOrTextual,
            Evidence(filePath, line, line),
            sourceSymbol: sourceSurface,
            targetSymbol: properties.GetValueOrDefault("targetPath"),
            contractElement: referenceKind,
            properties: properties));
    }

    private static void AddNavigationEdges(
        ScanManifest manifest,
        IReadOnlyDictionary<string, CodeFact> pageFacts,
        List<CodeFact> facts)
    {
        var targetFacts = pageFacts.Values
            .Concat(facts.Where(fact => fact.FactType is FactTypes.AspNetHandlerDeclared or FactTypes.AspNetRouteDeclared or FactTypes.AspNetConfigSurfaceDeclared))
            .OrderBy(TargetPriority)
            .ThenBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();

        foreach (var reference in facts.Where(fact => fact.FactType == FactTypes.AspNetNavigationReferenceDeclared).OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray())
        {
            if (!reference.Properties.TryGetValue("targetPath", out var targetPath) || string.IsNullOrWhiteSpace(targetPath))
            {
                continue;
            }

            var target = targetFacts.FirstOrDefault(fact => StaticTargetMatches(fact, targetPath));
            if (target is null)
            {
                continue;
            }

            var properties = BaseProperties(manifest, RuleIds.LegacyAspNetNavigation, "Navigation edge evidence is a conservative static link and does not prove runtime reachability, route matching, auth, deployment, or page rendering.");
            properties["edgeKind"] = "static-navigation-target";
            properties["referenceFactId"] = reference.FactId;
            properties["targetFactId"] = target.FactId;
            properties["targetFactType"] = target.FactType;
            properties["supportingFactIds"] = string.Join(",", new[] { reference.FactId, target.FactId }.OrderBy(value => value, StringComparer.Ordinal));

            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.AspNetNavigationEdgeDeclared,
                RuleIds.LegacyAspNetNavigation,
                WeakestTier(reference.EvidenceTier, target.EvidenceTier),
                reference.Evidence,
                sourceSymbol: reference.SourceSymbol,
                targetSymbol: target.TargetSymbol ?? target.ContractElement,
                contractElement: "static-navigation-target",
                properties: properties));
        }
    }

    private static bool StaticTargetMatches(CodeFact fact, string targetPath)
    {
        if (fact.Evidence.FilePath.Equals(targetPath, StringComparison.Ordinal))
        {
            return true;
        }

        if (fact.Properties.TryGetValue("mappedPagePath", out var mappedPagePath) && mappedPagePath.Equals(targetPath, StringComparison.Ordinal))
        {
            return true;
        }

        return MatchesProperty(fact, "pathDescriptor", targetPath)
            || MatchesProperty(fact, "urlDescriptor", targetPath)
            || MatchesProperty(fact, "mappedUrlDescriptor", targetPath);
    }

    private static bool MatchesProperty(CodeFact fact, string propertyName, string targetPath)
    {
        return fact.Properties.TryGetValue(propertyName, out var descriptor)
            && descriptor.Equals(targetPath, StringComparison.Ordinal);
    }

    private static int TargetPriority(CodeFact fact)
    {
        return fact.FactType switch
        {
            FactTypes.WebFormsPageDeclared => 0,
            FactTypes.AspNetHandlerDeclared => 1,
            FactTypes.AspNetRouteDeclared => 2,
            FactTypes.AspNetConfigSurfaceDeclared => 3,
            _ => 9
        };
    }

    private static bool IsConfigCandidate(XElement element)
    {
        var name = element.Name.LocalName;
        if (name.Equals("pages", StringComparison.OrdinalIgnoreCase)
            || name.Equals("compilation", StringComparison.OrdinalIgnoreCase))
        {
            return ParentNameEquals(element, "system.web");
        }

        if (!name.Equals("add", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return element.Parent?.Name.LocalName switch
        {
            "httpHandlers" or "httpModules" => HasAncestorNamed(element, "system.web"),
            "handlers" or "modules" => HasAncestorNamed(element, "system.webServer"),
            "controls" or "namespaces" => HasAncestorNamed(element, "pages") && HasAncestorNamed(element, "system.web"),
            "urlMappings" => HasAncestorNamed(element, "system.web"),
            _ => false
        };
    }

    private static string SectionKind(XElement element)
    {
        var parent = element.Parent?.Name.LocalName ?? string.Empty;
        return element.Name.LocalName.Equals("pages", StringComparison.OrdinalIgnoreCase) ? "system.web/pages"
            : element.Name.LocalName.Equals("compilation", StringComparison.OrdinalIgnoreCase) ? "system.web/compilation"
            : parent switch
            {
                "httpHandlers" => "system.web/httpHandlers",
                "handlers" => "system.webServer/handlers",
                "httpModules" => "system.web/httpModules",
                "modules" => "system.webServer/modules",
                "controls" => "system.web/pages/controls",
                "namespaces" => "system.web/pages/namespaces",
                "urlMappings" => "system.web/urlMappings",
                _ => $"aspnet-config/{parent}"
            };
    }

    private static void AddConfigElementProperties(SortedDictionary<string, string> properties, XElement element)
    {
        foreach (var attribute in element.Attributes().OrderBy(attribute => attribute.Name.LocalName, StringComparer.Ordinal))
        {
            var name = attribute.Name.LocalName;
            var value = attribute.Value;
            switch (name)
            {
                case "path":
                case "url":
                case "mappedUrl":
                case "src":
                    AddSafeNavigationTarget(properties, name == "path" ? "pathDescriptor" : $"{name}Descriptor", value, "config", $"config-{Dash(name)}");
                    break;
                case "verb":
                case "name":
                case "tagPrefix":
                case "namespace":
                case "debug":
                case "targetFramework":
                    AddSafeIdentifier(properties, name, value, "config", $"config-{Dash(name)}");
                    break;
                case "type":
                    AddSafeCodeName(properties, "typeName", value, "config", "config-type");
                    break;
            }
        }
    }

    private static void AddLocationScope(SortedDictionary<string, string> properties, XElement element)
    {
        var location = element.Ancestors().FirstOrDefault(ancestor => ancestor.Name.LocalName.Equals("location", StringComparison.OrdinalIgnoreCase));
        if (location is null)
        {
            return;
        }

        AddSafeConfigLocationPath(properties, "configScopePath", AttributeValue(location, "path"));
    }

    private static bool HasUnsupportedConfigSource(XElement element)
    {
        return element.Attributes().Any(attribute => attribute.Name.LocalName is "configSource" or "file");
    }

    private static void AddIdentityControlFacts(ScanManifest manifest, IReadOnlyList<CodeFact> existingFacts, List<CodeFact> facts)
    {
        foreach (var control in existingFacts
                     .Where(fact => fact.FactType == FactTypes.WebFormsControlDeclared)
                     .Where(fact => IdentityControlTypes.Contains(fact.Properties.GetValueOrDefault("controlType") ?? string.Empty))
                     .OrderBy(fact => fact.FactId, StringComparer.Ordinal))
        {
            var properties = BaseProperties(manifest, RuleIds.LegacyAspNetIdentityState,
                "Login-control rows are static markup declarations and do not prove rendering, authentication, authorization, credential handling, or runtime use.");
            properties["identityKind"] = "login-control";
            properties["controlType"] = control.Properties["controlType"];
            properties["declarationStatus"] = "declared-in-markup";
            properties["supportingFactIds"] = control.FactId;
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.AspNetIdentityStateDeclared,
                RuleIds.LegacyAspNetIdentityState,
                EvidenceTiers.Tier2Structural,
                Evidence(control.Evidence.FilePath, control.Evidence.StartLine, control.Evidence.EndLine),
                sourceSymbol: control.SourceSymbol,
                targetSymbol: control.TargetSymbol,
                contractElement: "login-control",
                properties: properties));
        }
    }

    private static void AddIdentityTypeFacts(ScanManifest manifest, IReadOnlyList<CodeFact> existingFacts, List<CodeFact> facts)
    {
        foreach (var relationship in existingFacts
                     .Where(fact => fact.FactType == FactTypes.SymbolRelationship && fact.EvidenceTier == EvidenceTiers.Tier1Semantic)
                     .Where(fact => IsIdentityInterfaceSymbol(fact.TargetSymbol))
                     .OrderBy(fact => fact.FactId, StringComparer.Ordinal))
        {
            var kind = relationship.TargetSymbol!.EndsWith("IPrincipal", StringComparison.Ordinal) ? "custom-principal-type" : "custom-identity-type";
            var properties = BaseProperties(manifest, RuleIds.LegacyAspNetIdentityState,
                "Custom principal/identity rows preserve compiler-resolved interface evidence but do not prove runtime assignment, claims, authentication, authorization, or effective identity flow.");
            properties["identityKind"] = kind;
            properties["declarationStatus"] = "compiler-resolved-interface";
            properties["supportingFactIds"] = relationship.FactId;
            AddSafeCodeName(properties, "typeName", relationship.SourceSymbol, "identity-state", kind);
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.AspNetIdentityStateDeclared,
                RuleIds.LegacyAspNetIdentityState,
                EvidenceTiers.Tier1Semantic,
                Evidence(relationship.Evidence.FilePath, relationship.Evidence.StartLine, relationship.Evidence.EndLine),
                sourceSymbol: relationship.SourceSymbol,
                targetSymbol: relationship.TargetSymbol,
                contractElement: kind,
                properties: properties));
        }
    }

    private static bool IsIdentityInterfaceSymbol(string? symbol)
    {
        var normalized = NormalizeGlobalSymbol(symbol);
        return normalized is "System.Security.Principal.IPrincipal" or "System.Security.Principal.IIdentity";
    }

    private static bool IsIdentitySourceSymbol(string? symbol, string typeName)
    {
        var normalized = RemoveGenericTypeParameters(NormalizeGlobalSymbol(symbol));
        return normalized?.Equals(typeName, StringComparison.Ordinal) == true
            || normalized?.EndsWith($".{typeName}", StringComparison.Ordinal) == true;
    }

    private static string? RemoveGenericTypeParameters(string? symbol)
    {
        if (symbol is null || !symbol.Contains('<', StringComparison.Ordinal))
        {
            return symbol;
        }

        var builder = new StringBuilder(symbol.Length);
        var depth = 0;
        foreach (var character in symbol)
        {
            if (character == '<')
            {
                depth++;
            }
            else if (character == '>' && depth > 0)
            {
                depth--;
            }
            else if (depth == 0)
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static string? NormalizeGlobalSymbol(string? symbol)
    {
        const string globalPrefix = "global::";
        return symbol?.StartsWith(globalPrefix, StringComparison.Ordinal) == true
            ? symbol[globalPrefix.Length..]
            : symbol;
    }

    private static CodeFact CreateGap(
        ScanManifest manifest,
        string filePath,
        int line,
        string ruleId,
        string gapKind,
        string message,
        string? snippetHash,
        string? supportingFactId = null)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["coverageLabel"] = manifest.BuildStatus == "Succeeded" ? "Full" : "Reduced",
            ["gapKind"] = gapKind,
            ["message"] = message,
            ["ruleLimitations"] = "Analysis gaps preserve uncertainty and must not be read as absence findings."
        };
        if (!string.IsNullOrWhiteSpace(supportingFactId)) properties["supportingFactIds"] = supportingFactId;
        return FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            ruleId,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(filePath, line, line, snippetHash, ExtractorId, ScannerVersions.LegacyAspNetExtractor),
            contractElement: gapKind,
            properties: properties);
    }

    private static SortedDictionary<string, string> BaseProperties(ScanManifest manifest, string ruleId, string limitations)
    {
        return new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["coverageLabel"] = manifest.BuildStatus == "Succeeded" ? "Full" : "Reduced",
            ["extractorVersion"] = ScannerVersions.LegacyAspNetExtractor,
            ["ruleId"] = ruleId,
            ["ruleLimitations"] = limitations
        };
    }

    private static EvidenceSpan Evidence(string filePath, int startLine, int endLine)
    {
        return new EvidenceSpan(filePath, startLine, Math.Max(startLine, endLine), null, ExtractorId, ScannerVersions.LegacyAspNetExtractor);
    }

    private static void AddSafeIdentifier(SortedDictionary<string, string> properties, string key, string? value, string family, string role)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();
        if (LooksSecretLike(trimmed))
        {
            properties[$"{key}Omitted"] = "secret-like";
        }
        else if (SafeIdentifierRegex().IsMatch(trimmed) && !ContainsUnsafeValueShape(trimmed))
        {
            properties[key] = trimmed;
        }
        else
        {
            AddAspNetHash(properties, $"{key}Hash", family, role, trimmed);
        }
    }

    private static void AddSafeCodeName(SortedDictionary<string, string> properties, string key, string? value, string family, string role)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();
        if (LooksSecretLike(trimmed))
        {
            properties[$"{key}Omitted"] = "secret-like";
        }
        else if (SafeCodeNameRegex().IsMatch(trimmed) && !ContainsUnsafeValueShape(trimmed))
        {
            properties[key] = trimmed;
        }
        else
        {
            AddAspNetHash(properties, $"{key}Hash", family, role, trimmed);
        }
    }

    private static void AddSafeMarkupPath(SortedDictionary<string, string> properties, string key, string? value, string family, string role)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (LooksSecretLike(value))
        {
            properties[$"{key}Omitted"] = "secret-like";
            return;
        }

        var safe = SafeMarkupPath(value);
        if (safe is not null)
        {
            properties[key] = safe;
        }
        else
        {
            AddAspNetHash(properties, $"{key}Hash", family, role, value);
        }
    }

    private static void AddSafeNavigationTarget(SortedDictionary<string, string> properties, string key, string? value, string family, string role)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (LooksSecretLike(value))
        {
            properties[$"{key}Omitted"] = "secret-like";
            return;
        }

        var safe = SafeNavigationPath(value);
        if (safe is not null)
        {
            properties[key] = safe;
        }
        else
        {
            AddAspNetHash(properties, $"{key}Hash", family, role, value);
        }
    }

    private static void AddSafeConfigLocationPath(SortedDictionary<string, string> properties, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (LooksSecretLike(value))
        {
            properties[$"{key}Omitted"] = "secret-like";
            return;
        }

        var safe = SafeConfigLocationPath(value);
        if (safe is not null)
        {
            properties[key] = safe;
        }
        else
        {
            AddAspNetHash(properties, $"{key}Hash", "config", "config-location-path", value);
        }
    }

    private static void AddAspNetHash(SortedDictionary<string, string> properties, string key, string family, string role, string value)
    {
        properties[key] = AspNetHash(family, role, value);
    }

    private static string AspNetHash(string family, string role, string value)
    {
        return FactFactory.Hash($"legacy.aspnet.{family}|{role}|{NormalizeHashValue(value)}", 32);
    }

    private static string NormalizeHashValue(string value)
    {
        return value.Trim().Replace('\\', '/').ReplaceLineEndings(" ");
    }

    private static string? SafeMarkupPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var raw = value.Trim();
        if (raw.StartsWith("/", StringComparison.Ordinal)
            || raw.StartsWith("\\", StringComparison.Ordinal)
            || raw.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        var normalized = NormalizeAspNetPath(raw);
        if (normalized.Contains("://", StringComparison.Ordinal)
            || normalized.Contains(':', StringComparison.Ordinal)
            || normalized.Contains("..", StringComparison.Ordinal)
            || normalized.Contains('$', StringComparison.Ordinal)
            || normalized.Contains('%', StringComparison.Ordinal)
            || normalized.Contains('{', StringComparison.Ordinal)
            || normalized.Contains('}', StringComparison.Ordinal)
            || normalized.Contains('?', StringComparison.Ordinal)
            || normalized.Contains('#', StringComparison.Ordinal)
            || normalized.StartsWith("/", StringComparison.Ordinal)
            || normalized.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        return SafeRelativePathRegex().IsMatch(normalized) ? normalized : null;
    }

    private static string? SafeNavigationPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var raw = value.Trim();
        if (raw.StartsWith("/", StringComparison.Ordinal)
            || raw.StartsWith("\\", StringComparison.Ordinal)
            || raw.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        var normalized = NormalizeAspNetPath(raw);
        if (normalized.Length > 180
            || normalized.Contains("://", StringComparison.Ordinal)
            || normalized.Contains(':', StringComparison.Ordinal)
            || normalized.Contains("..", StringComparison.Ordinal)
            || normalized.Contains('$', StringComparison.Ordinal)
            || normalized.Contains('%', StringComparison.Ordinal)
            || normalized.Contains('{', StringComparison.Ordinal)
            || normalized.Contains('}', StringComparison.Ordinal)
            || normalized.Contains('?', StringComparison.Ordinal)
            || normalized.Contains('#', StringComparison.Ordinal)
            || normalized.StartsWith("/", StringComparison.Ordinal)
            || normalized.StartsWith("//", StringComparison.Ordinal)
            || normalized.StartsWith("javascript", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return SafeRelativePathRegex().IsMatch(normalized) ? normalized : null;
    }

    private static string? SafeConfigLocationPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var raw = value.Trim();
        if (raw.StartsWith("/", StringComparison.Ordinal)
            || raw.Contains("://", StringComparison.Ordinal)
            || raw.Contains(':', StringComparison.Ordinal)
            || raw.Contains("..", StringComparison.Ordinal)
            || raw.Contains('$', StringComparison.Ordinal)
            || raw.Contains('%', StringComparison.Ordinal)
            || raw.Contains('?', StringComparison.Ordinal)
            || raw.Contains('#', StringComparison.Ordinal)
            || raw.Contains('{', StringComparison.Ordinal)
            || raw.Contains('}', StringComparison.Ordinal)
            || raw.Contains('*', StringComparison.Ordinal))
        {
            return null;
        }

        var normalized = NormalizeAspNetPath(raw);
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length is 0 or > 2)
        {
            return null;
        }

        return SafeConfigLocationPathRegex().IsMatch(normalized) ? normalized : null;
    }

    private static string NormalizeAspNetPath(string value)
    {
        return FileInventory.NormalizeRelativePath(value.Trim().Replace('\\', '/').TrimStart('~', '/'));
    }

    private static string ResolveSourceRelativeNavigationTarget(string sourcePath, string target)
    {
        var raw = target.Trim();
        if (raw.StartsWith("~", StringComparison.Ordinal)
            || raw.StartsWith("/", StringComparison.Ordinal)
            || raw.StartsWith("\\", StringComparison.Ordinal)
            || raw.Contains("://", StringComparison.Ordinal)
            || LooksJavaScriptNavigation(raw))
        {
            return raw;
        }

        var sourceDirectory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            return raw;
        }

        return $"{sourceDirectory}/{raw}";
    }

    private static bool ContainsUnsafeValueShape(string value)
    {
        return value.Contains("://", StringComparison.Ordinal)
            || value.Contains('/', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Contains('$', StringComparison.Ordinal)
            || value.Contains('%', StringComparison.Ordinal)
            || value.Contains('?', StringComparison.Ordinal)
            || value.Contains('#', StringComparison.Ordinal)
            || value.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksDynamicMarkupValue(string value)
    {
        return value.Contains("<%", StringComparison.Ordinal)
            || value.Contains("%>", StringComparison.Ordinal)
            || value.Contains("<%#", StringComparison.Ordinal)
            || value.Contains("Eval(", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Bind(", StringComparison.OrdinalIgnoreCase)
            || value.Contains("$(", StringComparison.Ordinal)
            || value.Contains("${", StringComparison.Ordinal);
    }

    private static string MaskMarkupComments(string text)
    {
        return MarkupCommentRegex().Replace(text, match => new string(' ', match.Length));
    }

    private static bool LooksJavaScriptNavigation(string value)
    {
        return value.TrimStart().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || value.Contains("window.location", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksSecretLike(string value)
    {
        return SecretLikeRegex().IsMatch(value);
    }

    private static bool IsXmlIoException(Exception ex)
    {
        return ex is IOException or UnauthorizedAccessException;
    }

    private static bool IsHandlerInterfaceOrFactory(string value)
    {
        var name = value.Split('.').Last();
        return name is "IHttpHandler" or "IHttpAsyncHandler" or "IHttpHandlerFactory" or "IHttpAsyncHandlerFactory";
    }

    private static bool IsPageMethodAttribute(AttributeSyntax attribute)
    {
        return PageMethodAttributes.Any(name => AttributeNameMatches(attribute, name));
    }

    private static bool AttributeNameMatches(AttributeSyntax attribute, string expectedName)
    {
        var name = attribute.Name.ToString();
        return name.Equals(expectedName, StringComparison.Ordinal)
            || name.Equals(expectedName + "Attribute", StringComparison.Ordinal)
            || name.EndsWith("." + expectedName, StringComparison.Ordinal)
            || name.EndsWith("." + expectedName + "Attribute", StringComparison.Ordinal);
    }

    private static string AttributeShortName(AttributeSyntax attribute)
    {
        var name = attribute.Name.ToString().Split('.').Last();
        return name.EndsWith("Attribute", StringComparison.Ordinal) ? name[..^"Attribute".Length] : name;
    }

    private static CodeFact? FindSemanticType(IReadOnlyDictionary<string, CodeFact[]> semanticTypesByFile, string filePath, string typeName)
    {
        return semanticTypesByFile.TryGetValue(filePath, out var candidates)
            ? candidates
            .Where(fact => fact.TargetSymbol?.EndsWith(typeName, StringComparison.Ordinal) ?? false)
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .FirstOrDefault()
            : null;
    }

    private static CodeFact? FindSemanticMethod(IReadOnlyDictionary<string, CodeFact[]> semanticMethodsByFile, string filePath, string methodName)
    {
        return semanticMethodsByFile.TryGetValue(filePath, out var candidates)
            ? candidates
            .Where(fact => fact.ContractElement?.Equals(methodName, StringComparison.Ordinal) == true
                || fact.TargetSymbol?.Contains("." + methodName + "(", StringComparison.Ordinal) == true)
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .FirstOrDefault()
            : null;
    }

    private static bool IsAsmxOperationMethod(IReadOnlyDictionary<string, CodeFact[]> asmxOperationsByFile, string filePath, string typeName, string methodName, string symbol)
    {
        return asmxOperationsByFile.TryGetValue(filePath, out var candidates)
            && candidates.Any(fact =>
                fact.TargetSymbol?.Equals(symbol, StringComparison.Ordinal) == true
                || (fact.SourceSymbol?.Equals(typeName, StringComparison.Ordinal) == true
                    && fact.ContractElement?.Equals(methodName, StringComparison.Ordinal) == true));
    }

    private static bool ParentNameEquals(XElement element, string name)
    {
        return element.Parent?.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool HasAncestorNamed(XElement element, string name)
    {
        return element.Ancestors().Any(ancestor => ancestor.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static string QualifiedTypeName(TypeDeclarationSyntax type)
    {
        var names = new Stack<string>();
        for (SyntaxNode? current = type; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case TypeDeclarationSyntax currentType:
                    names.Push(currentType.Identifier.ValueText);
                    break;
                case NamespaceDeclarationSyntax namespaceDeclaration:
                    names.Push(namespaceDeclaration.Name.ToString());
                    break;
                case FileScopedNamespaceDeclarationSyntax fileScoped:
                    names.Push(fileScoped.Name.ToString());
                    break;
            }
        }

        return string.Join(".", names);
    }

    private static bool LooksLikeRouteAdd(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression.ToString().Contains("Routes", StringComparison.Ordinal)
            || invocation.ArgumentList.Arguments.Any(argument => argument.Expression.ToString().Contains("Route", StringComparison.Ordinal));
    }

    private static string InvocationName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            _ => invocation.Expression.ToString()
        };
    }

    private static string? StringLiteral(ExpressionSyntax expression)
    {
        return expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
            _ => null
        };
    }

    private static SortedDictionary<string, string> ParseAttributes(string text)
    {
        var attributes = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex().Matches(text).Cast<Match>())
        {
            var value = match.Groups["dq"].Success ? match.Groups["dq"].Value : match.Groups["sq"].Value;
            attributes[match.Groups["name"].Value] = value;
        }

        return attributes;
    }

    private static string AttributeValue(XElement element, string name)
    {
        return element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
    }

    private static int LineNumber(XObject node)
    {
        return node is IXmlLineInfo lineInfo && lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;
    }

    private static int SpanLine(SyntaxTree tree, SyntaxNode node)
    {
        return tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
    }

    private static int EndLine(SyntaxTree tree, SyntaxNode node)
    {
        return Math.Max(SpanLine(tree, node), tree.GetLineSpan(node.Span).EndLinePosition.Line + 1);
    }

    private static int LineAt(SourceText source, int position)
    {
        return source.Lines.GetLineFromPosition(position).LineNumber + 1;
    }

    private static bool TryRead(string repoPath, string relativePath, out string text)
    {
        try
        {
            text = File.ReadAllText(Path.Combine(repoPath, relativePath));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            text = string.Empty;
            return false;
        }
    }

    private static string WeakestTier(params string[] tiers)
    {
        return tiers.Contains(EvidenceTiers.Tier4Unknown) ? EvidenceTiers.Tier4Unknown
            : tiers.Contains(EvidenceTiers.Tier3SyntaxOrTextual) ? EvidenceTiers.Tier3SyntaxOrTextual
            : tiers.Contains(EvidenceTiers.Tier2Structural) ? EvidenceTiers.Tier2Structural
            : EvidenceTiers.Tier1Semantic;
    }

    private static string Dash(string value)
    {
        return string.Concat(value.Select(ch => char.IsUpper(ch) ? "-" + char.ToLowerInvariant(ch) : ch.ToString())).TrimStart('-');
    }

    [GeneratedRegex(@"<%@\s*Application\b(?<attrs>.*?)%>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ApplicationDirectiveRegex();

    [GeneratedRegex(@"<%@\s*WebHandler\b(?<attrs>.*?)%>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HandlerDirectiveRegex();

    [GeneratedRegex(@"<(?<name>[A-Za-z][\w:.-]*)\b(?<attrs>[^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex MarkupTagRegex();

    [GeneratedRegex(@"<!--.*?-->|<%--.*?--%>", RegexOptions.Singleline)]
    private static partial Regex MarkupCommentRegex();

    [GeneratedRegex(@"(?<name>[A-Za-z_:][\w:.-]*)\s*=\s*(?:""(?<dq>[^""]*)""|'(?<sq>[^']*)')", RegexOptions.Singleline)]
    private static partial Regex AttributeRegex();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.-]{0,127}$")]
    private static partial Regex SafeIdentifierRegex();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.+`]{0,255}$")]
    private static partial Regex SafeCodeNameRegex();

    [GeneratedRegex(@"(?:Authentication|Authorization|Identity|Principal|Role|Session|Profile)Module$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IdentityPipelineNameRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_./*{}-]{1,180}$")]
    private static partial Regex SafeRelativePathRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_.-]+(?:/[A-Za-z0-9_.-]+)?$")]
    private static partial Regex SafeConfigLocationPathRegex();

    [GeneratedRegex(@"(?i)(password|passwd|pwd|secret|token|api[_-]?key|connectionstring|machinekey|private[_-]?key|client[_-]?secret)")]
    private static partial Regex SecretLikeRegex();
}
