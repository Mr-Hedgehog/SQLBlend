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
Prepare `config.json` to define your data sources, queries, parameters, and operations. For example:
```json
{
  "Description": "Optional description of the configuration, displayed in the configuration selector",
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
      "OutputFileName": "query1_result",
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
          "Format": "InClause"
        }
      ]
    }
  ],
  "FiltersAndAggregations": [
    {
      "Name": "FinalResult",
      "OutputFileName": "final_result",
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

Optional: add `config.override.json` next to the executable file to override connection strings for local/dev runs without changing the main `config.json`.

Example `config.override.json`:
```json
{
  "ConnectionStrings": [
    {
      "Name": "Source1",
      "ConnectionString": "Server=localhost;Database=...;User Id=...;Password=..."
    }
  ]
}
```
In the above:

* Optional `Description` field provides a human-readable description of the configuration. It is displayed in the configuration folder selector (limited to 60 characters in the list view) and is also searchable when selecting a configuration.
* Optional `OutputFileName` field (without extension) can be used in `Queries` and `FiltersAndAggregations` to control the output CSV filename. If omitted, `Name` is used.
* Two data sources (`Source1` and `Source2`) are defined.
* `Query1` runs against SQL Server, `Query2` runs against PostgreSQL and uses the results of `Query1` to build its IN clause.
* The final result (`FinalResult`) unions `Query1` and `Query2` results, then filters them.
* If `config.override.json` exists near the executable, connection strings are overridden by matching `Name`.

### Operations examples

Below are minimal `Operations` examples for every supported operation type.

#### 1) `Union`
```json
{
  "Operation": "Union",
  "QueryNames": ["Query1", "Query2", "Query3"]
}
```

#### 2) `Filter`
`Filter` is applied to the current intermediate result (for example, after `Union`).
```json
{
  "Operation": "Filter",
  "Condition": "Value >= 100"
}
```

Also supported in conditions: `=`, `!=`, `<>`, `>`, `<`, `>=`, `<=`, `LIKE`, `IN`.

Examples:
- `"Condition": "Status = Active"`
- `"Condition": "Name LIKE %test%"`
- `"Condition": "Type IN A,B,C"`

#### 3) `InnerJoin`
```json
{
  "Operation": "InnerJoin",
  "LeftQueryName": "Orders",
  "RightQueryName": "Customers",
  "JoinConditions": [
    {
      "LeftColumn": "CustomerId",
      "RightColumn": "Id",
      "Operator": "="
    }
  ],
  "SelectColumns": [
    { "Query": "Left", "Column": "OrderId" },
    { "Query": "Left", "Column": "Amount" },
    { "Query": "Right", "Column": "Name" }
  ]
}
```

#### 4) `LeftJoin`
```json
{
  "Operation": "LeftJoin",
  "LeftQueryName": "Orders",
  "RightQueryName": "Customers",
  "JoinConditions": [
    {
      "LeftColumn": "CustomerId",
      "RightColumn": "Id",
      "Operator": "="
    }
  ],
  "SelectColumns": [
    { "Query": "Left", "Column": "OrderId" },
    { "Query": "Left", "Column": "CustomerId" },
    { "Query": "Right", "Column": "Name" }
  ]
}
```

> Note: joins currently support only `"Operator": "="`.

### Query-level SQL variables

You can define variables per query and use them directly in SQL files.

Config example:
```json
{
  "Name": "query1",
  "DataSourceName": "Query1Source",
  "QueryFilePath": "queries/query1.sql",
  "Variables": {
    "PeriodStartDate": "2026-01-01"
  }
}
```

SQL example (`queries/query1.sql`):
```sql
SELECT *
FROM documents
WHERE created_at >= '{{PeriodStartDate}}';
```

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

### 4. Command Line Arguments

The application supports optional command line arguments:

* `--cache`  
  Enables cache usage (reads previously saved results from the `Results` folder when available).

* `<configsBaseDir>`  
  Optional path to the base directory with configuration folders.  
  If omitted, the current working directory is used.

Default behavior (without `--cache`):

* The app runs without cache.
* Existing files in the `Results` folder are removed before execution.
* Queries and aggregations are executed against data sources and fresh results are saved.

Examples:

* `SQLBlend.exe`
* `SQLBlend.exe --cache`
* `SQLBlend.exe C:\Configs`
* `SQLBlend.exe C:\Configs --cache`

## Requirements
* .NET 9.0 or newer
* Dapper (added via NuGet)
* Microsoft.Data.SqlClient for SQL Server support
* Npgsql for PostgreSQL support

## Contributing
Contributions are welcome! Please open an issue for new features, bug fixes, or questions, and feel free to submit pull requests.
