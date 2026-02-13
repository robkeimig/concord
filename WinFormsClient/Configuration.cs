namespace WinFormsClient;

using System.Text.Json;
using System.Text.Json.Serialization;

public class Configuration
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };


    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? LastServerId { get; set; }
    public Keys PushToTalkKey { get; set; } = Keys.None;
    public List<Server> Servers { get; set; } = [];

    public static string ConfigurationFilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            Application.ProductName,
            "configuration.json");
    }

    public static Configuration Load(string? path = null)
    {
        if (!File.Exists(path))
        {
            var created = new Configuration();
            created.SaveChanges();
            return created;
        }

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<Configuration>(json, SerializerOptions) ?? new Configuration();
        config.Servers ??= [];
        return config;
    }

    public void SaveChanges()
    {
        var dir = Path.GetDirectoryName(ConfigurationFilePath());

        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(this, SerializerOptions);
        File.WriteAllText(ConfigurationFilePath(), json);
    }
}
