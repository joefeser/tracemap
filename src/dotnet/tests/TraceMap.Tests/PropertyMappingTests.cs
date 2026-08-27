using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Data.Sqlite;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class PropertyMappingTests
{
    private static readonly HashSet<string> FactPropertySchema = new(StringComparer.Ordinal)
    {
        "containerMethodAssemblyName", "containerMethodAssemblyVersion", "containerMethodContainingSymbolId",
        "containerMethodSymbolId", "containerMethodSymbolKind", "coverageLabel", "direction", "limitations",
        "mappingShape", "sanitization", "sourcePropertyAssemblyName", "sourcePropertyAssemblyVersion",
        "sourcePropertyContainingSymbolId", "sourcePropertySymbolId", "sourcePropertySymbolKind",
        "sourceTypeAssemblyName", "sourceTypeAssemblyVersion", "sourceTypeContainingSymbolId",
        "sourceTypeSymbolId", "sourceTypeSymbolKind", "targetPropertyAssemblyName", "targetPropertyAssemblyVersion",
        "targetPropertyContainingSymbolId", "targetPropertySymbolId", "targetPropertySymbolKind",
        "targetTypeAssemblyName", "targetTypeAssemblyVersion", "targetTypeContainingSymbolId",
        "targetTypeSymbolId", "targetTypeSymbolKind"
    };

    private static readonly HashSet<string> GapPropertySchema = new(StringComparer.Ordinal)
    {
        "coverageEffect", "coverageLabel", "gapKind", "limitations", "occurrenceCount", "sanitization",
        "shapeState", "suppressedFactCount", "suppressedGapCount",
        "scopeAssemblyName", "scopeAssemblyVersion", "scopeContainingSymbolId", "scopeSymbolId", "scopeSymbolKind",
        "sourceEndpointAssemblyName", "sourceEndpointAssemblyVersion", "sourceEndpointContainingSymbolId",
        "sourceEndpointSymbolId", "sourceEndpointSymbolKind",
        "targetEndpointAssemblyName", "targetEndpointAssemblyVersion", "targetEndpointContainingSymbolId",
        "targetEndpointSymbolId", "targetEndpointSymbolKind"
    };

    private const string SupportedFixture = """
        namespace Fixt;

        public sealed class SourceDto
        {
            public string Name { get; set; } = "";
            public int Count { get; set; }
            public string? OptionalName { get; set; }
        }

        public sealed class TargetDto
        {
            public string Name { get; set; } = "";
            public int Count { get; set; }
        }

        public sealed class Mappers
        {
            public void Copy(SourceDto source, TargetDto target)
            {
                target.Name = source.Name;
                target.Count = source.Count;
                source.OptionalName = target.Name;
                target.Name = (((source.Name)));
            }

            public TargetDto Initialize(SourceDto source)
            {
                return new TargetDto
                {
                    Name = source.OptionalName!,
                    Count = (int)source.Count
                };
            }

            public System.Collections.Generic.List<TargetDto> Project(
                System.Collections.Generic.List<SourceDto> items)
            {
                return items.ConvertAll(item => new TargetDto { Count = item.Count });
            }
        }
        """;

    [Fact]
    public void Extractor_emits_exact_facts_for_supported_shapes_with_closed_schema()
    {
        var extraction = ExtractOne(SupportedFixture);
        var facts = extraction.Facts;

        Assert.All(facts, fact =>
        {
            Assert.Equal(FactTypes.PropertyMappingDeclared, fact.FactType);
            Assert.Equal(RuleIds.CSharpSemanticPropertyMapping, fact.RuleId);
            Assert.Equal(EvidenceTiers.Tier1Semantic, fact.EvidenceTier);
            Assert.Equal("bounded-static-property-mapping", Prop(fact)["coverageLabel"]);
            Assert.Equal("source-to-target", Prop(fact)["direction"]);
            Assert.Contains("does not prove mapping execution", Prop(fact)["limitations"], StringComparison.Ordinal);
            Assert.Empty(Prop(fact).Keys.Except(FactPropertySchema, StringComparer.Ordinal));
            Assert.StartsWith("Fixt.", fact.SourceSymbol, StringComparison.Ordinal);
            Assert.StartsWith("Fixt.", fact.TargetSymbol, StringComparison.Ordinal);
            Assert.Equal(fact.ContractElement, Prop(fact)["mappingShape"]);
            Assert.Equal("CSharpPropertyMappingExtractor", fact.Evidence.ExtractorId);
            Assert.Equal("csharp-property-mapping/0.1.0", fact.Evidence.ExtractorVersion);
            Assert.True(Prop(fact).ContainsKey("sourcePropertySymbolId"));
            Assert.True(Prop(fact).ContainsKey("targetPropertySymbolId"));
            Assert.True(Prop(fact).ContainsKey("containerMethodSymbolId"));
            Assert.NotEqual(Prop(fact)["sourcePropertySymbolId"], Prop(fact)["targetPropertySymbolId"]);
        });
        Assert.DoesNotContain(extraction.Gaps, gap =>
            Prop(gap).GetValueOrDefault("gapKind") == "PropertyMappingTruncated");

        // The plain and parenthesized copies share identical resolved endpoints.
        Assert.Equal(2, facts.Count(fact =>
            Prop(fact).GetValueOrDefault("mappingShape") == "assignment"
            && fact.SourceSymbol == "Fixt.SourceDto.Name"
            && fact.TargetSymbol == "Fixt.TargetDto.Name"));

        var reversed = Assert.Single(facts, fact =>
            fact.SourceSymbol == "Fixt.TargetDto.Name"
            && fact.TargetSymbol == "Fixt.SourceDto.OptionalName");
        Assert.Equal("assignment", Prop(reversed)["mappingShape"]);

        Assert.Equal(4, facts.Count(fact => Prop(fact).GetValueOrDefault("mappingShape") == "assignment"));
        Assert.Single(facts, fact =>
            Prop(fact).GetValueOrDefault("mappingShape") == "object-initializer"
            && fact.SourceSymbol == "Fixt.SourceDto.OptionalName");
        Assert.Single(facts, fact =>
            Prop(fact).GetValueOrDefault("mappingShape") == "object-initializer"
            && fact.SourceSymbol == "Fixt.SourceDto.Count");
        Assert.Single(facts, fact => Prop(fact).GetValueOrDefault("mappingShape") == "projection");

        var initializerFact = Assert.Single(facts, fact =>
            fact.SourceSymbol == "Fixt.SourceDto.OptionalName");
        Assert.Contains("Fixt.TargetDto", Prop(initializerFact)["targetTypeSymbolId"], StringComparison.Ordinal);
        Assert.Contains("Fixt.SourceDto", Prop(initializerFact)["sourceTypeSymbolId"], StringComparison.Ordinal);

        var repeat = ExtractOne(SupportedFixture);
        Assert.Equal(
            Materialized(facts).Select(Projection),
            Materialized(repeat.Facts).Select(Projection));
    }

    [Fact]
    public void Extractor_unwraps_only_identity_preserving_syntax()
    {
        const string source = """
            namespace Fixt;

            public sealed class Dto
            {
                public string Name { get; set; } = "";
                public int Number { get; set; }
                public Duo? Child { get; set; }
            }

            public sealed class Duo
            {
                public string Label { get; set; } = "";
                public int Size { get; set; }
            }

            public sealed class Wren
            {
                public string Label { get; set; } = "";
            }

            public static class Store
            {
                public static Duo First { get; set; } = new Duo();
                public static Duo? NullableDuo { get; set; }
                public static Twin TwinSlot { get; set; } = new Twin();
                public static Wren Second { get; set; } = new Wren();
            }

            public sealed class Twin
            {
                public int Size { get; set; }
            }

            public sealed class Runner
            {
                public void Unwrap(Dto source)
                {
                    Store.First.Label = ((string)source.Name)!;
                    Store.TwinSlot.Size = (Store.NullableDuo!.Size);
                    Store.Second.Label = source.Child!.Label;
                    Store.TwinSlot.Size = (int)((long)source.Number);
                }

                public void ConditionalRemainsUnsupported(Duo duo, Dto source)
                {
                    Store.First.Label = duo.Label is null ? source.Name : duo.Label;
                }
            }
            """;

        var extraction = ExtractWithErrors(source);
        var gaps = extraction.Gaps;

        Assert.Equal(3, extraction.Facts.Count);
        Assert.Contains(extraction.Facts, fact =>
            fact.SourceSymbol == "Fixt.Dto.Name"
            && fact.TargetSymbol == "Fixt.Duo.Label");
        Assert.Contains(extraction.Facts, fact =>
            fact.SourceSymbol == "Fixt.Duo.Size"
            && fact.TargetSymbol == "Fixt.Twin.Size");
        Assert.Contains(extraction.Facts, fact =>
            fact.SourceSymbol == "Fixt.Duo.Label"
            && fact.TargetSymbol == "Fixt.Wren.Label");

        // The nested non-identity cast chain stays unsupported.
        Assert.Equal(1, gaps.Count(gap => Prop(gap).GetValueOrDefault("shapeState") == "conversion"));

        // Conditional expressions stay unsupported even when an arm resolves to a property.
        Assert.Equal(1, gaps.Count(gap => Prop(gap).GetValueOrDefault("shapeState") == "conditional-expression"));
    }

    private const string UnsupportedFixture = """
        namespace Fixt;

        public sealed class SourceDto
        {
            public string Name { get; set; } = "";
            public long LongNumber { get; set; }
            public int Count { get; set; }
        }

        public sealed class TargetDto
        {
            public string Name { get; set; } = "";
            public int Count { get; set; }
            public long LongNumber { get; set; }
        }

        public sealed class Bag
        {
            public int this[int index] => index;
        }

        public static class Helper
        {
            public static string Decorate(string input) => input;
        }

        public sealed class Subjects
        {
            public Bag Indexed { get; set; } = new Bag();

            public void Consume(TargetDto target, SourceDto source, int scalar)
            {
                scalar = source.Count;
                target.Name = Helper.Decorate(source.Name);
                target.Name = $"value-{source.Name}";
                target.Count = source.Count + 1;
                target.LongNumber = source.Count;
                target.Count = (int)source.LongNumber;
                target.Count = Indexed[3];
                target.Name = scalar switch { 1 => "one", _ => "rest" };
                target.Name += source.Name;
                target.Name ??= source.Name;
                target.Name = source.Name is null ? "empty" : source.Name;
            }

            public void DynamicAccess(TargetDto target, System.Collections.Generic.Dictionary<string, string> bag, SourceDto source)
            {
                bag["customer"] = source.Name;
                dynamic payload = new object();
                target.Name = payload.CustomerTag;
            }
        }
        """;

    [Fact]
    public void Extractor_fails_closed_for_transforming_dynamic_and_conversion_shapes_without_noise()
    {
        var extraction = ExtractOne(UnsupportedFixture);

        Assert.Empty(extraction.Facts);
        var gaps = extraction.Gaps;

        Assert.All(gaps, gap =>
        {
            Assert.Equal(FactTypes.AnalysisGap, gap.FactType);
            Assert.Equal(RuleIds.CSharpSemanticPropertyMappingGap, gap.RuleId);
            Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
            Assert.Equal(gap.ContractElement, Prop(gap)["gapKind"]);
            Assert.Empty(Prop(gap).Keys.Except(GapPropertySchema, StringComparer.Ordinal));
            Assert.Equal("reduces-direct-property-mapping-coverage", Prop(gap)["coverageEffect"]);
        });

        // Local-to-local and local-to-property plain value copies stay silent.
        if (gaps.Count != 12)
        {
            Assert.Fail("unexpected gap set: " + System.Text.Json.JsonSerializer.Serialize(gaps.Select(GapSummary)));
        }
        var invocation = Assert.Single(gaps, gap => Prop(gap).GetValueOrDefault("shapeState") == "invocation");
        Assert.Equal("PropertyMappingShapeUnsupported", Prop(invocation)["gapKind"]);
        Assert.Contains("Consume", Prop(invocation).GetValueOrDefault("scopeSymbolId"), StringComparison.Ordinal);
        var invocationTarget = Prop(invocation).GetValueOrDefault("targetEndpointSymbolId");
        if (invocationTarget is null || !invocationTarget.Contains("TargetDto", StringComparison.Ordinal))
        {
            Assert.Fail($"invocation gap endpoints: {System.Text.Json.JsonSerializer.Serialize(Prop(invocation))}");
        }
        Assert.Equal(1, gaps.Count(gap => Prop(gap).GetValueOrDefault("shapeState") == "interpolation"));
        Assert.Equal(1, gaps.Count(gap => Prop(gap).GetValueOrDefault("shapeState") == "binary-expression"));
        Assert.Equal(1, gaps.Count(gap => Prop(gap).GetValueOrDefault("shapeState") == "type-conversion-required"));
        Assert.Equal(1, gaps.Count(gap => Prop(gap).GetValueOrDefault("shapeState") == "conversion"));
        Assert.Equal(2, gaps.Count(gap => Prop(gap).GetValueOrDefault("shapeState") == "indexer-element"));
        Assert.Equal(1, gaps.Count(gap => Prop(gap).GetValueOrDefault("shapeState") == "switch-expression"));
        Assert.Equal(1, gaps.Count(gap => Prop(gap).GetValueOrDefault("shapeState") == "conditional-expression"));
        Assert.Equal(2, gaps.Count(gap => Prop(gap).GetValueOrDefault("shapeState") == "compound-assignment"));
        var dynamicGap = Assert.Single(gaps, gap =>
            Prop(gap).GetValueOrDefault("gapKind") == "PropertyMappingShapeUnsupported"
            && Prop(gap).GetValueOrDefault("shapeState") == "dynamic");
        Assert.Contains("DynamicAccess", Prop(dynamicGap).GetValueOrDefault("scopeSymbolId"), StringComparison.Ordinal);
        // Fully resolved non-property counterparts never become unavailable gaps.
        Assert.DoesNotContain(gaps, gap => Prop(gap).GetValueOrDefault("shapeState") == "non-property-symbol");

        // No expression text or argument fragments are retained anywhere.
        var serialized = System.Text.Json.JsonSerializer.Serialize(gaps.Select(gap => Prop(gap)));
        Assert.DoesNotContain("Decorate(source", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("$\"value-", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Extractor_normalizes_unadmitted_expression_states_to_the_closed_vocabulary()
    {
        const string source = """
            namespace Fixt;

            public sealed class SourceDto
            {
                public SourceDto? Child { get; set; }
                public string Name { get; set; } = "";
                public System.Threading.Tasks.Task<string> PendingName { get; set; } =
                    System.Threading.Tasks.Task.FromResult("");
            }

            public sealed class TargetDto
            {
                public string? Name { get; set; }
            }

            public static class Mapper
            {
                public static async System.Threading.Tasks.Task Copy(SourceDto source, TargetDto target)
                {
                    target.Name = source.Child?.Name;
                    target.Name = await source.PendingName;
                }
            }
            """;

        var extraction = ExtractOne(source);

        Assert.Empty(extraction.Facts);
        Assert.Equal(2, extraction.Gaps.Count);
        Assert.All(extraction.Gaps, gap =>
            Assert.Equal("expression-transform", Prop(gap)["shapeState"]));
    }

    [Fact]
    public void Extractor_rejects_record_with_and_invalid_property_writes()
    {
        const string source = """
            namespace Fixt;

            public sealed class SourceDto
            {
                public string Name { get; set; } = "";
            }

            public sealed class ReadOnlyTarget
            {
                public string Name { get; } = "";
            }

            public sealed class PrivateTarget
            {
                public string Name { get; private set; } = "";
            }

            public sealed class PrivateSource
            {
                public string Name { private get; set; } = "";
            }

            public sealed class TargetDto
            {
                public string Name { get; set; } = "";
            }

            public sealed record RecordTarget(string Name);

            public static class Mapper
            {
                public static void Invalid(
                    SourceDto source,
                    PrivateSource privateSource,
                    ReadOnlyTarget readOnly,
                    PrivateTarget privateTarget,
                    TargetDto target)
                {
                    readOnly.Name = source.Name;
                    privateTarget.Name = source.Name;
                    target.Name = privateSource.Name;
                    _ = new RecordTarget("") with { Name = source.Name };
                }
            }
            """;

        var extraction = ExtractWithErrors(source);

        Assert.Empty(extraction.Facts);
        Assert.Equal(4, extraction.Gaps.Count);
        Assert.Equal(3, extraction.Gaps.Count(gap =>
            Prop(gap).GetValueOrDefault("gapKind") == "PropertyMappingSemanticUnavailable"
            && Prop(gap).GetValueOrDefault("shapeState") == "incomplete-binding"));
        Assert.Single(extraction.Gaps, gap =>
            Prop(gap).GetValueOrDefault("gapKind") == "PropertyMappingSemanticUnavailable"
            && Prop(gap).GetValueOrDefault("shapeState") == "expression-transform");
    }

    [Fact]
    public void Extractor_rejects_equal_unresolved_property_types_as_semantically_unavailable()
    {
        const string source = """
            namespace Fixt;

            public sealed class SourceDto
            {
                public MissingContract Name { get; set; }
            }

            public sealed class TargetDto
            {
                public MissingContract Name { get; set; }
            }

            public static class Mapper
            {
                public static void Copy(SourceDto source, TargetDto target)
                {
                    target.Name = source.Name;
                }
            }
            """;

        var extraction = ExtractWithErrors(source);

        Assert.Empty(extraction.Facts);
        var gap = Assert.Single(extraction.Gaps);
        Assert.Equal("PropertyMappingSemanticUnavailable", Prop(gap)["gapKind"]);
        Assert.Equal("incomplete-binding", Prop(gap)["shapeState"]);
    }

    [Fact]
    public void Extractor_flags_ambiguous_property_candidates_as_target_ambiguity()
    {
        const string source = """
            namespace Fixt;

            public interface ILeftContract
            {
                int Value { get; set; }
            }

            public interface IRightContract
            {
                int Value { get; set; }
            }

            public interface IDerivedBoth : ILeftContract, IRightContract
            {
            }

            public sealed class AmbiguousHolder
            {
                public int Slot { get; set; }
            }

            public static class AmbiguousWriter
            {
                public static void Write(AmbiguousHolder holder, IDerivedBoth source)
                {
                    holder.Slot = source.Value;
                }
            }
            """;

        var extraction = ExtractWithErrors(source);
        var gap = Assert.Single(extraction.Gaps);
        Assert.Equal("PropertyMappingTargetAmbiguous", Prop(gap)["gapKind"]);
        Assert.Equal("ambiguous-candidates", Prop(gap)["shapeState"]);
        Assert.Empty(extraction.Facts);
    }

    [Fact]
    public void Extractor_stays_silent_outside_method_containers_and_plain_copies()
    {
        const string source = """
            namespace Fixt;

            public sealed class SourceDto
            {
                public string Name { get; set; } = "";
            }

            public sealed class TargetDto
            {
                public string Name { get; set; } = "";
            }

            public sealed class Holder
            {
                public static SourceDto SharedSource = new SourceDto();
                public TargetDto Initialized = new TargetDto { Name = SharedSource.Name };
                public TargetDto ViaCtor;

                public Holder()
                {
                    ViaCtor = new TargetDto { Name = SharedSource.Name };
                    ViaCtor.Name = SharedSource.Name;
                }

                public void PlainCopies(SourceDto source)
                {
                    var local = source.Name;
                    string other = local;
                    other = local;
                }
            }
            """;

        var extraction = ExtractOne(source);
        Assert.Empty(extraction.Facts);
        Assert.Empty(extraction.Gaps);
    }

    [Fact]
    public void Extractor_enforces_per_method_and_per_document_bounds_with_one_aggregated_gap()
    {
        const string perMethodSource = """
            namespace Fixt;

            public sealed class WideSource
            {
                public string F01 { get; set; } = ""; public string F02 { get; set; } = "";
                public string F03 { get; set; } = ""; public string F04 { get; set; } = "";
                public string F05 { get; set; } = ""; public string F06 { get; set; } = "";
                public string F07 { get; set; } = ""; public string F08 { get; set; } = "";
                public string F09 { get; set; } = ""; public string F10 { get; set; } = "";
                public string F11 { get; set; } = ""; public string F12 { get; set; } = "";
                public string F13 { get; set; } = ""; public string F14 { get; set; } = "";
                public string F15 { get; set; } = ""; public string F16 { get; set; } = "";
                public string F17 { get; set; } = ""; public string F18 { get; set; } = "";
                public string F19 { get; set; } = ""; public string F20 { get; set; } = "";
                public string F21 { get; set; } = ""; public string F22 { get; set; } = "";
                public string F23 { get; set; } = ""; public string F24 { get; set; } = "";
                public string F25 { get; set; } = ""; public string F26 { get; set; } = "";
                public string F27 { get; set; } = ""; public string F28 { get; set; } = "";
                public string F29 { get; set; } = ""; public string F30 { get; set; } = "";
            }

            public sealed class WideTarget
            {
                public string F01 { get; set; } = ""; public string F02 { get; set; } = "";
                public string F03 { get; set; } = ""; public string F04 { get; set; } = "";
                public string F05 { get; set; } = ""; public string F06 { get; set; } = "";
                public string F07 { get; set; } = ""; public string F08 { get; set; } = "";
                public string F09 { get; set; } = ""; public string F10 { get; set; } = "";
                public string F11 { get; set; } = ""; public string F12 { get; set; } = "";
                public string F13 { get; set; } = ""; public string F14 { get; set; } = "";
                public string F15 { get; set; } = ""; public string F16 { get; set; } = "";
                public string F17 { get; set; } = ""; public string F18 { get; set; } = "";
                public string F19 { get; set; } = ""; public string F20 { get; set; } = "";
                public string F21 { get; set; } = ""; public string F22 { get; set; } = "";
                public string F23 { get; set; } = ""; public string F24 { get; set; } = "";
                public string F25 { get; set; } = ""; public string F26 { get; set; } = "";
                public string F27 { get; set; } = ""; public string F28 { get; set; } = "";
                public string F29 { get; set; } = ""; public string F30 { get; set; } = "";
            }

            public sealed class Mapper
            {
                public void First(WideSource source, WideTarget target)
                {
                    target.F01 = source.F01; target.F02 = source.F02; target.F03 = source.F03;
                    target.F04 = source.F04; target.F05 = source.F05; target.F06 = source.F06;
                    target.F07 = source.F07; target.F08 = source.F08; target.F09 = source.F09;
                    target.F10 = source.F10; target.F11 = source.F11; target.F12 = source.F12;
                    target.F13 = source.F13; target.F14 = source.F14; target.F15 = source.F15;
                    target.F16 = source.F16; target.F17 = source.F17; target.F18 = source.F18;
                    target.F19 = source.F19; target.F20 = source.F20; target.F21 = source.F21;
                    target.F22 = source.F22; target.F23 = source.F23; target.F24 = source.F24;
                    target.F25 = source.F25; target.F26 = source.F26; target.F27 = source.F27;
                    target.F28 = source.F28; target.F29 = source.F29; target.F30 = source.F30;
                }

                public void Second(WideSource source, WideTarget target)
                {
                    target.F01 = source.F01;
                }
            }
            """;

        var methodExtraction = ExtractOne(perMethodSource);
        if (methodExtraction.Facts.Count != 26 || methodExtraction.Gaps.Count != 1)
        {
            Assert.Fail($"""
                per-method bound mismatch: facts={methodExtraction.Facts.Count} gaps={methodExtraction.Gaps.Count}
                facts={System.Text.Json.JsonSerializer.Serialize(methodExtraction.Facts.Select(f => FactSummary(f)))}
                gaps={System.Text.Json.JsonSerializer.Serialize(methodExtraction.Gaps.Select(g => GapSummary(g)))}
                """);
        }
        var truncated = Assert.Single(methodExtraction.Gaps);
        Assert.Equal("PropertyMappingTruncated", Prop(truncated)["gapKind"]);
        Assert.Equal("5", Prop(truncated)["suppressedFactCount"]);
        Assert.Equal("5", Prop(truncated)["occurrenceCount"]);
        Assert.Equal("0", Prop(truncated)["suppressedGapCount"]);

        var documentBuilder = new System.Text.StringBuilder();
        documentBuilder.AppendLine("namespace Big;");
        documentBuilder.AppendLine("""
            public sealed class RowSource
            {
                public string A { get; set; } = ""; public string B { get; set; } = ""; public string C { get; set; } = "";
            }

            public sealed class RowTarget
            {
                public string A { get; set; } = ""; public string B { get; set; } = ""; public string C { get; set; } = "";
            }
            """);
        documentBuilder.AppendLine("public static class Bulk");
        documentBuilder.AppendLine("{");
        for (var methodIndex = 0; methodIndex < 20; methodIndex++)
        {
            documentBuilder.AppendLine($"    public static void Map{methodIndex}(RowSource source, RowTarget target)");
            documentBuilder.AppendLine("    {");
            for (var copyIndex = 0; copyIndex < 15; copyIndex++)
            {
                documentBuilder.AppendLine(copyIndex % 2 == 0
                    ? "        target.A = source.A;"
                    : copyIndex % 3 == 0 ? "        target.B = source.B;" : "        target.C = source.C;");
            }
            documentBuilder.AppendLine("    }");
        }
        documentBuilder.AppendLine("}");

        var documentExtraction = ExtractOne(documentBuilder.ToString());
        Assert.Equal(250, documentExtraction.Facts.Count);
        var documentTruncated = Assert.Single(documentExtraction.Gaps);
        Assert.Equal("50", Prop(documentTruncated)["suppressedFactCount"]);
    }

    [Fact]
    public void Extractor_keeps_the_truncation_summary_inside_the_gap_bound()
    {
        var source = new System.Text.StringBuilder("""
            namespace Fixt;
            public sealed class SourceDto { public string Name { get; set; } = ""; }
            public sealed class TargetDto { public string Name { get; set; } = ""; }
            public static class Helper { public static string Copy(string value) => value; }
            public static class Mapper
            {
                public static void Copy(SourceDto source, TargetDto target)
                {
            """);
        for (var index = 0; index < 101; index++)
        {
            source.AppendLine("        target.Name = Helper.Copy(source.Name);");
        }
        source.AppendLine("    }");
        source.AppendLine("}");

        var extraction = ExtractOne(source.ToString());

        Assert.Empty(extraction.Facts);
        Assert.Equal(100, extraction.Gaps.Count);
        var truncation = Assert.Single(extraction.Gaps, gap =>
            Prop(gap).GetValueOrDefault("gapKind") == "PropertyMappingTruncated");
        Assert.Equal("truncation", Prop(truncation)["shapeState"]);
        Assert.Equal("2", Prop(truncation)["suppressedGapCount"]);
        Assert.Equal("2", Prop(truncation)["occurrenceCount"]);
    }

    [Fact]
    public void Extractor_resolves_shadowed_names_through_declared_symbols_not_labels()
    {
        const string source = """
            namespace Fixt;

            public sealed class AlphaSource
            {
                public string Name { get; set; } = "";
            }

            public sealed class BetaSource
            {
                public string Name { get; set; } = "";
            }

            public sealed class TargetDto
            {
                public string Name { get; set; } = "";
            }

            public sealed class Worker
            {
                public AlphaSource FieldSource = new AlphaSource();

                public void CopyField(TargetDto target)
                {
                    target.Name = FieldSource.Name;
                }

                public void LocalShadowsField(TargetDto target)
                {
                    var FieldSource = new BetaSource();
                    target.Name = FieldSource.Name;
                }

                public void SameLabelOtherParam(TargetDto target, BetaSource source)
                {
                    target.Name = source.Name;
                }
            }
            """;

        var extraction = ExtractOne(source);
        Assert.Equal(3, extraction.Facts.Count);
        Assert.Empty(extraction.Gaps);

        var fieldFact = Assert.Single(extraction.Facts, fact =>
            Prop(fact).GetValueOrDefault("sourceTypeSymbolId")?.Contains("AlphaSource", StringComparison.Ordinal) == true);

        // The shadowing local and the same-label parameter both declare BetaSource.Name;
        // identical display labels keep distinct canonical identities and containers.
        var betaFacts = extraction.Facts.Where(fact =>
            Prop(fact).GetValueOrDefault("sourceTypeSymbolId")?.Contains("BetaSource", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(2, betaFacts.Length);
        Assert.Equal(2, betaFacts.Select(fact => Prop(fact)["containerMethodSymbolId"]).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("TargetDto", betaFacts[0].TargetSymbol, StringComparison.Ordinal);
        _ = fieldFact;
    }

    [Fact]
    public void Mapping_rules_are_versioned_and_catalogued_with_limitations()
    {
        Assert.Equal("csharp.semantic.propertymapping.v1", RuleIds.CSharpSemanticPropertyMapping);
        Assert.Equal("csharp.semantic.propertymapping-gap.v1", RuleIds.CSharpSemanticPropertyMappingGap);
        Assert.Equal("csharp-property-mapping/0.1.0", ScannerVersions.CSharpPropertyMappingExtractor);
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        Assert.Contains($"id: {RuleIds.CSharpSemanticPropertyMapping}", catalog, StringComparison.Ordinal);
        Assert.Contains($"id: {RuleIds.CSharpSemanticPropertyMappingGap}", catalog, StringComparison.Ordinal);
        Assert.Contains("- PropertyMappingDeclared", catalog, StringComparison.Ordinal);
        Assert.Contains("does not prove that any mapping executed", catalog, StringComparison.Ordinal);
        Assert.Contains("excluded from the legacy name-based property-flow reporter", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scan_emits_deterministic_mapping_evidence_and_round_trips_generic_storage()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "obj"));
        File.WriteAllText(Path.Combine(temp.Path, "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "OrderMaps.cs"), """
            namespace Sample;

            public sealed class OrderEntity
            {
                public string ExternalId { get; set; } = "";
                public string BuyerName { get; set; } = "";
            }

            public sealed class OrderView
            {
                public string Id { get; set; } = "";
                public string Customer { get; set; } = "";
            }

            public static class ViewMappers
            {
                public static OrderView From(OrderEntity entity, OrderView view)
                {
                    view.Id = entity.ExternalId;
                    view.Customer = entity.BuyerName;
                    view.Id = ((entity.ExternalId));
                    return view;
                }

                public static OrderView Projection(OrderEntity entity)
                {
                    return new OrderView
                    {
                        Id = entity.ExternalId,
                        Customer = entity.BuyerName
                    };
                }
            }
            """);
        File.WriteAllText(Path.Combine(temp.Path, "obj", "GeneratedMapper.cs"), """
            namespace Generated;
            public sealed class Pair { public string Left { get; set; } = ""; public string Right { get; set; } = ""; }
            public static class GenMapper
            {
                public static void Copy(Pair source, Pair target) => target.Right = source.Left;
            }
            """);

        var first = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap-first")));
        var second = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap-second")));

        var mappings = first.Facts
            .Where(fact => fact.RuleId == RuleIds.CSharpSemanticPropertyMapping)
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(5, mappings.Length);
        Assert.All(mappings, fact =>
        {
            Assert.Equal(FactTypes.PropertyMappingDeclared, fact.FactType);
            Assert.Equal(EvidenceTiers.Tier1Semantic, fact.EvidenceTier);
            Assert.EndsWith("OrderMaps.cs", fact.Evidence.FilePath, StringComparison.Ordinal);
            Assert.True(fact.Evidence.StartLine >= 15);
        });
        Assert.DoesNotContain(first.Facts, fact =>
            fact.RuleId == RuleIds.CSharpSemanticPropertyMapping
            && fact.Evidence.FilePath.StartsWith("obj/", StringComparison.Ordinal));
        Assert.Equal("Succeeded", first.Manifest.BuildStatus);

        var assignmentShapes = mappings.Where(fact =>
                Prop(fact).GetValueOrDefault("mappingShape") == "assignment")
            .ToArray();
        Assert.Equal(3, assignmentShapes.Length);
        Assert.Equal(2, mappings.Count(fact => Prop(fact).GetValueOrDefault("mappingShape") == "object-initializer"));

        Assert.Equal(
            mappings.Select(Projection).ToArray(),
            second.Facts
                .Where(fact => fact.RuleId == RuleIds.CSharpSemanticPropertyMapping)
                .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
                .Select(Projection)
                .ToArray());

        var artifacts = Path.Combine(temp.Path, "artifacts");
        Directory.CreateDirectory(artifacts);
        var factsPath = Path.Combine(artifacts, "facts.ndjson");
        var indexPath = Path.Combine(artifacts, "index.sqlite");
        await JsonlFactWriter.WriteAsync(factsPath, first.Facts);
        SqliteIndexWriter.Write(indexPath, first.Manifest, first.Facts);
        Assert.Contains(RuleIds.CSharpSemanticPropertyMapping, File.ReadAllText(factsPath), StringComparison.Ordinal);
        using var connection = new SqliteConnection($"Data Source={indexPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from facts where rule_id = $rule;";
        command.Parameters.AddWithValue("$rule", RuleIds.CSharpSemanticPropertyMapping);
        Assert.Equal(mappings.Length, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public void Scan_keeps_same_named_properties_from_distinct_project_assemblies_separate()
    {
        using var temp = new TempDirectory();
        var source = Path.Combine(temp.Path, "src");
        var contractsA = Path.Combine(source, "ContractsA");
        var contractsB = Path.Combine(source, "ContractsB");
        var web = Path.Combine(source, "Web");
        Directory.CreateDirectory(contractsA);
        Directory.CreateDirectory(contractsB);
        Directory.CreateDirectory(web);
        WriteContractProject(contractsA, "ContractsA");
        WriteContractProject(contractsB, "ContractsB");
        File.WriteAllText(Path.Combine(web, "Web.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../ContractsA/ContractsA.csproj"><Aliases>A</Aliases></ProjectReference>
                <ProjectReference Include="../ContractsB/ContractsB.csproj"><Aliases>B</Aliases></ProjectReference>
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(web, "Mappers.cs"), """
            extern alias A;
            extern alias B;

            namespace Web;

            public sealed class CopyFromA
            {
                public void Map(A::Shared.Contract source, A::Shared.ViewModel target)
                {
                    target.Name = source.Name;
                }
            }

            public sealed class CopyFromB
            {
                public void Map(B::Shared.Contract source, B::Shared.ViewModel target)
                {
                    target.Name = source.Name;
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));
        var mappings = result.Facts
            .Where(fact => fact.RuleId == RuleIds.CSharpSemanticPropertyMapping)
            .OrderBy(fact => Prop(fact)["targetTypeAssemblyName"], StringComparer.Ordinal)
            .ToArray();

        if (mappings.Length != 2)
        {
            Assert.Fail($"""
                expected two cross-assembly mappings, got {mappings.Length}
                mappingFacts={System.Text.Json.JsonSerializer.Serialize(result.Facts.Where(f => f.RuleId == RuleIds.CSharpSemanticPropertyMapping).Select(f => FactSummary(f)))}
                mappingGaps={System.Text.Json.JsonSerializer.Serialize(result.Facts.Where(f => f.RuleId == RuleIds.CSharpSemanticPropertyMappingGap).Select(g => GapSummary(g)))}
                buildStatus={result.Manifest.BuildStatus} analysisLevel={result.Manifest.AnalysisLevel}
                projects={System.Text.Json.JsonSerializer.Serialize(result.Manifest.Projects)}
                capabilities={System.Text.Json.JsonSerializer.Serialize(result.Facts.Where(f => f.FactType == FactTypes.AnalyzerCapabilityDiagnostic).Select(g => GapSummary(g)))}
                knownGaps={System.Text.Json.JsonSerializer.Serialize(result.Manifest.KnownGaps)}
                """);
        }

        Assert.Equal(2, mappings.Length);
        Assert.Equal(["ContractsA", "ContractsB"], mappings.Select(fact => Prop(fact)["targetTypeAssemblyName"]));
        Assert.Equal(["ContractsA", "ContractsB"], mappings.Select(fact => Prop(fact)["sourceTypeAssemblyName"]));
        Assert.Equal(2, mappings.Select(fact => Prop(fact)["targetPropertySymbolId"]).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, mappings.Select(fact => Prop(fact)["sourcePropertySymbolId"]).Distinct(StringComparer.Ordinal).Count());
        Assert.All(mappings, fact => Assert.StartsWith("Shared.Contract", fact.SourceSymbol, StringComparison.Ordinal));
        Assert.All(mappings, fact => Assert.StartsWith("Shared.ViewModel", fact.TargetSymbol, StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_keeps_partial_compilation_evidence_deterministic_and_truthfully_labeled()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Healthy.cs"), """
            namespace Sample;

            public sealed class Metric
            {
                public string Key { get; set; } = "";
                public string Summary { get; set; } = "";
            }

            public sealed class SnapshotRow
            {
                public string Identifier { get; set; } = "";
                public string Display { get; set; } = "";
            }

            public static class Compose
            {
                public static void Fill(SnapshotRow row, Metric metric)
                {
                    row.Identifier = metric.Key;
                    row.Display = metric.Summary;
                }
            }
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Broken.cs"), "namespace Sample;\npublic sealed class Nope\n{\n    public this-is-not-valid-csharp();\n");

        var first = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap-first")));
        var second = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap-second")));

        var firstMappings = first.Facts
            .Where(fact => fact.RuleId == RuleIds.CSharpSemanticPropertyMapping)
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        var secondMappings = second.Facts
            .Where(fact => fact.RuleId == RuleIds.CSharpSemanticPropertyMapping)
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(2, firstMappings.Length);
        Assert.All(firstMappings, fact =>
        {
            Assert.EndsWith("Healthy.cs", fact.Evidence.FilePath);
            Assert.True(fact.Evidence.StartLine >= 15);
        });

        Assert.Equal(
            firstMappings.Select(Projection).ToArray(),
            secondMappings.Select(Projection).ToArray());

        Assert.Equal("FailedOrPartial", first.Manifest.BuildStatus);
    }

    private static System.Collections.Generic.IReadOnlyDictionary<string, string> Prop(CodeFact fact) =>
        fact.Properties ?? new SortedDictionary<string, string>(StringComparer.Ordinal);

    private static System.Collections.Generic.IReadOnlyDictionary<string, string> Prop(SemanticFactCandidate candidate) =>
        candidate.Properties ?? new SortedDictionary<string, string>(StringComparer.Ordinal);

    private static IReadOnlyList<CodeFact> Materialized(IReadOnlyList<SemanticFactCandidate> facts) =>
        CSharpSemanticExtractor.MaterializeFacts(Manifest(), facts);

    private static IReadOnlyList<string> Projection(CodeFact fact) =>
    [
        fact.FactId,
        fact.FactType,
        fact.RuleId,
        fact.Evidence.FilePath,
        fact.Evidence.StartLine.ToString(System.Globalization.CultureInfo.InvariantCulture),
        fact.SourceSymbol ?? string.Empty,
        fact.TargetSymbol ?? string.Empty,
        .. fact.Properties.Select(pair => $"{pair.Key}={pair.Value}"),
    ];

    private static string FactSummary(SemanticFactCandidate fact) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            SourceSymbol = fact.SourceSymbol,
            TargetSymbol = fact.TargetSymbol,
            Contract = fact.ContractElement,
            Shape = Prop(fact).GetValueOrDefault("mappingShape"),
            File = fact.Evidence.FilePath,
            Line = fact.Evidence.StartLine
        });

    private static string GapSummary(SemanticFactCandidate gap) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            Kind = Prop(gap).GetValueOrDefault("gapKind"),
            State = Prop(gap).GetValueOrDefault("shapeState"),
            Scope = Prop(gap).GetValueOrDefault("scopeSymbolId"),
            SourceEndpoint = Prop(gap).GetValueOrDefault("sourceEndpointSymbolId"),
            TargetEndpoint = Prop(gap).GetValueOrDefault("targetEndpointSymbolId"),
            File = gap.Evidence.FilePath,
            Line = gap.Evidence.StartLine
        });

    private static string FactSummary(CodeFact fact) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            fact.SourceSymbol,
            fact.TargetSymbol,
            Contract = fact.ContractElement,
            Shape = Prop(fact).GetValueOrDefault("mappingShape"),
            File = fact.Evidence.FilePath,
            Line = fact.Evidence.StartLine
        });

    private static string GapSummary(CodeFact gap) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            Kind = Prop(gap).GetValueOrDefault("gapKind"),
            State = Prop(gap).GetValueOrDefault("shapeState"),
            Scope = Prop(gap).GetValueOrDefault("scopeSymbolId"),
            SourceEndpoint = Prop(gap).GetValueOrDefault("sourceEndpointSymbolId"),
            TargetEndpoint = Prop(gap).GetValueOrDefault("targetEndpointSymbolId"),
            File = gap.Evidence.FilePath,
            Line = gap.Evidence.StartLine
        });

    private static ScanManifest Manifest() => new(
        "scan-property-mapping-fixture",
        "sample",
        null,
        "main",
        "abc123",
        ScannerVersions.TraceMap,
        DateTimeOffset.Parse("2026-08-12T00:00:00Z"),
        "Level1SemanticAnalysis",
        "Succeeded",
        [],
        [],
        ["net10.0"],
        []);

    private static ExtractionResult ExtractOne(string source) => ExtractInternal(source, allowErrors: false);
    private static ExtractionResult ExtractWithErrors(string source) => ExtractInternal(source, allowErrors: true);

    private static ExtractionResult ExtractInternal(string source, bool allowErrors)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "Fixture.cs");
        var referenceList = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToList();
        var references = referenceList.ToArray();
        var compilation = CSharpCompilation.Create(
            "Fixture",
            [tree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (!allowErrors)
        {
            Assert.Empty(errors);
        }

        var facts = new List<SemanticFactCandidate>();
        var gaps = new List<SemanticFactCandidate>();
        PropertyMappingExtractor.Extract(
            projectPath: null,
            filePath: "Fixture.cs",
            root: tree.GetRoot(),
            model: compilation.GetSemanticModel(tree, ignoreAccessibility: true),
            facts,
            gaps);
        return new ExtractionResult(facts, gaps);
    }

    private sealed record ExtractionResult(
        IReadOnlyList<SemanticFactCandidate> Facts,
        IReadOnlyList<SemanticFactCandidate> Gaps);

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "rules", "rule-catalog.yml")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static void WriteContractProject(string directory, string assemblyName)
    {
        File.WriteAllText(Path.Combine(directory, $"{assemblyName}.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><AssemblyName>{assemblyName}</AssemblyName></PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(directory, "Shared.cs"), """
            namespace Shared;

            public sealed class Contract
            {
                public string Name { get; set; } = "";
            }

            public sealed class ViewModel
            {
                public string Name { get; set; } = "";
            }
            """);
    }
}
