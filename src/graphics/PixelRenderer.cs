using Silk.NET.OpenGL;

/*
 * This code has adapted version of AMD FSR 1
 * All rights to the FSR algorithm are owned by AMD.
 *
 */

public unsafe class PixelRenderer
{
    private uint _displayProgram;
    private int _easuColorTextureLoc;
    private int _easuInputResolutionLoc;

    private int _easuInputViewportLoc;
    private int _easuOutputResolutionLoc;
    private uint _easuProgram;
    private GL _gl;
    private uint _intermediateFbo;
    private uint _intermediateHeight;

    private uint _intermediateTexture;
    private uint _intermediateWidth;
    private uint _lowResFbo;
    private uint _lowResHeight;

    private uint _lowResTexture;
    private uint _lowResWidth;
    private int _rcasColorTextureLoc;
    private int _rcasInputResolutionLoc;
    private uint _rcasProgram;

    private int _rcasSharpnessLoc;

    private float _sharpness = 0.2f;

    private uint _vao;
    private uint _vbo;

    public void Initialize(GL gl, uint lowResWidth, uint lowResHeight, uint outputWidth, uint outputHeight)
    {
        _gl = gl;
        _lowResWidth = lowResWidth;
        _lowResHeight = lowResHeight;

        var srcAspect = (float)lowResWidth / lowResHeight;
        var outAspect = (float)outputWidth / outputHeight;
        if (srcAspect >= outAspect)
        {
            _intermediateWidth = outputWidth;
            _intermediateHeight = Math.Max(1u, (uint)(outputWidth / srcAspect + 0.5f));
        }
        else
        {
            _intermediateHeight = outputHeight;
            _intermediateWidth = Math.Max(1u, (uint)(outputHeight * srcAspect + 0.5f));
        }

        _lowResTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _lowResTexture);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba,
            _lowResWidth, _lowResHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        _gl.BindTexture(TextureTarget.Texture2D, 0);

