// Disable parallel execution to avoid file-system races.

namespace Nalix.Shared.Tests.Environment;

[Xunit.CollectionDefinition("DirectoriesTests", DisableParallelization = true)]
public sealed class DirectoriesTestsCollection : System.Object
{
}
