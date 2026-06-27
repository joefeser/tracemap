using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class MessageSurfaceTests
{
    [Fact]
    public void Scan_extracts_static_message_publish_consume_binding_and_gaps_without_private_values()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Messaging.cs"), """
            public sealed class Messaging
            {
                private const string OrdersTopic = "orders.events";

                public void Publish(dynamic kafkaProducer, dynamic channel, dynamic daprClient, dynamic serviceBusSender, string tenantTopic, object message)
                {
                    kafkaProducer.ProduceAsync(OrdersTopic, message);
                    channel.QueueDeclare("orders-work");
                    channel.Bind("orders-bound", "orders-exchange");
                    channel.BasicPublish("orders-exchange", "created", null, message);
                    channel.BasicPublish("", "orders-default", null, message);
                    daprClient.PublishEventAsync("pubsub", "orders.created", message);
                    serviceBusSender.SendAsync("orders-sent", message);
                    kafkaProducer.ProduceAsync(tenantTopic, message);
                    _mediator.Publish(message);
                    AddOptions<Messaging>().Bind("orders-options");
                }

                [QueueTrigger("orders-work")]
                public void Handle(string payload)
                {
                }

                [ServiceBusTrigger("orders-servicebus-queue")]
                public void HandleServiceBusQueue(string payload)
                {
                }

                [ServiceBusTrigger("orders-named-queue", Connection = "ServiceBusConnection")]
                public void HandleNamedServiceBusQueue(string payload)
                {
                }

                [ServiceBusTrigger("orders-servicebus-topic", "orders-subscription")]
                public void HandleServiceBusTopic(string payload)
                {
                }

                [ServiceBusTrigger("unsafe://private-host/orders")]
                public void Unsafe(string payload)
                {
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.MessagePublisherSurface
            && fact.RuleId == RuleIds.MessageSurfacePublish
            && fact.Properties.GetValueOrDefault("normalizedDestinationKey") == "orders.events"
            && fact.Properties.GetValueOrDefault("surfaceKind") == "message-stream");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.MessagePublisherSurface
            && fact.Properties.GetValueOrDefault("normalizedDestinationKey") == "orders.created"
            && fact.Properties.GetValueOrDefault("frameworkFamily") == "dapr");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.MessageBindingDeclared
            && fact.RuleId == RuleIds.MessageSurfaceBinding
            && fact.Properties.GetValueOrDefault("normalizedDestinationKey") == "orders-work");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.MessageBindingDeclared
            && fact.RuleId == RuleIds.MessageSurfaceBinding
            && fact.Properties.GetValueOrDefault("normalizedDestinationKey") == "orders-bound");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.MessageBindingDeclared
            && fact.RuleId == RuleIds.MessageSurfaceBinding
            && fact.Properties.GetValueOrDefault("normalizedDestinationKey") == "orders-options");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.MessagePublisherSurface
            && fact.Properties.GetValueOrDefault("frameworkFeature") == "basic-publish-default-exchange"
            && fact.Properties.GetValueOrDefault("surfaceKind") == "message-queue"
            && fact.Properties.GetValueOrDefault("normalizedDestinationKey") == "orders-default");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.MessagePublisherSurface
            && fact.Properties.GetValueOrDefault("operationKind") == "send"
            && fact.Properties.GetValueOrDefault("normalizedDestinationKey") == "orders-sent");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.MessageConsumerSurface
            && fact.RuleId == RuleIds.MessageSurfaceConsume
            && fact.Properties.GetValueOrDefault("normalizedDestinationKey") == "orders-work");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.MessageConsumerSurface
            && fact.Properties.GetValueOrDefault("normalizedDestinationKey") == "orders-servicebus-queue"
            && fact.Properties.GetValueOrDefault("surfaceKind") == "message-queue");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.MessageConsumerSurface
            && fact.Properties.GetValueOrDefault("normalizedDestinationKey") == "orders-named-queue"
            && fact.Properties.GetValueOrDefault("surfaceKind") == "message-queue");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.MessageConsumerSurface
            && fact.Properties.GetValueOrDefault("normalizedDestinationKey") == "orders-servicebus-topic"
            && fact.Properties.GetValueOrDefault("surfaceKind") == "message-topic");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.MessageSurfaceGap
            && fact.Properties.GetValueOrDefault("gapReason") == "dynamic-destination");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.MessageConsumerSurface
            && fact.Properties.GetValueOrDefault("destinationIdentityStatus") == "hashed"
            && fact.Properties.GetValueOrDefault("destinationHash")?.Length == 64);
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType is FactTypes.MessagePublisherSurface or FactTypes.MessageConsumerSurface
            && fact.Properties.Values.Any(value => value.Contains("unsafe://private-host", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.MessagePublisherSurface
            && fact.Evidence.StartLine.ToString() == "12"
            && fact.Properties.GetValueOrDefault("frameworkFamily")?.Contains("mediator", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void Qualified_const_destination_names_use_qualified_identity()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Messaging.cs"), """
            public static class FirstDestinations
            {
                public const string Destination = "orders.first";
            }

            public static class SecondDestinations
            {
                public const string Destination = "orders.second";
            }

            public sealed class Publisher
            {
                public void Publish(dynamic kafkaProducer, object message)
                {
                    kafkaProducer.ProduceAsync(FirstDestinations.Destination, message);
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.MessagePublisherSurface
            && fact.Properties.GetValueOrDefault("normalizedDestinationKey") == "orders.first");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.MessagePublisherSurface
            && fact.Properties.GetValueOrDefault("normalizedDestinationKey") == "orders.second");
    }

    [Fact]
    public void Ambiguous_unqualified_const_destination_names_emit_gap_instead_of_wrong_static_surface()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Messaging.cs"), """
            public sealed class Publisher
            {
                private const string Destination = "orders.first";

                public void Publish(dynamic kafkaProducer, object message)
                {
                    kafkaProducer.ProduceAsync(Destination, message);
                }
            }

            public sealed class OtherPublisher
            {
                private const string Destination = "orders.second";
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.MessagePublisherSurface
            && fact.Properties.GetValueOrDefault("normalizedDestinationKey") is "orders.first" or "orders.second");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.MessageSurfaceGap
            && fact.Properties.GetValueOrDefault("gapReason") == "dynamic-destination");
    }

    [Fact]
    public void Stable_message_surface_key_does_not_change_when_handler_name_changes()
    {
        using var first = new TempDirectory();
        using var second = new TempDirectory();
        var firstRepo = Path.Combine(first.Path, "synthetic");
        var secondRepo = Path.Combine(second.Path, "synthetic");
        Directory.CreateDirectory(firstRepo);
        Directory.CreateDirectory(secondRepo);
        File.WriteAllText(Path.Combine(firstRepo, "Function.cs"), """
            public sealed class Worker
            {
                [QueueTrigger("orders-work")]
                public void Handle(string payload) { }
            }
            """);
        File.WriteAllText(Path.Combine(secondRepo, "Function.cs"), """
            public sealed class Worker
            {
                [QueueTrigger("orders-work")]
                public void Renamed(string payload) { }
            }
            """);

        var firstFact = ScanEngine.Scan(new ScanOptions(firstRepo, Path.Combine(firstRepo, ".tracemap")))
            .Facts.Single(fact => fact.FactType == FactTypes.MessageConsumerSurface);
        var secondFact = ScanEngine.Scan(new ScanOptions(secondRepo, Path.Combine(secondRepo, ".tracemap")))
            .Facts.Single(fact => fact.FactType == FactTypes.MessageConsumerSurface);

        Assert.Equal(firstFact.Properties["stableMessageSurfaceKey"], secondFact.Properties["stableMessageSurfaceKey"]);
        Assert.NotEqual(firstFact.Properties["handlerSymbolId"], secondFact.Properties["handlerSymbolId"]);
    }

    [Fact]
    public async Task Combined_report_projects_message_surfaces_and_static_candidate_edges()
    {
        using var temp = new TempDirectory();
        var publisherRepo = Path.Combine(temp.Path, "publisher");
        var consumerRepo = Path.Combine(temp.Path, "consumer");
        Directory.CreateDirectory(publisherRepo);
        Directory.CreateDirectory(consumerRepo);
        File.WriteAllText(Path.Combine(publisherRepo, "Publisher.cs"), """
            public sealed class Publisher
            {
                public void Publish(dynamic kafkaProducer, object message) => kafkaProducer.ProduceAsync("orders.events", message);
                public void Configure(dynamic channel) => channel.QueueDeclare("orders-work");
            }
            """);
        File.WriteAllText(Path.Combine(consumerRepo, "Consumer.cs"), """
            public sealed class Consumer
            {
                public void Consume(dynamic kafkaConsumer) => kafkaConsumer.Consume("orders.events");
            }
            """);

        var publisherScan = ScanEngine.Scan(new ScanOptions(publisherRepo, Path.Combine(publisherRepo, ".tracemap")));
        var consumerScan = ScanEngine.Scan(new ScanOptions(consumerRepo, Path.Combine(consumerRepo, ".tracemap")));
        Directory.CreateDirectory(Path.Combine(publisherRepo, ".tracemap"));
        Directory.CreateDirectory(Path.Combine(consumerRepo, ".tracemap"));
        SqliteIndexWriter.Write(Path.Combine(publisherRepo, ".tracemap", "index.sqlite"), publisherScan.Manifest, publisherScan.Facts);
        SqliteIndexWriter.Write(Path.Combine(consumerRepo, ".tracemap", "index.sqlite"), consumerScan.Manifest, consumerScan.Facts);
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions(
            [Path.Combine(publisherRepo, ".tracemap", "index.sqlite"), Path.Combine(consumerRepo, ".tracemap", "index.sqlite")],
            combinedPath,
            ["publisher", "consumer"]));

        var result = await CombinedDependencyReporter.WriteAsync(new CombinedDependencyReportOptions(combinedPath, Path.Combine(temp.Path, "report")));

        Assert.Contains(result.Report.DependencySurfaces, surface =>
            surface.SurfaceKind == "message-stream"
            && surface.OperationDirection == "publish"
            && surface.NormalizedDestinationKey == "orders.events"
            && surface.FrameworkFamily == "kafka");
        Assert.Contains(result.Report.DependencySurfaces, surface =>
            surface.SurfaceKind == "message-stream"
            && surface.OperationDirection == "consume"
            && surface.NormalizedDestinationKey == "orders.events");
        Assert.Contains(result.Report.DependencyEdges, edge =>
            edge.EdgeKind == "message-publish-consume"
            && edge.RuleId == RuleIds.MessageSurfaceCandidateEdge);
        Assert.Equal("hidden", result.Report.MessageReviewContext.ClaimLevel);
        Assert.Equal("partial", result.Report.MessageReviewContext.Status);
        Assert.Equal("ReducedCoverage", result.Report.MessageReviewContext.CoverageLabel);
        Assert.Contains(result.Report.MessageReviewContext.Rows, row =>
            row.ContextKind == "static-destination-candidate"
            && row.RuleId == RuleIds.MessageFlowContext
            && row.EvidenceTier == EvidenceTiers.Tier4Unknown
            && row.SupportingEdgeIds.Count == 1);
        Assert.All(result.Report.MessageReviewContext.Rows, row =>
        {
            Assert.NotEqual("message-publish-consume", row.ContextKind);
            Assert.DoesNotContain("delivery", row.ContextKind, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Empty(result.Report.MessageReviewContext.Gaps);

        var markdown = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report", "dependency-report.md"));
        Assert.Contains("Event/message rows are static evidence only", markdown);
        Assert.Contains("## Message Review Context", markdown);
        Assert.Contains("Claim level: `hidden`", markdown);
        Assert.Contains("message-publish-consume", markdown);
        Assert.Contains("static-destination-candidate", markdown);
        Assert.DoesNotContain("delivered message", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payload compatible", markdown, StringComparison.OrdinalIgnoreCase);

        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report", "dependency-report.json"));
        Assert.Contains("\"operationDirection\": \"publish\"", json);
        Assert.Contains("\"messageReviewContext\"", json);
        Assert.Contains("\"claimLevel\": \"hidden\"", json);
        Assert.Contains("\"ruleId\": \"message.flow.context.v1\"", json);
        Assert.Contains("Event/message rows are static evidence only", json);
        Assert.DoesNotContain(temp.Path, json, StringComparison.OrdinalIgnoreCase);

        var catalog = await File.ReadAllTextAsync(Path.Combine(FindRepositoryRoot(), "rules", "rule-catalog.yml"));
        Assert.Contains(RuleIds.MessageFlowContext, catalog);
        Assert.Contains(RuleIds.MessageFlowGap, catalog);
    }

    [Fact]
    public async Task Paths_and_reverse_filter_message_surfaces_by_direction_and_reject_candidate_edge_kind()
    {
        using var temp = new TempDirectory();
        var publisherRepo = Path.Combine(temp.Path, "publisher");
        var consumerRepo = Path.Combine(temp.Path, "consumer");
        Directory.CreateDirectory(publisherRepo);
        Directory.CreateDirectory(consumerRepo);
        File.WriteAllText(Path.Combine(publisherRepo, "Publisher.cs"), """
            public sealed class Publisher
            {
                public void Publish(dynamic kafkaProducer, object message) => kafkaProducer.ProduceAsync("orders.events", message);
                public void Configure(dynamic channel) => channel.QueueDeclare("orders-work");
            }
            """);
        File.WriteAllText(Path.Combine(consumerRepo, "Consumer.cs"), """
            public sealed class Consumer
            {
                public void Consume(dynamic kafkaConsumer) => kafkaConsumer.Consume("orders.events");
            }
            """);

        var publisherScan = ScanEngine.Scan(new ScanOptions(publisherRepo, Path.Combine(publisherRepo, ".tracemap")));
        var consumerScan = ScanEngine.Scan(new ScanOptions(consumerRepo, Path.Combine(consumerRepo, ".tracemap")));
        Directory.CreateDirectory(Path.Combine(publisherRepo, ".tracemap"));
        Directory.CreateDirectory(Path.Combine(consumerRepo, ".tracemap"));
        SqliteIndexWriter.Write(Path.Combine(publisherRepo, ".tracemap", "index.sqlite"), publisherScan.Manifest, publisherScan.Facts);
        SqliteIndexWriter.Write(Path.Combine(consumerRepo, ".tracemap", "index.sqlite"), consumerScan.Manifest, consumerScan.Facts);
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions(
            [Path.Combine(publisherRepo, ".tracemap", "index.sqlite"), Path.Combine(consumerRepo, ".tracemap", "index.sqlite")],
            combinedPath,
            ["publisher", "consumer"]));

        var paths = await CombinedDependencyPathReporter.WriteAsync(new CombinedDependencyPathOptions(
            combinedPath,
            Path.Combine(temp.Path, "paths"),
            FromSource: "publisher",
            ToSurface: "message-stream",
            MessageDirection: "publish"));
        Assert.Equal("publish", paths.Report.Query.MessageDirection);
        Assert.Contains(paths.Report.Inventory.SurfacesByKind, pair => pair.Key == "message-stream");
        Assert.DoesNotContain(paths.Report.Gaps, gap =>
            gap.RuleId == RuleIds.MessageSurfaceGap
            && gap.Reason == "direction-filter-not-supported");

        var reverse = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            Path.Combine(temp.Path, "reverse"),
            Surface: "message-stream",
            MessageDirection: "consume",
            To: "sources"));
        Assert.Equal("consume", reverse.Report.Query.MessageDirection);
        Assert.Contains(reverse.Report.SelectedSurfaces, surface =>
            surface.SurfaceKind == "message-stream"
            && surface.Metadata.GetValueOrDefault("operationDirection") == "consume");
        Assert.DoesNotContain(reverse.Report.SelectedSurfaces, surface =>
            surface.SurfaceKind == "message-stream"
            && surface.Metadata.GetValueOrDefault("operationDirection") == "publish");
        Assert.DoesNotContain(reverse.Report.Gaps, gap =>
            gap.RuleId == RuleIds.MessageSurfaceGap
            && gap.Reason == "direction-filter-not-supported"
            && gap.Metadata.GetValueOrDefault("gapReason") == "direction-filter-not-supported");

        var allDirections = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            Path.Combine(temp.Path, "reverse-all"),
            Surface: "message-stream",
            MessageDirection: "all",
            To: "sources"));
        Assert.Null(allDirections.Report.Query.MessageDirection);
        Assert.Contains(allDirections.Report.SelectedSurfaces, surface =>
            surface.Metadata.GetValueOrDefault("operationDirection") == "publish");
        Assert.Contains(allDirections.Report.SelectedSurfaces, surface =>
            surface.Metadata.GetValueOrDefault("operationDirection") == "consume");

        var declare = await CombinedReverseReporter.WriteAsync(new CombinedReverseOptions(
            combinedPath,
            Path.Combine(temp.Path, "reverse-declare"),
            Surface: "message-queue",
            MessageDirection: "declare",
            To: "sources"));
        Assert.Equal("declare", declare.Report.Query.MessageDirection);
        Assert.Contains(declare.Report.SelectedSurfaces, surface =>
            surface.SurfaceKind == "message-queue"
            && surface.Metadata.GetValueOrDefault("operationDirection") == "declare");
        Assert.All(declare.Report.SelectedSurfaces, surface =>
            Assert.Equal("declare", surface.Metadata.GetValueOrDefault("operationDirection")));

        using var output = new StringWriter();
        using var error = new StringWriter();
        var pathExit = await TraceMapCommand.RunAsync(["paths", "--index", combinedPath, "--out", Path.Combine(temp.Path, "bad-paths"), "--to-surface", "message-publish-consume"], output, error);
        Assert.Equal(1, pathExit);
        Assert.Contains("edge kind", error.ToString());

        using var reverseOutput = new StringWriter();
        using var reverseError = new StringWriter();
        var reverseExit = await TraceMapCommand.RunAsync(["reverse", "--index", combinedPath, "--out", Path.Combine(temp.Path, "bad-reverse"), "--surface", "message-publish-consume"], reverseOutput, reverseError);
        Assert.Equal(1, reverseExit);
        Assert.Contains("edge kind", reverseError.ToString());

        using var invalidDirectionOutput = new StringWriter();
        using var invalidDirectionError = new StringWriter();
        var invalidDirectionExit = await TraceMapCommand.RunAsync(["reverse", "--index", combinedPath, "--out", Path.Combine(temp.Path, "bad-direction"), "--surface", "message-stream", "--message-direction", "sideways"], invalidDirectionOutput, invalidDirectionError);
        Assert.Equal(1, invalidDirectionExit);
        Assert.Contains("--message-direction", invalidDirectionError.ToString());
    }

    [Fact]
    public void Message_identity_hashes_unsafe_destinations_with_full_sha256()
    {
        var identity = MessageSurfaceIdentity.FromRaw("unsafe://private-host/orders");

        Assert.Equal("hashed", identity.Status);
        Assert.Null(identity.NormalizedDestinationKey);
        Assert.NotNull(identity.DestinationHash);
        Assert.Equal(64, identity.DestinationHash!.Length);
        Assert.Matches("^[a-f0-9]{64}$", identity.DestinationHash);

        var endpointName = MessageSurfaceIdentity.FromRaw("order-service-endpoint");
        Assert.Equal("static", endpointName.Status);
        Assert.Equal("order-service-endpoint", endpointName.NormalizedDestinationKey);
        Assert.Null(endpointName.DestinationHash);

        var hostLike = MessageSurfaceIdentity.FromRaw("user@host");
        Assert.Equal("hashed", hostLike.Status);
        Assert.Null(hostLike.NormalizedDestinationKey);
        Assert.Equal(64, hostLike.DestinationHash!.Length);

        var caseSensitive = MessageSurfaceIdentity.FromRaw("Orders.Events");
        Assert.Equal("static", caseSensitive.Status);
        Assert.Equal("Orders.Events", caseSensitive.NormalizedDestinationKey);
    }

    [Fact]
    public void Static_invocation_stable_key_does_not_change_when_line_number_changes()
    {
        using var first = new TempDirectory();
        using var second = new TempDirectory();
        var firstRepo = Path.Combine(first.Path, "synthetic");
        var secondRepo = Path.Combine(second.Path, "synthetic");
        Directory.CreateDirectory(firstRepo);
        Directory.CreateDirectory(secondRepo);
        File.WriteAllText(Path.Combine(firstRepo, "Publisher.cs"), """
            public sealed class Publisher
            {
                public void Publish(dynamic kafkaProducer, object message)
                {
                    kafkaProducer.ProduceAsync("orders.events", message);
                }
            }
            """);
        File.WriteAllText(Path.Combine(secondRepo, "Publisher.cs"), """
            public sealed class Publisher
            {

                public void Publish(dynamic kafkaProducer, object message)
                {
                    kafkaProducer.ProduceAsync("orders.events", message);
                }
            }
            """);

        var firstFact = ScanEngine.Scan(new ScanOptions(firstRepo, Path.Combine(firstRepo, ".tracemap")))
            .Facts.Single(fact => fact.FactType == FactTypes.MessagePublisherSurface);
        var secondFact = ScanEngine.Scan(new ScanOptions(secondRepo, Path.Combine(secondRepo, ".tracemap")))
            .Facts.Single(fact => fact.FactType == FactTypes.MessagePublisherSurface);

        Assert.NotEqual(firstFact.Evidence.StartLine, secondFact.Evidence.StartLine);
        Assert.Equal(firstFact.Properties["stableMessageSurfaceKey"], secondFact.Properties["stableMessageSurfaceKey"]);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "rules", "rule-catalog.yml"))
                && Directory.Exists(Path.Combine(directory.FullName, ".kiro")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate TraceMap repository root.");
    }
}