        _lowResFbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _lowResFbo);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, _lowResTexture, 0);
        if (_gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
            throw new Exception("Low-res FBO is not complete.");
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        _intermediateTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _intermediateTexture);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba,
            _intermediateWidth, _intermediateHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        _gl.BindTexture(TextureTarget.Texture2D, 0);

        _intermediateFbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _intermediateFbo);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, _intermediateTexture, 0);
        if (_gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
            throw new Exception("Intermediate FBO is not complete.");
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        float[] vertices =
        {
            1.0f, -1.0f, 0.0f, 1.0f,
            1.0f, 1.0f, 0.0f, 0.0f,
            -1.0f, 1.0f, 1.0f, 0.0f,

            1.0f, -1.0f, 0.0f, 1.0f,
            -1.0f, 1.0f, 1.0f, 0.0f,
            -1.0f, -1.0f, 1.0f, 1.0f
        };
        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* v = vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), v,
                BufferUsageARB.StaticDraw);
        }

        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float),
            (void*)(2 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.BindVertexArray(0);

        InitDisplayShader();
        InitFSRShaders();
    }

    public void UploadLowResImage(byte[] image, uint width, uint height)
    {
        if (width != _lowResWidth || height != _lowResHeight)
            throw new ArgumentException("Image size does not match allocated low-res texture size.");

        _gl.BindTexture(TextureTarget.Texture2D, _lowResTexture);
        fixed (byte* ptr = image)
        {
            _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, width, height,
                PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    private static void ComputeAspectViewport(uint srcW, uint srcH, uint dstW, uint dstH,
        out int vpX, out int vpY, out uint vpW, out uint vpH)
    {
        var srcAspect = (float)srcW / srcH;
        var dstAspect = (float)dstW / dstH;
        vpX = 0;
        vpY = 0;
        vpW = dstW;
        vpH = dstH;
        if (dstAspect > srcAspect)
        {
            vpW = Math.Max(1u, (uint)(dstH * srcAspect + 0.5f));
            vpX = (int)((dstW - vpW) / 2);
        }
        else if (dstAspect < srcAspect)
        {
            vpH = Math.Max(1u, (uint)(dstW / srcAspect + 0.5f));
            vpY = (int)((dstH - vpH) / 2);
        }
    }

    public void Render(uint fbWidth, uint fbHeight, bool useFSR = true, bool preserveAspect = true)
    {
        if (!useFSR)
        {
            RenderSimple(fbWidth, fbHeight, preserveAspect);
            return;
        }

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _intermediateFbo);
        _gl.Viewport(0, 0, _intermediateWidth, _intermediateHeight);
        _gl.ClearColor(0f, 0f, 0f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        _gl.UseProgram(_easuProgram);

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _lowResTexture);
        _gl.Uniform1(_easuColorTextureLoc, 0);
        _gl.Uniform2(_easuInputViewportLoc, (float)_lowResWidth, _lowResHeight);
        _gl.Uniform2(_easuInputResolutionLoc, (float)_lowResWidth, _lowResHeight);
        _gl.Uniform2(_easuOutputResolutionLoc, (float)_intermediateWidth, _intermediateHeight);

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _gl.BindVertexArray(0);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, fbWidth, fbHeight);
        _gl.ClearColor(0f, 0f, 0f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        int rcasX = 0, rcasY = 0;
        uint rcasW = fbWidth, rcasH = fbHeight;
        if (preserveAspect)
            ComputeAspectViewport(_intermediateWidth, _intermediateHeight, fbWidth, fbHeight,
                out rcasX, out rcasY, out rcasW, out rcasH);

        _gl.Viewport(rcasX, rcasY, rcasW, rcasH);
        _gl.UseProgram(_rcasProgram);

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _intermediateTexture);
        _gl.Uniform1(_rcasColorTextureLoc, 0);
        _gl.Uniform1(_rcasSharpnessLoc, _sharpness);
        _gl.Uniform2(_rcasInputResolutionLoc, (float)_intermediateWidth, _intermediateHeight);

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _gl.BindVertexArray(0);
    }

    private void RenderSimple(uint fbWidth, uint fbHeight, bool preserveAspect)
    {
        _gl.Viewport(0, 0, fbWidth, fbHeight);
        _gl.ClearColor(0f, 0f, 0f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        if (preserveAspect)
        {
            ComputeAspectViewport(_lowResWidth, _lowResHeight, fbWidth, fbHeight,
                out var vpX, out var vpY, out var vpW, out var vpH);
            _gl.Viewport(vpX, vpY, vpW, vpH);
        }

        _gl.UseProgram(_displayProgram);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _lowResTexture);
        _gl.Uniform1(_gl.GetUniformLocation(_displayProgram, "screenTexture"), 0);

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _gl.BindVertexArray(0);
    }

    public void SetSharpness(float sharpness)
    {
        _sharpness = Math.Clamp(sharpness, 0.0f, 2.0f);
    }

    private void InitDisplayShader()
    {
        var vertexShaderSource = @"
            #version 330 core
            layout (location = 0) in vec2 aPos;
            layout (location = 1) in vec2 aTexCoords;
            out vec2 TexCoords;
            void main() {
                TexCoords = aTexCoords;
                gl_Position = vec4(aPos, 0.0, 1.0);
            }";

        var fragmentShaderSource = @"
            #version 330 core
            out vec4 FragColor;
            in vec2 TexCoords;
            uniform sampler2D screenTexture;
            void main() {
                FragColor = texture(screenTexture, TexCoords);
            }";

        _displayProgram = CreateProgram(vertexShaderSource, fragmentShaderSource);
    }

    private void InitFSRShaders()
    {
        var vertexShaderSource = @"
            #version 330 core
            layout (location = 0) in vec2 aPos;
            layout (location = 1) in vec2 aTexCoords;
            out vec2 uv;
            void main() {
                uv = aTexCoords;
                gl_Position = vec4(aPos, 0.0, 1.0);
            }";

        var fsrFragmentShaderSource = @"
            #version 330 core

            in vec2 uv;
            uniform sampler2D colorTexture;
            uniform float sharpness;
            uniform vec2 inputViewport;
            uniform vec2 inputResolution;
            uniform vec2 outputResolution;
            layout(location = 0) out vec4 fragColor;

            #ifndef FSR_RCAS_LIMIT
            #define FSR_RCAS_LIMIT 0.25
            #endif

            vec3 FsrRcasLoadF(ivec2 p) {
                ivec2 dim = ivec2(inputResolution);
                return texelFetch(colorTexture, clamp(p, ivec2(0), dim - ivec2(1)), 0).rgb;
            }

            void FsrRcasCon(out float con, float sharpness) {
                con = exp2(-sharpness);
            }

            vec3 FsrRcasF(vec2 ip, float con) {
                ivec2 sp = ivec2(floor(ip));
                vec3 b = FsrRcasLoadF(sp + ivec2( 0, -1));
                vec3 d = FsrRcasLoadF(sp + ivec2(-1,  0));
                vec3 e = FsrRcasLoadF(sp);
                vec3 f = FsrRcasLoadF(sp + ivec2( 1,  0));
                vec3 h = FsrRcasLoadF(sp + ivec2( 0,  1));

                float bL = b.g + 0.5 * (b.r + b.b);
                float dL = d.g + 0.5 * (d.r + d.b);
                float eL = e.g + 0.5 * (e.r + e.b);
                float fL = f.g + 0.5 * (f.r + f.b);
                float hL = h.g + 0.5 * (h.r + h.b);

                float nz = 0.25 * (bL + dL + fL + hL) - eL;
                nz = clamp(abs(nz) / (max(max(bL, dL), max(eL, max(fL, hL))) - min(min(bL, dL), min(eL, min(fL, hL)))), 0.0, 1.0);
                nz = 1.0 - 0.5 * nz;

                vec3 mn4 = min(min(b, d), min(f, h));
                vec3 mx4 = max(max(b, d), max(f, h));

                vec2 peakC = vec2(1.0, -4.0);
                vec3 hitMin = mn4 / (4.0 * mx4);
                vec3 hitMax = (peakC.x - mx4) / (4.0 * mn4 + peakC.y);
                vec3 lobeRGB = max(-hitMin, hitMax);
                float lobe = max(-FSR_RCAS_LIMIT, min(max(lobeRGB.r, max(lobeRGB.g, lobeRGB.b)), 0.0)) * con;

                #ifdef FSR_RCAS_DENOISE
                lobe *= nz;
                #endif

                return (lobe * (b + d + h + f) + e) / (4.0 * lobe + 1.0);
            }

            vec4 RCAS(vec2 sampleUV, float sharpness) {
                float con;
                FsrRcasCon(con, sharpness);
                vec2 ip = sampleUV * inputResolution;
                vec3 outRgb = FsrRcasF(ip, con);
                return vec4(outRgb, 1.0);
            }

            vec3 FsrEasuCF(vec2 p) {
                return texture(colorTexture, clamp(p, vec2(0.0), vec2(1.0))).rgb;
            }

            void FsrEasuCon(out vec4 con0, out vec4 con1, out vec4 con2, out vec4 con3,
                            vec2 inputViewportInPixels, vec2 inputSizeInPixels, vec2 outputSizeInPixels) {
                con0 = vec4(inputViewportInPixels.x / outputSizeInPixels.x,
                            inputViewportInPixels.y / outputSizeInPixels.y,
                            0.5 * inputViewportInPixels.x / outputSizeInPixels.x - 0.5,
                            0.5 * inputViewportInPixels.y / outputSizeInPixels.y - 0.5);
                con1 = vec4(1.0 / inputSizeInPixels.x,
                            1.0 / inputSizeInPixels.y,
                            -1.0 / inputSizeInPixels.x,
                            0.0);
                con2 = vec4(-1.0 / inputSizeInPixels.x,
                            2.0 / inputSizeInPixels.y,
                            1.0 / inputSizeInPixels.x,
                            2.0 / inputSizeInPixels.y);
                con3 = vec4(0.0,
                            4.0 / inputSizeInPixels.y,
                            0.0,
                            0.0);
            }

            void FsrEasuTapF(inout vec3 aC, inout float aW, vec2 off, vec2 dir, vec2 len, float lob, float clp, vec3 c) {
                vec2 v = vec2(dot(off, dir), dot(off, vec2(-dir.y, dir.x)));
                v *= len;
                float d2 = min(dot(v, v), clp);
                float wB = 0.5 * d2 - 1.0;
                float wA = lob * d2 - 1.0;
                wB *= wB;
                wA *= wA;
                wB = 1.5625 * wB - 0.5625;
                float w = wB * wA;
                aC += c * w;
                aW += w;
            }

            void FsrEasuSetF(inout vec2 dir, inout float len, float w,
                             float lA, float lB, float lC, float lD, float lE) {
                float lenX = max(abs(lE - lC), abs(lC - lA));
                float dirX = lE - lA;
                dir.x += dirX * w;
                lenX = clamp(abs(dirX) / lenX, 0.0, 1.0);
                lenX *= lenX;
                len += lenX * w;

                float lenY = max(abs(lE - lB), abs(lB - lD));
                float dirY = lE - lD;
                dir.y += dirY * w;
                lenY = clamp(abs(dirY) / lenY, 0.0, 1.0);
                lenY *= lenY;
                len += lenY * w;
            }

            void FsrEasuF(out vec3 pix, vec2 ip, vec4 con0, vec4 con1, vec4 con2, vec4 con3) {
                vec2 pp = ip * con0.xy + con0.zw;
                vec2 fp = floor(pp);
                pp -= fp;

                vec2 p0 = fp * con1.xy + con1.zw;
                vec2 p1 = p0 + con2.xy;
                vec2 p2 = p0 + con2.zw;
                vec2 p3 = p0 + con3.xy;

                vec4 off = vec4(-0.5, 0.5, -0.5, 0.5) * con1.xxyy;

                vec3 bC = FsrEasuCF(p0 + off.xw); float bL = bC.g + 0.5 * (bC.r + bC.b);
                vec3 cC = FsrEasuCF(p0 + off.yw); float cL = cC.g + 0.5 * (cC.r + cC.b);
                vec3 iC = FsrEasuCF(p1 + off.xw); float iL = iC.g + 0.5 * (iC.r + iC.b);
                vec3 jC = FsrEasuCF(p1 + off.yw); float jL = jC.g + 0.5 * (jC.r + jC.b);
                vec3 fC = FsrEasuCF(p1 + off.yz); float fL = fC.g + 0.5 * (fC.r + fC.b);
                vec3 eC = FsrEasuCF(p1 + off.xz); float eL = eC.g + 0.5 * (eC.r + eC.b);
                vec3 kC = FsrEasuCF(p2 + off.xw); float kL = kC.g + 0.5 * (kC.r + kC.b);
                vec3 lC = FsrEasuCF(p2 + off.yw); float lL = lC.g + 0.5 * (lC.r + lC.b);
                vec3 hC = FsrEasuCF(p2 + off.yz); float hL = hC.g + 0.5 * (hC.r + hC.b);
                vec3 gC = FsrEasuCF(p2 + off.xz); float gL = gC.g + 0.5 * (gC.r + gC.b);
                vec3 oC = FsrEasuCF(p3 + off.yz); float oL = oC.g + 0.5 * (oC.r + oC.b);
                vec3 nC = FsrEasuCF(p3 + off.xz); float nL = nC.g + 0.5 * (nC.r + nC.b);

                vec2 dir = vec2(0.0);
                float lenVal = 0.0;
                FsrEasuSetF(dir, lenVal, (1.0 - pp.x) * (1.0 - pp.y), bL, eL, fL, gL, jL);
                FsrEasuSetF(dir, lenVal, pp.x * (1.0 - pp.y), cL, fL, gL, hL, kL);
                FsrEasuSetF(dir, lenVal, (1.0 - pp.x) * pp.y, fL, iL, jL, kL, nL);
                FsrEasuSetF(dir, lenVal, pp.x * pp.y, gL, jL, kL, lL, oL);

                vec2 dir2 = vec2(dir.x * dir.x, dir.y * dir.y);
                float dirR = dir2.x + dir2.y;
                float zro = dirR < (1.0 / 32768.0) ? 1.0 : 0.0;
                dirR = inversesqrt(dirR);
                dirR = zro > 0.5 ? 1.0 : dirR;
                dir.x = zro > 0.5 ? 1.0 : dir.x;
                dir.y = zro > 0.5 ? 0.0 : dir.y;
                dir *= vec2(dirR);

                lenVal = lenVal * 0.5;
                lenVal *= lenVal;
                float stretch = dot(dir, dir) / max(abs(dir.x), abs(dir.y));
                vec2 len2 = vec2(1.0 + (stretch - 1.0) * lenVal, 1.0 - 0.5 * lenVal);
                float lob = 0.5 - 0.29 * lenVal;
                float clp = 1.0 / lob;

                vec3 min4 = min(min(fC, gC), min(jC, kC));
                vec3 max4 = max(max(fC, gC), max(jC, kC));

                vec3 aC = vec3(0.0);
                float aW = 0.0;
                FsrEasuTapF(aC, aW, vec2(0.0, -1.0) - pp, dir, len2, lob, clp, bC);
                FsrEasuTapF(aC, aW, vec2(1.0, -1.0) - pp, dir, len2, lob, clp, cC);
                FsrEasuTapF(aC, aW, vec2(-1.0, 1.0) - pp, dir, len2, lob, clp, iC);
                FsrEasuTapF(aC, aW, vec2(0.0, 1.0) - pp, dir, len2, lob, clp, jC);
                FsrEasuTapF(aC, aW, vec2(0.0, 0.0) - pp, dir, len2, lob, clp, fC);
                FsrEasuTapF(aC, aW, vec2(-1.0, 0.0) - pp, dir, len2, lob, clp, eC);
                FsrEasuTapF(aC, aW, vec2(1.0, 1.0) - pp, dir, len2, lob, clp, kC);
                FsrEasuTapF(aC, aW, vec2(2.0, 1.0) - pp, dir, len2, lob, clp, lC);
                FsrEasuTapF(aC, aW, vec2(2.0, 0.0) - pp, dir, len2, lob, clp, hC);
                FsrEasuTapF(aC, aW, vec2(1.0, 0.0) - pp, dir, len2, lob, clp, gC);
                FsrEasuTapF(aC, aW, vec2(1.0, 2.0) - pp, dir, len2, lob, clp, oC);
                FsrEasuTapF(aC, aW, vec2(0.0, 2.0) - pp, dir, len2, lob, clp, nC);

                pix = min(max4, max(min4, aC / aW));
            }

            vec4 EASU(vec2 sampleUV) {
                vec4 con0, con1, con2, con3;
                FsrEasuCon(con0, con1, con2, con3, inputViewport, inputResolution, outputResolution);
                vec3 c;
                FsrEasuF(c, sampleUV * outputResolution, con0, con1, con2, con3);
                return vec4(c, 1.0);
            }

            void main() {
            #ifdef USE_RCAS
                fragColor = RCAS(vec2(1.0 - uv.x, 1.0 - uv.y), sharpness);
            #else
                fragColor = EASU(uv);
            #endif
            }";

        _easuProgram = CreateProgram(vertexShaderSource, fsrFragmentShaderSource, new[] { "USE_RCAS" }, false);
        _easuColorTextureLoc = _gl.GetUniformLocation(_easuProgram, "colorTexture");
        _easuInputViewportLoc = _gl.GetUniformLocation(_easuProgram, "inputViewport");
        _easuInputResolutionLoc = _gl.GetUniformLocation(_easuProgram, "inputResolution");
        _easuOutputResolutionLoc = _gl.GetUniformLocation(_easuProgram, "outputResolution");

        _rcasProgram = CreateProgram(vertexShaderSource, fsrFragmentShaderSource, new[] { "USE_RCAS" });
        _rcasColorTextureLoc = _gl.GetUniformLocation(_rcasProgram, "colorTexture");
        _rcasSharpnessLoc = _gl.GetUniformLocation(_rcasProgram, "sharpness");
        _rcasInputResolutionLoc = _gl.GetUniformLocation(_rcasProgram, "inputResolution");
    }

    private uint CreateProgram(string vertexSource, string fragmentSource, string[] defines = null,
        bool enableDefines = true)
    {
        var finalFragmentSource = fragmentSource;
        if (defines != null && enableDefines)
        {
            var defineStr = string.Join("\n", Array.ConvertAll(defines, d => $"#define {d}"));

            var versionPos = finalFragmentSource.IndexOf("#version", StringComparison.Ordinal);
            if (versionPos >= 0)
            {
                var lineEnd = finalFragmentSource.IndexOf('\n', versionPos);
                if (lineEnd >= 0)
                    finalFragmentSource = finalFragmentSource.Substring(0, lineEnd + 1)
                                          + defineStr + "\n"
                                          + finalFragmentSource.Substring(lineEnd + 1);
                else
                    finalFragmentSource += "\n" + defineStr + "\n";
            }
            else
            {
                finalFragmentSource = defineStr + "\n" + finalFragmentSource;
            }
        }

        var vertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vertexShader, vertexSource);
        _gl.CompileShader(vertexShader);
        CheckShaderCompile(vertexShader, "VERTEX");

        var fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fragmentShader, finalFragmentSource);
        _gl.CompileShader(fragmentShader);
        CheckShaderCompile(fragmentShader, "FRAGMENT");

        var program = _gl.CreateProgram();
        _gl.AttachShader(program, vertexShader);
        _gl.AttachShader(program, fragmentShader);
        _gl.LinkProgram(program);
        CheckProgramLink(program);

        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        return program;
    }

    private void CheckShaderCompile(uint shader, string type)
    {
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
        if (status == 0)
        {
            var log = _gl.GetShaderInfoLog(shader);
            throw new Exception($"{type} shader compilation failed: {log}");
        }
    }

    private void CheckProgramLink(uint program)
    {
        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var status);
        if (status == 0)
        {
            var log = _gl.GetProgramInfoLog(program);
            throw new Exception($"Program link failed: {log}");
        }
    }

    public void Delete()
    {
        _gl.DeleteTexture(_lowResTexture);
        _gl.DeleteTexture(_intermediateTexture);
        _gl.DeleteFramebuffer(_lowResFbo);
        _gl.DeleteFramebuffer(_intermediateFbo);
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteProgram(_displayProgram);
        _gl.DeleteProgram(_easuProgram);
        _gl.DeleteProgram(_rcasProgram);
    }
}