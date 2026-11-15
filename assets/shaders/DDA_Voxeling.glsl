#version 430 core

layout(local_size_x = 256, local_size_y = 1, local_size_z = 1) in;

layout(std430, binding = 0) buffer RawDataBuffer {
    uint data[];
};

// packed: side | top << 8 | bottom << 16
layout(std430, binding = 1) buffer BlockFacesBuffer {
    uint blockFaces[256];
};

// ---------- Uniforms ----------
uniform int screenWidth;
uniform int screenHeight;

uniform vec3  cameraPos;
uniform vec3  cameraFront;
uniform vec3  cameraUp;
uniform vec3  cameraRight;
uniform float fov;

uniform usampler3D volumeTexture;
uniform sampler2D   atlasTexture;

// Volume geometry. World → volume space: p_vol = p_world - volOrigin
uniform float volOriginX, volOriginY, volOriginZ;
uniform int   volSizeX,   volSizeY,   volSizeZ;

uniform int maxSteps = 768;

// ---------- Tweaking constants ----------
const vec3  SUN_DIR     = normalize(vec3(0.55, 1.0, 0.75)); // toward the sun
const float SUN_AMBIENT = 0.38;
const float SUN_DIFFUSE = 0.62;

const vec3  SKY_TOP     = vec3(0.26, 0.50, 0.92);
const vec3  SKY_HORIZON = vec3(0.74, 0.86, 0.98);

const vec3  FOG_COLOR   = vec3(0.72, 0.83, 0.96);
const float FOG_START   = 90.0;
const float FOG_END     = 280.0;

const int ATLAS_TILES = 16;
const int TILE_SIZE   = 16;

// ---------- Helpers ----------
uint packRGBA(vec4 c) {
    uint r = uint(clamp(c.r, 0.0, 1.0) * 255.0 + 0.5);
    uint g = uint(clamp(c.g, 0.0, 1.0) * 255.0 + 0.5);
    uint b = uint(clamp(c.b, 0.0, 1.0) * 255.0 + 0.5);
    uint a = uint(clamp(c.a, 0.0, 1.0) * 255.0 + 0.5);
    return (a << 24) | (b << 16) | (g << 8) | r;
}

vec3 skyColor(vec3 dir) {
    float t = clamp(dir.y * 0.5 + 0.5, 0.0, 1.0);
    return mix(SKY_HORIZON, SKY_TOP, pow(t, 0.85));
}

