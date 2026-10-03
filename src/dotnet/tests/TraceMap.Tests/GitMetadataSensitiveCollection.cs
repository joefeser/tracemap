using Xunit;

namespace TraceMap.Tests;

// These tests create or inspect Git repositories and assert exact scan identity.
// Concurrent Git-heavy collections can exceed the scanner's bounded Git probe.
[CollectionDefinition("Git metadata sensitive", DisableParallelization = true)]
public sealed class GitMetadataSensitiveCollection { }
