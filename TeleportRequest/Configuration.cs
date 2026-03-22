using Newtonsoft.Json;

namespace TeleportRequest;

public class Configuration
{
    public int Interval = 3;
    public int Timeout = 3;

    public void Write(string path)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write);
        this.Write(stream);
    }

    public void Write(Stream stream)
    {
        var value = JsonConvert.SerializeObject(this, Formatting.Indented);
        using var streamWriter = new StreamWriter(stream);
        streamWriter.Write(value);
    }

    public static Configuration Read(string path)
    {
        if (!File.Exists(path))
        {
            return new Configuration();
        }
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Read(stream);
    }

    public static Configuration Read(Stream stream)
    {
        using var streamReader = new StreamReader(stream);
        return JsonConvert.DeserializeObject<Configuration>(streamReader.ReadToEnd()) ?? new Configuration();
    }
}