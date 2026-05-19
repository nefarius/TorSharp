using System.IO;
using System.Text.Json;

namespace Proxy.Configurations;

public class Configuration
{
    private static Configuration? _configuration;

    public Configuration(Server server, Authentication authentication, Firewall firewall)
    {
        Server = server;
        Authentication = authentication;
        Firewall = firewall;
    }

    public Server Server { get; private set; }
    public Authentication Authentication { get; private set; }
    public Firewall Firewall { get; private set; }

    public static Configuration? Settings
    {
        get => _configuration ??= JsonSerializer.Deserialize<Configuration>(File.ReadAllText("config.json"));
        set => _configuration = value;
    }
}
