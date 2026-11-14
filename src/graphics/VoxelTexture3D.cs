namespace VoxelChaos.graphics;

using Silk.NET.OpenGL;

public class VoxelTexture3D
{
    private GL _gl;

    public VoxelTexture3D(GL gl)
    {
        _gl = gl;
        CreateTexture();
    }

    private void CreateTexture()
    {
        // Generate and bind the 3D texture identifier
        uint textureId = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture3D, textureId);

        // Configure wrapping options for all 3 coordinate axes (S, T, R)
        _gl.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);

        // Configure volumetric minification and magnification filtering
        _gl.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
    }

    public unsafe void UploadToShader(byte[] voxelDataArray, uint x_size, uint y_size, uint z_size)
    {
        // Allocate and upload raw volumetric voxel bytes to the GPU
        {
            fixed (void* dataPtr = voxelDataArray)
            {
                _gl.TexImage3D(TextureTarget.Texture3D, 0, InternalFormat.Rgba8,
                    x_size, y_size, z_size, 0, PixelFormat.Rgba, PixelType.UnsignedByte, dataPtr);
            }
        }
    }

    public void Bind(Graphics.Shader shader)
    {
        shader.SetUniformInt("volumeTexture", 0); // Assign to Texture Unit 0
    }
    
}