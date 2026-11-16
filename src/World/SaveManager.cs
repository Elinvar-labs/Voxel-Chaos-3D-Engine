using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace VoxelChaos.World;

public static class SaveManager
{
    private const string SaveFolder = "saves";

    // ⚠️ ДОБАВЛЯЕМ IncludeFields = true, чтобы Vector3 (и его поля X, Y, Z) корректно сохранялись
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        WriteIndented = true,
        IncludeFields = true 
    };

    static SaveManager()
    {
        if (!Directory.Exists(SaveFolder)) Directory.CreateDirectory(SaveFolder);
    }

    public static void Save(string slotName, WorldSaveData data)
    {
        string path = Path.Combine(SaveFolder, slotName + ".json");
        string json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static WorldSaveData? Load(string slotName)
    {
        string path = Path.Combine(SaveFolder, slotName + ".json");
        if (!File.Exists(path)) return null;
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<WorldSaveData>(json, JsonOptions);
    }

    public static List<string> GetSaves()
    {
        var list = new List<string>();
        foreach (var file in Directory.GetFiles(SaveFolder, "*.json"))
            list.Add(Path.GetFileNameWithoutExtension(file));
        return list;
    }

    public static void Delete(string slotName)
    {
        string path = Path.Combine(SaveFolder, slotName + ".json");
        if (File.Exists(path)) File.Delete(path);
    }
}