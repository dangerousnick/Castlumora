using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

/// <summary>
/// Real executable startup deadlines must not compete with unrelated CPU-heavy fixtures.
/// Explicit lease/concurrency assertions inside each test remain unchanged.
/// </summary>
[CollectionDefinition(nameof(ImportExecutableCollection), DisableParallelization = true)]
public sealed class ImportExecutableCollection
{
}
