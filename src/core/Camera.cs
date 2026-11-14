using System;
using System.Numerics;
using Silk.NET.Input;
using VoxelChaos.Graphics;
using Timer = VoxelChaos.Utils.Timer;

namespace VoxelChaos.Core;

public class Camera
{
    private Vector3 front = new(0F, 0F, -1F);
    private readonly uint heightPx;
    private readonly Timer movementTimer;
    private float pitch;
    private Vector3 position;
    private Vector3 right;
    private readonly float fov;

    private readonly float sensitivity = 0.1F;
    private readonly float speed = 10F;
    private Vector3 up;

    private readonly uint widthPx;
    private readonly Vector3 worldUp = new(0F, 1F, 0F);
    private float yaw = 0F;

    public Camera(Vector3 startPos, float fov, uint winWidth, uint winHeight)
    {
        widthPx = winWidth;
        heightPx = winHeight;
        movementTimer = new Timer();
        position = startPos;
        this.fov = fov;

        UpdateVectors();
    }

    public void Render(Shader shader)
    {
        shader.SetUniformVec3f("cameraPos", position);
        shader.SetUniformVec3f("cameraFront", front);
        shader.SetUniformVec3f("cameraUp", up);
        shader.SetUniformVec3f("cameraRight", right);
        shader.SetUniformFloat("fov", fov);
    } 

    public Vector3 GetPosition() => position;
    public Vector3 GetFront() => front;
    public Vector3 GetRight() => right;
    public Vector3 GetUp() => up;

    public void Update()
    {
        ParseInput();
    }

    private void UpdateVectors()
    {
        right = Vector3.Normalize(Vector3.Cross(front, worldUp));
        up = Vector3.Normalize(Vector3.Cross(right, front));
    }

    public virtual void ParseInput()
    {
        movementTimer.Update();
        var delta = movementTimer.GetDelta();
        var cameraSpeed = (Input.Pressed(Key.ControlLeft) ? speed + 80F : speed) * delta;

        var movement = Vector3.Zero;
        
        var forwardPlane = Vector3.Normalize(new Vector3(front.X, 0.0f, front.Z));

        if (Input.Pressed(Key.W)) movement += forwardPlane * cameraSpeed;
        if (Input.Pressed(Key.S)) movement -= forwardPlane * cameraSpeed;
        if (Input.Pressed(Key.A)) movement += right * cameraSpeed;
        if (Input.Pressed(Key.D)) movement -= right * cameraSpeed;
        if (Input.Pressed(Key.ShiftLeft)) movement.Y += cameraSpeed;
        if (Input.Pressed(Key.Space)) movement.Y -= cameraSpeed;

        if (movement.LengthSquared() > 0) position += movement;

        yaw -= Input.DeltaX * sensitivity;
        pitch -= Input.DeltaY * sensitivity;

        pitch = Math.Clamp(pitch, -89F, 89F);
        yaw = (yaw % 360f + 360f) % 360f;

        front.X = (float)(Math.Cos(pitch.ToRadians()) * Math.Cos(yaw.ToRadians()));
        front.Y = (float)Math.Sin(pitch.ToRadians());
        front.Z = (float)(Math.Cos(pitch.ToRadians()) * Math.Sin(yaw.ToRadians()));
        front = Vector3.Normalize(front);

        UpdateVectors();
    }

    public virtual void SetPosition(Vector3 pos)
    {
        position = pos;
    }
}
