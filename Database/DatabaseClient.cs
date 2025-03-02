using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Npgsql;
using SQLBlend.Config.Models.Config;

namespace SQLBlend.Database;

internal class DatabaseClient : IDatabaseClient
{
    private readonly string connectionString;
    private readonly DbConnectionType dbType;

    public DatabaseClient(string connectionString, DbConnectionType dbType)
    {
        this.connectionString = connectionString;
        this.dbType = dbType;
    }

    public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string query)
    {
        using (var connection = CreateConnection())
        {
            connection.Open();

            // Execute the query using Dapper.
            // Dapper by default returns an IEnumerable<dynamic>,
            // where each row is represented as an ExpandoObject (IDictionary<string, object>).
            var rows = await connection.QueryAsync(query);

            var result = new List<Dictionary<string, object>>();
            foreach (var row in rows)
            {
                // Each row is an ExpandoObject, which can be cast to IDictionary<string, object>
                if (row is IDictionary<string, object> dictRow)
                {
                    result.Add(new Dictionary<string, object>(dictRow));
                }
            }

            return result;
        }
    }

    private IDbConnection CreateConnection()
    {
        return dbType switch
        {
            DbConnectionType.SqlServer => new SqlConnection(connectionString),
            DbConnectionType.Postgres => new NpgsqlConnection(connectionString),
            _ => throw new NotSupportedException($"Unsupported database type: {dbType}")
        };
    }
}