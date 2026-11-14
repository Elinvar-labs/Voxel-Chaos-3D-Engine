namespace VoxelChaos.Utils;

public class AssetsManager
{
    private readonly string AudioPath = "Audio";
    private readonly string FontPath = "Fonts";
    private readonly string MapsPath = "Maps";
    private readonly string ModelPath = "Models";
    private readonly string QuakeMapPath = "Maps";
    private readonly string ShaderPath = "Shaders";
    private readonly string SkyBoxPath = "Skyboxes";
    private readonly string TexturePath = "Textures";
    private readonly string rootPath = AppDomain.CurrentDomain.BaseDirectory;

    public virtual string? GetTexture(string name)
    {
        if (name == null) Console.WriteLine("ERROR: Broken path for texture: " + name);
        var fullPath = rootPath + "/" + TexturePath + "/" + name;
        Console.WriteLine("Trying open path: " + fullPath);
        if (ValidatePath(fullPath)) return fullPath;

        return null;
    }

    public virtual string? GetModel(string name)
    {
        var fullPath = rootPath + "/" + ModelPath + "/" + name;
        Console.WriteLine("Trying open path: " + fullPath);
        if (ValidatePath(fullPath)) return fullPath;

        return null;
    }

    public virtual string? GetAudio(string name)
    {
        var fullPath = rootPath + "/" + AudioPath + "/" + name;
        Console.WriteLine("Trying open path: " + fullPath);
        if (ValidatePath(fullPath)) return fullPath;

        return null;
    }

    public virtual string? GetShader(string name)
    {
        var fullPath = rootPath + "/" + ShaderPath + "/" + name;
        Console.WriteLine("Trying open path: " + fullPath);
        if (ValidatePath(fullPath)) return fullPath;

        return null;
    }

    public virtual string? GetFont(string name)
    {
        var fullPath = rootPath + "/" + FontPath + "/" + name;
        Console.WriteLine("Trying open path: " + fullPath);
        if (ValidatePath(fullPath)) return fullPath;

        return null;
    }

    public virtual string? GetSkyBox(string folder)
    {
        var fullPath = GetTexture(SkyBoxPath + "/" + folder + "/");
        if (ValidatePath(fullPath)) return fullPath;

        return null;
    }

    public virtual string? GetMap(string name)
    {
        var fullPath = GetTexture(MapsPath + "/" + name);
        if (ValidatePath(fullPath)) return fullPath;

        return null;
    }

    public virtual string? GetQuakeMap(string name)
    {
        var fullPath = rootPath + "/" + QuakeMapPath + "/" + name;
        Console.WriteLine("Trying open path: " + fullPath);
        if (ValidatePath(fullPath)) return fullPath;

        return null;
    }

    private static bool ValidatePath(string? path)
    {
        if (path == null)
        {
            Console.WriteLine("ERROR: NULL PATH: " + path);
            return false;
        }

        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            Path.GetFullPath(path);
            return true;
        }
        catch (Exception)
        {
            Console.WriteLine("ERROR: BAD PATH: " + path);
            return false;
        }
    }

    public virtual string GetRoot()
    {
        return rootPath;
    }
}
