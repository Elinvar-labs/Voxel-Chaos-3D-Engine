namespace VoxelChaos.graphics;

using Silk.NET.OpenGL;

public class VoxelTexture3D
{
    private GL _gl;
    private uint _textureId;

    public VoxelTexture3D(GL gl)
    {
        _gl = gl;
        CreateTexture();
    }

    private void CreateTexture()
    {
        _textureId = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture3D, _textureId);

        _gl.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
        // integer textures can't filter, nearest is all we ever get
        _gl.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
    }

    // 1 byte per voxel instead of 4 — 4x bandwidth saved, texelFetch-compatible
    public unsafe void UploadToShader(byte[] voxelDataArray, uint x_size, uint y_size, uint z_size)
    {
        _gl.BindTexture(TextureTarget.Texture3D, _textureId);
        fixed (void* dataPtr = voxelDataArray)
        {
            _gl.TexImage3D(TextureTarget.Texture3D, 0, InternalFormat.R8ui,
                x_size, y_size, z_size, 0, PixelFormat.RedInteger, PixelType.UnsignedByte, dataPtr);
        }
    }

    public void Bind(Graphics.Shader shader)
    {
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture3D, _textureId);
        shader.SetUniformInt("volumeTexture", 0);
    }
}