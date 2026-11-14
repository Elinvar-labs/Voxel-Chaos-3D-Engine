using System.Numerics;
using Silk.NET.OpenGL;
using Shader = VoxelChaos.Graphics.Shader;

namespace VoxelChaos.Utils;

public class ShaderManager
{
    private static readonly Dictionary<string, Shader> shaders = new();

    public static Shader Get(string name)
    {
        return shaders[name];
    }

    public static void Add(GL _gl, string name, string vertexShaderPath, string fragmentShaderPath)
    {
        shaders.Add(name, new Shader(_gl, vertexShaderPath, fragmentShaderPath));
    }

    public static void Clear()
    {
        foreach (var key in shaders.Keys.ToArray())
            shaders[key].Cleanup();
        shaders.Clear();
    }

    public static void Replace(GL _gl, string name, string vertexShaderPath, string fragmentShaderPath)
    {
        shaders.Remove(name);
        shaders.Add(name, new Shader(_gl, vertexShaderPath, fragmentShaderPath));
    }

    public static void Bind(string name)
    {
        shaders[name].Bind();
    }

    public static void Unbind(string name)
    {
        shaders[name].Unbind();
    }

    public static void SetUniform(string shaderName, string location, int n)
    {
        shaders[shaderName].SetUniformInt(location, n);
    }

    public static void SetUniform(string shaderName, string location, float n)
    {
        shaders[shaderName].SetUniformFloat(location, n);
    }

    public static void SetUniform(string shaderName, string location, float x, float y, float z)
    {
        shaders[shaderName].SetUniformVec3f(location, x, y, z);
    }

    public static void SetUniform(string shaderName, string location, Vector3 vec)
    {
        shaders[shaderName].SetUniformVec3f(location, vec);
    }

    public static void SetUniform(string shaderName, string location, float x, float y, float z, float w)
    {
        shaders[shaderName].SetUniformVec4f(location, x, y, z, w);
    }

    public static void SetUniform(string shaderName, string location, Vector4 vec)
    {
        shaders[shaderName].SetUniformVec4f(location, vec);
    }

    public static void SetUniform(string shaderName, string location, Matrix4x4 mat)
    {
        shaders[shaderName].SetUniformMatrix4f(location, mat);
    }
}