namespace SQLBlend.Database;

internal interface IDatabaseClient
{
    Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string query);
}
