using Xunit;

namespace NpiManagement.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer integration";
}
