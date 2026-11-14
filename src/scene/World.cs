using System;
using System.Numerics;
using raymarching;
using Silk.NET.OpenGL;
using VoxelChaos.Core;
using VoxelChaos.graphics;
using VoxelChaos.Voxeling;
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
    private VoxelTexture3D voxelTexture3D;

    private Chunk[] chunks = new Chunk[4 * 4 * 4];
    public Camera camera;

    public World(GL gl, float camX, float camY, float camZ, uint realWidth, uint realHeight, uint renderWidth,
        uint renderHeight)
    {
        this._gl = gl;

        this.renderWidth = renderWidth;
        this.renderHeight = renderHeight;
        this.realWidth = realWidth;
        this.realHeight = realHeight;

        camera = new Camera(new Vector3(camX, camY, camZ), 60, renderWidth, renderHeight);
        GenerateWorld();

        renderer = new PixelRenderer();
        renderer.Initialize(gl, renderWidth, renderHeight, realWidth, realHeight);

        screenImage = new byte[renderWidth * renderHeight * 4];

        _ssbo = new ByteShaderStorageBuffer(gl);
        _ssbo.Initialize(screenImage);

        DDAShader = new Shader(gl, computeShaderPath);
        voxelTexture3D = new VoxelTexture3D(gl);
    }

    public void Render()
    {
        for (int i = 0; i < screenImage.Length; i++)
            screenImage[i] = 255;
        DDAShader.Bind();
        voxelTexture3D.UploadToShader(chunks[0].data, 32, 32, 32);
        voxelTexture3D.Bind(DDAShader);
        camera.Render(DDAShader);
        GetScreenImageFromShader();
        renderer.UploadLowResImage(screenImage, renderWidth, renderHeight);
        renderer.Render(realWidth, realHeight);
    }

    void GenerateWorld()
    {
        chunks = new Chunk[1];
        chunks[0] = new Chunk();
        for (int i = 0; i < Constants.CHUNK_SIZE_CUBED; i++)
        {
            chunks[0].data[i] = (byte)Random.Shared.Next(0, 2);
        }
    }

    void GetScreenImageFromShader()
    {
        // Groups Compute for DispatchCompute
        int totalUints = screenImage.Length / 4;
        uint groupsX = (uint)Math.Ceiling((double)totalUints / 256);

        DDAShader.Bind();
        _ssbo.Bind();
    
        _gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 0, _ssbo.GetID());

        DDAShader.SetUniformInt("screenWidth", (int)renderWidth);
        DDAShader.SetUniformInt("screenHeight", (int)renderHeight);

        _gl.DispatchCompute(groupsX, 1, 1);

        _ssbo.DownloadData(screenImage);
    }

    public void Update()
    {
        camera.Update();
    }
}