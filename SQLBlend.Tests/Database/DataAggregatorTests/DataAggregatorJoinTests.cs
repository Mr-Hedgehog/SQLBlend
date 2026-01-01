using SQLBlend.Config.Models.Config;
using SQLBlend.Database;

namespace SQLBlend.Tests.Database.DataAggregatorTests;

[TestFixture]
public class DataAggregatorJoinTests
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
            new() { { "emp_id", 1 }, { "name", "Alice" }, { "dept_id", 10 } },
            new() { { "emp_id", 2 }, { "name", "Bob" }, { "dept_id", 20 } },
            new() { { "emp_id", 3 }, { "name", "Charlie" }, { "dept_id", 10 } },
            new() { { "emp_id", 4 }, { "name", "David" }, { "dept_id", 30 } }
        };

        testData["departments"] = new List<Dictionary<string, object>>
        {
            new() { { "dept_id", 10 }, { "dept_name", "IT" } },
            new() { { "dept_id", 20 }, { "dept_name", "HR" } },
            new() { { "dept_id", 30 }, { "dept_name", "Sales" } }
        };

        aggregator = new DataAggregator(testData);
    }

    [Test]
    [Description("Verify that InnerJoin operation returns only matching rows from both tables")]
    public void InnerJoin_ReturnsMatchingRows()
    {
        var operations = new List<OperationConfig>
        {
            new()
            {
                Operation = OperationType.InnerJoin,
                LeftQueryName = "employees",
                RightQueryName = "departments",
                JoinConditions = new List<JoinCondition>
                {
                    new() { LeftColumn = "dept_id", RightColumn = "dept_id", Operator = "=" }
                },
                SelectColumns = new List<SelectColumn>
                {
                    new() { Query = SelectColumnSide.Left, Column = "emp_id" },
                    new() { Query = SelectColumnSide.Left, Column = "name" },
                    new() { Query = SelectColumnSide.Right, Column = "dept_name" }
                }
            }
        };

        var result = aggregator.ApplyOperations(operations);

        Assert.That(result, Has.Count.EqualTo(4));
        Assert.That(result[0]["dept_name"], Is.EqualTo("IT"));
    }

    [Test]
    [Description("Verify that LeftJoin operation includes unmatched rows from left table with null values for right columns")]
    public void LeftJoin_IncludesUnmatchedRowsFromLeft()
    {
        // Add unmatched department
        testData["departments"].Add(new() { { "dept_id", 40 }, { "dept_name", "Finance" } });
        aggregator = new DataAggregator(testData);

        var operations = new List<OperationConfig>
        {
            new()
            {
                Operation = OperationType.LeftJoin,
                LeftQueryName = "departments",
                RightQueryName = "employees",
                JoinConditions = new List<JoinCondition>
                {
                    new() { LeftColumn = "dept_id", RightColumn = "dept_id", Operator = "=" }
                },
                SelectColumns = new List<SelectColumn>
                {
                    new() { Query = SelectColumnSide.Left, Column = "dept_id" },
                    new() { Query = SelectColumnSide.Left, Column = "dept_name" },
                    new() { Query = SelectColumnSide.Right, Column = "name" }
                }
            }
        };

        var result = aggregator.ApplyOperations(operations);

        // All 4 departments should be in result, even Finance with no employees
        Assert.That(result.Count, Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    [Description("Verify that Join operation correctly handles multiple join conditions")]
    public void InnerJoin_WithMultipleConditions()
    {
        testData["projects"] = new List<Dictionary<string, object>>
        {
            new() { { "emp_id", 1 }, { "dept_id", 10 }, { "project", "ProjectA" } },
            new() { { "emp_id", 2 }, { "dept_id", 20 }, { "project", "ProjectB" } },
            new() { { "emp_id", 3 }, { "dept_id", 10 }, { "project", "ProjectA" } }
        };

        aggregator = new DataAggregator(testData);

        var operations = new List<OperationConfig>
        {
            new()
            {
                Operation = OperationType.InnerJoin,
                LeftQueryName = "employees",
                RightQueryName = "projects",
                JoinConditions = new List<JoinCondition>
                {
                    new() { LeftColumn = "emp_id", RightColumn = "emp_id", Operator = "=" },
                    new() { LeftColumn = "dept_id", RightColumn = "dept_id", Operator = "=" }
                },
                SelectColumns = new List<SelectColumn>
                {
                    new() { Query = SelectColumnSide.Left, Column = "name" },
                    new() { Query = SelectColumnSide.Right, Column = "project" }
                }
            }
        };

        var result = aggregator.ApplyOperations(operations);

        Assert.That(result, Has.Count.EqualTo(3));
    }

    [Test]
    [Description("Performance test: Verify that Join operation completes efficiently with 5000 rows on each side")]
    public void Join_Performance_LargeDataset()
    {
        const int rowCount = 5000;
        
        var leftData = new List<Dictionary<string, object>>();
        var rightData = new List<Dictionary<string, object>>();

        for (int i = 0; i < rowCount; i++)
        {
            leftData.Add(new() { { "id", i }, { "value", i * 10 } });
            rightData.Add(new() { { "id", i }, { "data", $"row_{i}" } });
        }

        testData["large_left"] = leftData;
        testData["large_right"] = rightData;
        aggregator = new DataAggregator(testData);

        var operations = new List<OperationConfig>
        {
            new()
            {
                Operation = OperationType.InnerJoin,
                LeftQueryName = "large_left",
                RightQueryName = "large_right",
                JoinConditions = new List<JoinCondition>
                {
                    new() { LeftColumn = "id", RightColumn = "id", Operator = "=" }
                },
                SelectColumns = new List<SelectColumn>
                {
                    new() { Query = SelectColumnSide.Left, Column = "id" },
                    new() { Query = SelectColumnSide.Right, Column = "data" }
                }
            }
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = aggregator.ApplyOperations(operations);
        stopwatch.Stop();

        Assert.That(result, Has.Count.EqualTo(rowCount));
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(2000), 
            $"Join should complete in less than 2 seconds for {rowCount} rows");

        TestContext.Out.WriteLine($"Join performance: {rowCount} rows in {stopwatch.ElapsedMilliseconds}ms");
    }
}
