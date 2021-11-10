using Hackney.Core.Testing.Shared;
using Xunit;

namespace ProcessesApi.Tests
{
    [CollectionDefinition("LogCall collection")]
    public class LogCallAspectFixtureCollection : ICollectionFixture<LogCallAspectFixture>
    { }
}