void main() {
    uint index = gl_GlobalInvocationID.x;
    if (index >= uint(screenWidth * screenHeight)) return;

    int pixelX = int(index % uint(screenWidth));
    int pixelY = int(index / uint(screenWidth));

    // ---------- Ray generation (world space) ----------
    vec2 ndc = vec2(
        (float(pixelX) + 0.5) / float(screenWidth)  * 2.0 - 1.0,
        1.0 - (float(pixelY) + 0.5) / float(screenHeight) * 2.0   // ← Y инвертирован
    );
    float aspect     = float(screenWidth) / float(screenHeight);
    float tanHalfFov = tan(radians(fov) * 0.5);

    vec3 rayDirWorld = normalize(
        cameraFront +
        cameraRight * (ndc.x * aspect * tanHalfFov) +
        cameraUp    * (ndc.y        * tanHalfFov)
    );

    // ---------- To volume space ----------
    vec3 volOrigin = vec3(volOriginX, volOriginY, volOriginZ);
    vec3 rayPos    = cameraPos - volOrigin;
    vec3 rayDir    = rayDirWorld;

    // ---------- DDA setup ----------
    ivec3 mapPos  = ivec3(floor(rayPos));
    ivec3 stepDir = ivec3(sign(rayDir));

    vec3 deltaDist = vec3(
        (rayDir.x == 0.0) ? 1e30 : abs(1.0 / rayDir.x),
        (rayDir.y == 0.0) ? 1e30 : abs(1.0 / rayDir.y),
        (rayDir.z == 0.0) ? 1e30 : abs(1.0 / rayDir.z)
    );

    vec3 sideDist;
    sideDist.x = (rayDir.x > 0.0) ? (float(mapPos.x) + 1.0 - rayPos.x) * deltaDist.x
                                  : (rayPos.x - float(mapPos.x)) * deltaDist.x;
    sideDist.y = (rayDir.y > 0.0) ? (float(mapPos.y) + 1.0 - rayPos.y) * deltaDist.y
                                  : (rayPos.y - float(mapPos.y)) * deltaDist.y;
    sideDist.z = (rayDir.z > 0.0) ? (float(mapPos.z) + 1.0 - rayPos.z) * deltaDist.z
                                  : (rayPos.z - float(mapPos.z)) * deltaDist.z;

    uint  voxelID = 0u;
    int   hitSide = -1;
    bool  hit     = false;
    float travel  = 0.0;

    // ---------- DDA loop ----------
    for (int i = 0; i < maxSteps; i++) {
        // Volume bounds check (X, Y and Z — not just Y)
        if (mapPos.x < 0 || mapPos.x >= volSizeX ||
            mapPos.y < 0 || mapPos.y >= volSizeY ||
            mapPos.z < 0 || mapPos.z >= volSizeZ) break;

        uint id = texelFetch(volumeTexture, mapPos, 0).r;
        if (id != 0u) { voxelID = id; hit = true; break; }

        if (sideDist.x < sideDist.y) {
            if (sideDist.x < sideDist.z) {
                travel = sideDist.x; sideDist.x += deltaDist.x;
                mapPos.x += stepDir.x; hitSide = 0;
            } else {
                travel = sideDist.z; sideDist.z += deltaDist.z;
                mapPos.z += stepDir.z; hitSide = 2;
            }
        } else {
            if (sideDist.y < sideDist.z) {
                travel = sideDist.y; sideDist.y += deltaDist.y;
                mapPos.y += stepDir.y; hitSide = 1;
            } else {
                travel = sideDist.z; sideDist.z += deltaDist.z;
                mapPos.z += stepDir.z; hitSide = 2;
            }
        }
    }

    uint pixelColor;

    if (hit) {
        // ---------- Face-local UV ----------
        vec3 localUV;
        if (hitSide < 0) {
            // camera inside solid voxel — degrade gracefully
            localUV = fract(rayPos);
            hitSide = 0;
        } else {
            float d = (hitSide == 0) ? deltaDist.x : ((hitSide == 1) ? deltaDist.y : deltaDist.z);
            float s = (hitSide == 0) ? sideDist.x  : ((hitSide == 1) ? sideDist.y  : sideDist.z);
            localUV = fract(rayPos + rayDir * (s - d));
        }

        uint faceIds    = blockFaces[min(voxelID, 255u)];
        uint sideTile   =  faceIds        & 0xFFu;
        uint topTile    = (faceIds >>  8) & 0xFFu;
        uint bottomTile = (faceIds >> 16) & 0xFFu;

        uint tile;
        vec2 faceUV;

        if (hitSide == 1) {
            faceUV = localUV.xz;
            if (stepDir.y < 0) tile = topTile;
            else             { tile = bottomTile; faceUV.y = 1.0 - faceUV.y; }
        } else if (hitSide == 0) {
            faceUV = localUV.zy;
            if (stepDir.x > 0) faceUV.x = 1.0 - faceUV.x;
            tile = sideTile;
        } else {
            faceUV = localUV.xy;
            if (stepDir.z < 0) faceUV.x = 1.0 - faceUV.x;
            tile = sideTile;
        }

        // Integer texel fetch — zero filtering, no atlas bleeding
        ivec2 tileOrigin = ivec2(int(tile) % ATLAS_TILES, int(tile) / ATLAS_TILES) * TILE_SIZE;
        ivec2 texelPos   = tileOrigin + clamp(ivec2(faceUV * float(TILE_SIZE)), ivec2(0), ivec2(TILE_SIZE - 1));
        vec4  texColor   = texelFetch(atlasTexture, texelPos, 0);

        // ---------- Shading ----------
        float faceShade = (hitSide == 1) ? 1.00 : ((hitSide == 0) ? 0.78 : 0.60);

        // Outward-facing normal of the surface the ray just hit
        vec3 faceNormal = vec3(
            (hitSide == 0) ? -float(stepDir.x) : 0.0,
            (hitSide == 1) ? -float(stepDir.y) : 0.0,
            (hitSide == 2) ? -float(stepDir.z) : 0.0
        );
        float ndl = max(dot(faceNormal, SUN_DIR), 0.0);
        float sun = SUN_AMBIENT + SUN_DIFFUSE * ndl;

        vec3 rgb = texColor.rgb * faceShade * sun;

        // ---------- Distance fog ----------
        float fogT = clamp((travel - FOG_START) / (FOG_END - FOG_START), 0.0, 1.0);
        if (fogT > 0.0) rgb = mix(rgb, FOG_COLOR, fogT);

        pixelColor = packRGBA(vec4(rgb, texColor.a));
    } else {
        // ---------- Sky ----------
        pixelColor = packRGBA(vec4(skyColor(rayDirWorld), 1.0));
    }

    data[index] = pixelColor;
}