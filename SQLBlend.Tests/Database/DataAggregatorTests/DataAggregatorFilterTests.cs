using SQLBlend.Config.Models.Config;
using SQLBlend.Database;

namespace SQLBlend.Tests.Database.DataAggregatorTests;

[TestFixture]
public class DataAggregatorFilterTests
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
        testData["employees"] = new List<Dictionary<string, object>>
        {
            new() { { "id", 1 }, { "name", "Alice" }, { "salary", 50000 } },
            new() { { "id", 2 }, { "name", "Bob" }, { "salary", 60000 } },
            new() { { "id", 3 }, { "name", "Charlie" }, { "salary", 75000 } },
            new() { { "id", 4 }, { "name", "David" }, { "salary", 55000 } }
        };

        aggregator = new DataAggregator(testData);
    }

    [Test]
    [Description("Verify that Filter operation correctly applies equality condition")]
    public void Filter_EqualsCondition()
    {
        var operations = new List<OperationConfig>
        {
            new()
            {
                Operation = OperationType.Union,
                QueryNames = new List<string> { "employees" }
            },
            new()
            {
                Operation = OperationType.Filter,
                Condition = "id = 2"
            }
        };

        var result = aggregator.ApplyOperations(operations);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0]["name"], Is.EqualTo("Bob"));
    }

    [Test]
    [Description("Verify that Filter operation correctly applies greater-than condition")]
    public void Filter_GreaterThanCondition()
    {
        var operations = new List<OperationConfig>
        {
            new()
            {
                Operation = OperationType.Union,
                QueryNames = new List<string> { "employees" }
            },
            new()
            {
                Operation = OperationType.Filter,
                Condition = "salary > 60000"
            }
        };

        var result = aggregator.ApplyOperations(operations);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0]["name"], Is.EqualTo("Charlie"));
    }

    [Test]
    [Description("Verify that Filter operation correctly applies less-than condition")]
    public void Filter_LessThanCondition()
    {
        var operations = new List<OperationConfig>
        {
            new()
            {
                Operation = OperationType.Union,
                QueryNames = new List<string> { "employees" }
            },
            new()
            {
                Operation = OperationType.Filter,
                Condition = "salary < 60000"
            }
        };

        var result = aggregator.ApplyOperations(operations);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    [Description("Verify that Filter operation correctly applies greater-than-or-equal condition")]
    public void Filter_GreaterOrEqualCondition()
    {
        var operations = new List<OperationConfig>
        {
            new()
            {
                Operation = OperationType.Union,
                QueryNames = new List<string> { "employees" }
            },
            new()
            {
                Operation = OperationType.Filter,
                Condition = "salary >= 60000"
            }
        };

        var result = aggregator.ApplyOperations(operations);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    [Description("Verify that Filter operation correctly applies not-equal condition")]
    public void Filter_NotEqualCondition()
    {
        var operations = new List<OperationConfig>
        {
            new()
            {
                Operation = OperationType.Union,
                QueryNames = new List<string> { "employees" }
            },
            new()
            {
                Operation = OperationType.Filter,
                Condition = "id <> 2"
            }
        };

        var result = aggregator.ApplyOperations(operations);

        Assert.That(result, Has.Count.EqualTo(3));
    }

    [Test]
    [Description("Verify that Filter operation correctly applies LIKE pattern matching with wildcards")]
    public void Filter_LikeCondition()
    {
        var operations = new List<OperationConfig>
        {
            new()
            {
                Operation = OperationType.Union,
                QueryNames = new List<string> { "employees" }
            },
            new()
            {
                Operation = OperationType.Filter,
                Condition = "name LIKE %li%"
            }
        };

        var result = aggregator.ApplyOperations(operations);

        Assert.That(result, Has.Count.EqualTo(2)); // Alice, Charlie
    }

    [Test]
    [Description("Verify that Filter operation correctly applies IN condition with comma-separated values")]
    public void Filter_InCondition()
    {
        var operations = new List<OperationConfig>
        {
            new()
            {
                Operation = OperationType.Union,
                QueryNames = new List<string> { "employees" }
            },
            new()
            {
                Operation = OperationType.Filter,
                Condition = "id IN 1,3,4"
            }
        };

        var result = aggregator.ApplyOperations(operations);

        Assert.That(result, Has.Count.EqualTo(3));
    }

    [Test]
    [Description("Verify that Filter operation returns all rows when condition is empty")]
    public void Filter_EmptyCondition()
    {
        var operations = new List<OperationConfig>
        {
            new()
            {
                Operation = OperationType.Union,
                QueryNames = new List<string> { "employees" }
            },
            new()
            {
                Operation = OperationType.Filter,
                Condition = ""
            }
        };

        var result = aggregator.ApplyOperations(operations);

        Assert.That(result, Has.Count.EqualTo(4)); // Returns all rows
    }
}