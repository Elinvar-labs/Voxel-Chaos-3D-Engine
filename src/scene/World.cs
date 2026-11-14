using System;
using raymarching;
using Silk.NET.OpenGL;
using Shader = VoxelChaos.Graphics.Shader;

namespace VoxelChaos.scene;

public class World
{
    private uint renderWidth, renderHeight, realWidth, realHeight;
    private PixelRenderer renderer;
    private GL _gl;
    private byte[] screenImage;

    string computeShaderPath = "assets/shaders/DDA_Voxeling.glsl";
    private Shader DDAShader;
    private ByteShaderStorageBuffer _ssbo;
    public Chunk[] chunks = new Chunk[4*4*4];

    public World(GL gl, float camX, float camY, float camZ, uint realWidth, uint realHeight, uint renderWidth,
        uint renderHeight)
    {
        this._gl = gl; 

        this.renderWidth = renderWidth;
        this.renderHeight = renderHeight;
        this.realWidth = realWidth;
        this.realHeight = realHeight;
        
        renderer = new PixelRenderer();
        renderer.Initialize(gl, renderWidth, renderHeight, realWidth, realHeight);
        
        screenImage = new byte[renderWidth * renderHeight * 4];
        
        _ssbo = new ByteShaderStorageBuffer(gl);
        _ssbo.Initialize(screenImage); 

        DDAShader = new Shader(gl, computeShaderPath);
    }

    public void Render()
    {
        // TODO: UPLOAD CHUNKS TO COMPUTE SHADER AS TEXTURE3D (3D Texture Bind)
        GetScreenImageFromShader();
        renderer.UploadLowResImage(screenImage, renderWidth, renderHeight);
        renderer.Render(realWidth, realHeight);
    }

    void GenerateWorld()
    {
            
    }
    void GetScreenImageFromShader()
    {
        // Coutning groups for DispatchCompute
        int totalUints = screenImage.Length / 4;
        uint groupsX = (uint)Math.Ceiling((double)totalUints / 256);
        
        _ssbo.Bind();
        DDAShader.Bind();
        
        // Run GPU Computing
        _gl.DispatchCompute(groupsX, 1, 1); 

        // Download Computed Data to screenImage
        _ssbo.DownloadData(screenImage);
    }

    public void Update()
    {
    }
}
