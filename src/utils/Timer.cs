using Silk.NET.GLFW;

namespace VoxelChaos.Utils;

public class Timer
{
    private float deltaTime;
    private readonly Glfw glfw;

    private float lastTime;

    public Timer()
    {
        glfw = Glfw.GetApi();
        deltaTime = 0F;
        lastTime = 0F;
    }

    public virtual float GetDelta()
    {
        return deltaTime;
    }

    public virtual void Update()
    {
        var currentTime = (float)glfw.GetTime();
        ;
        deltaTime = currentTime - lastTime;
        lastTime = currentTime;
    }
}