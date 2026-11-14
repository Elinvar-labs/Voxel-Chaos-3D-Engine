namespace VoxelChaos.Utils;

public static class FileManager
{
    private static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;

    public static string LoadString(string path)
    {
        var fullPath = Path.Combine(BaseDirectory, path);
        return File.ReadAllText(fullPath);
    }

    public static Stream OpenStream(string path)
    {
        var fullPath = Path.Combine(BaseDirectory, path);
        return File.OpenRead(fullPath);
    }
}