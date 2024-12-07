# SQLBlend

***SQLBlend*** is a console-based C# application designed to execute SQL queries against various databases (currently supporting ***Microsoft SQL Server*** and ***PostgreSQL***), then combine the retrieved results and apply filtering and aggregation operations. The application allows to dynamically configure data sources, queries, and transformations via a configuration file, including the ability to perform sequential queries where one query’s results can be used as parameters for another.

## Features
* Multi-Database Support: Execute queries against SQL Server and PostgreSQL.
* Config-Driven: A JSON configuration file (e.g., appsettings.json) defines:
  * Multiple connection strings to different data sources.
  * A set of queries referencing these data sources and their SQL files.
  * Parameters that can use previous query results as inputs.
* Sequential Query Execution: Run queries in sequence, using results from previous queries to dynamically construct subsequent ones (e.g., building IN clauses from returned IDs).
* In-Memory Aggregations and Filtering:
  * Union: Merge multiple query results into a single dataset.
  * Filter: Apply conditions (e.g., Value > 100) to filter rows.
  * InnerJoin & LeftJoin: Join results from multiple queries on one or more columns, using different conditions.
  * Extensible and Customizable: Most logic is configured via the JSON file, reducing the need to modify code when requirements change.
 
## Workflow
### 1. Set up Configuration:
Prepare config.json to define your data sources, queries, parameters, and operations. For example:
```json
{
  "ConnectionStrings": [
    {
      "Name": "Source1",
      "Type": "SqlServer",
      "ConnectionString": "Server=...;Database=...;User Id=...;Password=..."
    },
    {
      "Name": "Source2",
      "Type": "Postgres",
      "ConnectionString": "Host=...;Database=...;Username=...;Password=..."
    }
  ],
  "Queries": [
    {
      "Name": "Query1",
      "DataSourceName": "Source1",
      "QueryFilePath": "queries/query1.sql"
    },
    {
      "Name": "Query2",
      "DataSourceName": "Source2",
      "QueryFilePath": "queries/query2.sql",
      "Parameters": [
        {
          "Name": "PrevIds",
          "FromQuery": "Query1",
          "Column": "Id",
          "Format": "IN_CLAUSE"
        }
      ]
    }
  ],
  "FiltersAndAggregations": [
    {
      "Name": "FinalResult",
      "Operations": [
        {
          "Operation": "Union",
          "QueryNames": [ "Query1", "Query2" ]
        },
        {
          "Operation": "Filter",
          "Condition": "Value > 100"
        }
      ]
    }
  ]
}

```
In the above:

Two data sources (`Source1` and `Source2`) are defined.
`Query1` runs against SQL Server, `Query2` runs against PostgreSQL and uses the results of `Query1` to build its IN clause.
The final result (`FinalResult`) unions `Query1` and `Query2` results, then filters them.

### 2. Add SQL Files:
Store your queries in separate .sql files, for example:
* `queries/query1.sql`:
```sql
SELECT Id, Name, Value FROM SomeTable;
```
* `queries/query2.sql`:
```sql
SELECT Id, Name, Value FROM AnotherTable WHERE Id IN @PrevIds;
```
### 3. Run the Application:
Simply run the console application. It will:
* Load the configuration.
* Connect to the specified databases.
* Execute Query1 first, then Query2 (substituting parameters from Query1’s results).
* Apply the union and filter operations.
* Export the final output.

## Requirements
* .NET 9.0 or newer
* Dapper (added via NuGet)
* Microsoft.Data.SqlClient for SQL Server support
* Npgsql for PostgreSQL support

## Contributing
Contributions are welcome! Please open an issue for new features, bug fixes, or questions, and feel free to submit pull requests.
