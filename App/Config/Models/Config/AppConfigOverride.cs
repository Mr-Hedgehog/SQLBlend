namespace SQLBlend.Config.Models.Config;

public class AppConfigOverride
{
    public List<ConnectionStringOverride>? ConnectionStrings { get; set; }
}

public class ConnectionStringOverride
{
    public string Name { get; set; }
    public string ConnectionString { get; set; }
}
