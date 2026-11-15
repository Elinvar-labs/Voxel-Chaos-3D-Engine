namespace VoxelChaos.graphics;

using Silk.NET.OpenGL;

public class VoxelTexture3D
{
    private GL _gl;
    private uint _textureId;

    private bool _allocated;      // был ли уже TexImage3D
    private uint _xSize, _ySize, _zSize;

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

    /// <summary>
    /// Синхронизирует CPU-буфер с 3D-текстурой.
    /// Первый вызов (или смена размера) — TexImage3D (аллокация).
    /// Далее — TexSubImage3D (запись поверх, в 2–4 раза быстрее на больших объёмах).
    /// </summary>
    public unsafe void Update(byte[] voxelData, uint xSize, uint ySize, uint zSize)
    {
        if (voxelData == null || xSize == 0 || ySize == 0 || zSize == 0) return;

        _gl.BindTexture(TextureTarget.Texture3D, _textureId);
        fixed (void* ptr = voxelData)
        {
            if (!_allocated || _xSize != xSize || _ySize != ySize || _zSize != zSize)
            {
                _gl.TexImage3D(TextureTarget.Texture3D, 0, InternalFormat.R8ui,
                    xSize, ySize, zSize, 0,
                    PixelFormat.RedInteger, PixelType.UnsignedByte, ptr);

                _allocated = true;
                _xSize = xSize; _ySize = ySize; _zSize = zSize;
            }
            else
            {
                _gl.TexSubImage3D(TextureTarget.Texture3D, 0, 0, 0, 0,
                    xSize, ySize, zSize,
                    PixelFormat.RedInteger, PixelType.UnsignedByte, ptr);
            }
        }
    }
    
    public unsafe void UploadToShader(byte[] voxelDataArray, uint x_size, uint y_size, uint z_size)
        => Update(voxelDataArray, x_size, y_size, z_size);

    public void Bind(Graphics.Shader shader)
    {
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture3D, _textureId);
        shader.SetUniformInt("volumeTexture", 0);
    }
}