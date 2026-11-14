using System.Numerics;
using Silk.NET.OpenGL;

namespace VoxelChaos.Graphics;

public class Shader
{
    private readonly uint program;
    private readonly GL _gl;
    private uint fragmentShaderId;
    private uint vertexShaderId;
    private uint computeShaderId;

    public Shader(GL _gl, string vertexPath, string fragmentPath)
    {
        this._gl = _gl;
        program = _gl.CreateProgram();
        if (program == 0) Console.WriteLine("CANT::CREATE::SHADER_PROGRAM");

        CreateVertexShader(LoadFile(vertexPath));
        CreateFragmentShader(LoadFile(fragmentPath));
        Link();
        _gl.DetachShader(program, vertexShaderId);
        _gl.DetachShader(program, fragmentShaderId);
        _gl.DeleteShader(vertexShaderId);
        _gl.DeleteShader(fragmentShaderId);
    }

    public Shader(GL _gl, string computePath)
    {
        this._gl = _gl;
        program = _gl.CreateProgram();
        if (program == 0) Console.WriteLine("CANT::CREATE::SHADER_PROGRAM");
        CreateComputeShader(LoadFile(computePath));
        Link();
        _gl.DetachShader(program, computeShaderId);
        _gl.DeleteShader(computeShaderId);
    }

    public virtual void CreateVertexShader(string? shaderCode)
    {
        if (shaderCode == null)
        {
            Console.WriteLine("ERROR: Cannot create Vertex Shader with ID: " + vertexShaderId);
            return;
        }

        vertexShaderId = CreateShader(shaderCode, ShaderType.VertexShader);
    }

    public virtual void CreateFragmentShader(string? shaderCode)
    {
        if (shaderCode == null)
        {
            Console.WriteLine("ERROR: Cannot create Fragment Shader with ID: " + fragmentShaderId);
            return;
        }

        fragmentShaderId = CreateShader(shaderCode, ShaderType.FragmentShader);
    }

    public virtual void CreateComputeShader(string? shaderCode)
    {
        if (shaderCode == null)
        {
            Console.WriteLine("ERROR: Cannot create Compute Shader with ID: " + vertexShaderId);
            return;
        }

        computeShaderId = CreateShader(shaderCode, ShaderType.ComputeShader);
    }

    protected virtual uint CreateShader(string shaderCode, ShaderType shaderType)
    {
        var shaderId = _gl.CreateShader(shaderType);
        if (shaderId == 0) Console.WriteLine("Error creating shader. Type: " + shaderType);

        _gl.ShaderSource(shaderId, shaderCode);
        _gl.CompileShader(shaderId);
        _gl.GetShader(shaderId, ShaderParameterName.CompileStatus, out var vStatus);
        if (vStatus != (int)GLEnum.True)
            throw new Exception("Shader failed to compile: " + _gl.GetShaderInfoLog(shaderId));

        _gl.AttachShader(program, shaderId);
        return shaderId;
    }

    public virtual void Link()
    {
        _gl.LinkProgram(program);
        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var lStatus);
        if (lStatus != (int)GLEnum.True)
            throw new Exception("Program failed to link: " + _gl.GetProgramInfoLog(program));

        if (vertexShaderId != 0) _gl.DetachShader(program, vertexShaderId);

        if (fragmentShaderId != 0) _gl.DetachShader(program, fragmentShaderId);
        _gl.ValidateProgram(program);
        _gl.GetProgram(program, ProgramPropertyARB.ValidateStatus, out var vStatus);
        if (vStatus != (int)GLEnum.True)
            throw new Exception("Program failed to validate: " + _gl.GetProgramInfoLog(program));
    }

    public virtual void Bind()
    {
        _gl.UseProgram(program);
    }

    public virtual void Unbind()
    {
        _gl.UseProgram(0);
    }

    public virtual void Cleanup()
    {
        Unbind();
        if (program != 0) _gl.DeleteProgram(program);
    }

    public virtual void SetUniformFloat(string name, float x)
    {
        var location = _gl.GetUniformLocation(program, name);
        _gl.Uniform1(location, x);
    }

    public virtual void SetUniformInt(string name, int x)
    {
        var location = _gl.GetUniformLocation(program, name);
        _gl.Uniform1(location, x);
    }

    public virtual unsafe void SetUniformMatrix4f(string name, Matrix4x4 matrix)
    {
        var location = _gl.GetUniformLocation(program, name);
        _gl.UniformMatrix4(location, 1, false, (float*)&matrix);
    }

    public virtual void SetUniformVec3f(string name, Vector3 vec)
    {
        var location = _gl.GetUniformLocation(program, name);
        _gl.Uniform3(location, vec);
    }

    public virtual void SetUniformVec3f(string name, float x, float y, float z)
    {
        var location = _gl.GetUniformLocation(program, name);
        _gl.Uniform3(location, x, y, z);
    }

    public virtual void SetUniformVec4f(string name, Vector4 vec)
    {
        var location = _gl.GetUniformLocation(program, name);
        _gl.Uniform4(location, vec);
    }

    public virtual void SetUniformVec4f(string name, float x, float y, float z, float w)
    {
        var location = _gl.GetUniformLocation(program, name);
        _gl.Uniform4(location, x, y, z, w);
    }

    private static string? LoadFile(string filePath)
    {
        try
        {
            return File.ReadAllText(filePath);
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine("ERROR: The file was not found: " + filePath);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"ERROR: An I/O error occurred: {ex.Message} in file: " + filePath);
        }

        return null;
    }
}