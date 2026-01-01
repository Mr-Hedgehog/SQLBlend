namespace SQLBlend.Config.Models.Config;

public class DbConnectionInfo
{
    public string Name { get; set; }
    public DbConnectionType Type { get; set; }
    public string ConnectionString { get; set; }
}
