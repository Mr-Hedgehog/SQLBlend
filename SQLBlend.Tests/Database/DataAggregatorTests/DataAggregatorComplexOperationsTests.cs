using SQLBlend.Config.Models.Config;
using SQLBlend.Database;

namespace SQLBlend.Tests.Database.DataAggregatorTests;

[TestFixture]
public class DataAggregatorComplexOperationsTests
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
        testData["sales_q1"] = new List<Dictionary<string, object>>
        {
            new() { { "region", "North" }, { "amount", 10000 }, { "product", "A" } },
            new() { { "region", "South" }, { "amount", 15000 }, { "product", "B" } },
            new() { { "region", "East" }, { "amount", 12000 }, { "product", "A" } }
        };

        testData["sales_q2"] = new List<Dictionary<string, object>>
        {
            new() { { "region", "North" }, { "amount", 11000 }, { "product", "C" } },
            new() { { "region", "West" }, { "amount", 9000 }, { "product", "B" } }
        };

        testData["products"] = new List<Dictionary<string, object>>
        {
            new() { { "product", "A" }, { "category", "Electronics" } },
            new() { { "product", "B" }, { "category", "Clothing" } },
            new() { { "product", "C" }, { "category", "Electronics" } }
        };

        aggregator = new DataAggregator(testData);
    }

    [Test]
    [Description("Verify that complex operation combining Union followed by Filter works correctly")]
    public void ComplexOperation_UnionThenFilter()
    {
        var operations = new List<OperationConfig>
        {
            new()
            {
                Operation = OperationType.Union,
                QueryNames = new List<string> { "sales_q1", "sales_q2" }
            },
            new()
            {
                Operation = OperationType.Filter,
                Condition = "amount > 10000"
            }
        };

        var result = aggregator.ApplyOperations(operations);

        Assert.That(result, Has.Count.EqualTo(3)); // 15000, 12000, 11000
    }

    [Test]
    [Description("Verify that complex operation combining Join on union results works correctly")]
    public void ComplexOperation_UnionThenJoin()
    {
        testData["all_products"] = new List<Dictionary<string, object>>
        {
            new() { { "product", "A" }, { "id", 1 } },
            new() { { "product", "B" }, { "id", 2 } },
            new() { { "product", "C" }, { "id", 3 } }
        };
        
        // Create union result as a separate query for joining
        testData["combined_sales"] = new List<Dictionary<string, object>>();
        testData["combined_sales"].AddRange(testData["sales_q1"]);
        testData["combined_sales"].AddRange(testData["sales_q2"]);
        
        aggregator = new DataAggregator(testData);

        var operations = new List<OperationConfig>
        {
            new()
            {
                Operation = OperationType.InnerJoin,
                LeftQueryName = "combined_sales",
                RightQueryName = "products",
                JoinConditions = new List<JoinCondition>
                {
                    new() { LeftColumn = "product", RightColumn = "product", Operator = "=" }
                },
                SelectColumns = new List<SelectColumn>
                {
                    new() { Query = SelectColumnSide.Left, Column = "region" },
                    new() { Query = SelectColumnSide.Left, Column = "amount" },
                    new() { Query = SelectColumnSide.Right, Column = "category" }
                }
            }
        };

        var result = aggregator.ApplyOperations(operations);
        
        Assert.That(result, Is.Not.Empty);
        Assert.That(result.All(r => r.ContainsKey("category")), Is.True);
    }

    [Test]
    [Description("Verify that multiple sequential Filter operations are applied correctly in sequence")]
    public void MultipleFilters()
    {
        var operations = new List<OperationConfig>
        {
            new()
            {
                Operation = OperationType.Union,
                QueryNames = new List<string> { "sales_q1", "sales_q2" }
            },
            new()
            {
                Operation = OperationType.Filter,
                Condition = "amount > 9000"
            },
            new()
            {
                Operation = OperationType.Filter,
                Condition = "region LIKE %North"
            }
        };

        var result = aggregator.ApplyOperations(operations);

        Assert.That(result.All(r => (int)r["amount"] > 9000), Is.True);
        Assert.That(result.All(r => r["region"]?.ToString()?.Contains("North") ?? false), Is.True);
    }
}
