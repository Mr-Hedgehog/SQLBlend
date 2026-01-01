using SQLBlend.Config.Models.Config;
using SQLBlend.Database;

namespace SQLBlend.Tests.Database.DataAggregatorTests;

[TestFixture]
public class DataAggregatorUnionTests
{
    private DataAggregator aggregator;
    private Dictionary<string, List<Dictionary<string, object>>> testData;

    [SetUp]
    public void Setup()
    {
        testData = new Dictionary<string, List<Dictionary<string, object>>>();
        InitializeTestData();
    }

    private void InitializeTestData()
    {
        testData["query1"] = new List<Dictionary<string, object>>
        {
            new() { { "id", 1 }, { "name", "Alice" }, { "dept", "IT" } },
            new() { { "id", 2 }, { "name", "Bob" }, { "dept", "HR" } },
            new() { { "id", 3 }, { "name", "Charlie" }, { "dept", "IT" } }
        };

        testData["query2"] = new List<Dictionary<string, object>>
        {
            new() { { "id", 4 }, { "name", "David" }, { "dept", "Sales" } },
            new() { { "id", 5 }, { "name", "Eve" }, { "dept", "IT" } }
        };

        testData["query3"] = new List<Dictionary<string, object>>();

        aggregator = new DataAggregator(testData);
    }

    [Test]
    [Description("Verify that Union operation combines multiple queries into a single result set")]
    public void Union_CombinesMultipleQueries()
    {
        var operations = new List<OperationConfig>
        {
            new()
            {
                Operation = OperationType.Union,
                QueryNames = new List<string> { "query1", "query2" }
            }
        };

        var result = aggregator.ApplyOperations(operations);

        Assert.That(result, Has.Count.EqualTo(5));
    }

    [Test]
    [Description("Verify that Union operation handles empty queries correctly")]
    public void Union_WithEmptyQuery()
    {
        var operations = new List<OperationConfig>
        {
            new()
            {
                Operation = OperationType.Union,
                QueryNames = new List<string> { "query1", "query3" }
            }
        };

        var result = aggregator.ApplyOperations(operations);

        Assert.That(result, Has.Count.EqualTo(3));
    }

    [Test]
    [Description("Verify that Union operation throws KeyNotFoundException when query name doesn't exist")]
    public void Union_ThrowsOnMissingQuery()
    {
        var operations = new List<OperationConfig>
        {
            new()
            {
                Operation = OperationType.Union,
                QueryNames = new List<string> { "nonexistent" }
            }
        };

        Assert.Throws<KeyNotFoundException>(() => aggregator.ApplyOperations(operations));
    }
}
