#version 430 core

layout(local_size_x = 256, local_size_y = 1, local_size_z = 1) in;

layout(std430, binding = 0) buffer RawDataBuffer {
    uint data[];
};

// packed: side | top << 8 | bottom << 16
layout(std430, binding = 1) buffer BlockFacesBuffer {
    uint blockFaces[256];
};

const int MAP_SIZE_X = 32;
const int MAP_SIZE_Y = 32;
const int MAP_SIZE_Z = 32;

uniform int screenWidth;
uniform int screenHeight;

uniform vec3 cameraPos;
uniform vec3 cameraFront;
uniform vec3 cameraUp;
uniform vec3 cameraRight;
uniform float fov;

uniform usampler3D volumeTexture;
uniform sampler2D atlasTexture;

uniform int maxSteps = 512;

const int ATLAS_TILES = 16;
const int TILE_SIZE   = 16;

void main() {
    uint index = gl_GlobalInvocationID.x;
    if (index >= uint(screenWidth * screenHeight)) return;

    int pixelX = int(index % uint(screenWidth));
    int pixelY = int(index / uint(screenWidth));

    vec2 ndc = vec2(
        (float(pixelX) + 0.5) / float(screenWidth)  * 2.0 - 1.0,
        (float(pixelY) + 0.5) / float(screenHeight) * 2.0 - 1.0
    );

    float aspect     = float(screenWidth) / float(screenHeight);
    float tanHalfFov = tan(radians(fov) * 0.5);

    vec3 rayDir = normalize(
        cameraFront +
        cameraRight * (ndc.x * aspect * tanHalfFov) +
        cameraUp    * (ndc.y        * tanHalfFov)
    );

    vec3  rayPos  = cameraPos;
    ivec3 stepDir = ivec3(sign(rayDir));
    ivec3 mapPos  = ivec3(floor(rayPos));

    vec3 deltaDist = vec3(
        (rayDir.x == 0.0) ? 1e30 : abs(1.0 / rayDir.x),
        (rayDir.y == 0.0) ? 1e30 : abs(1.0 / rayDir.y),
        (rayDir.z == 0.0) ? 1e30 : abs(1.0 / rayDir.z)
    );

    vec3 sideDist;
    sideDist.x = (rayDir.x > 0.0) ? (float(mapPos.x) + 1.0 - rayPos.x) * deltaDist.x : (rayPos.x - float(mapPos.x)) * deltaDist.x;
    sideDist.y = (rayDir.y > 0.0) ? (float(mapPos.y) + 1.0 - rayPos.y) * deltaDist.y : (rayPos.y - float(mapPos.y)) * deltaDist.y;
    sideDist.z = (rayDir.z > 0.0) ? (float(mapPos.z) + 1.0 - rayPos.z) * deltaDist.z : (rayPos.z - float(mapPos.z)) * deltaDist.z;

    uint voxelID = 0u;
    int  hitSide = -1;
    bool hit     = false;

    for (int i = 0; i < maxSteps; i++) {
        if (mapPos.x >= 0 && mapPos.x < MAP_SIZE_X &&
            mapPos.y >= 0 && mapPos.y < MAP_SIZE_Y &&
            mapPos.z >= 0 && mapPos.z < MAP_SIZE_Z)
        {
            uint id = texelFetch(volumeTexture, mapPos, 0).r;
            if (id != 0u) { voxelID = id; hit = true; break; }
        } else if (mapPos.y < 0 || mapPos.y >= MAP_SIZE_Y) {
            break;
        }

        if (sideDist.x < sideDist.y) {
            if (sideDist.x < sideDist.z) { sideDist.x += deltaDist.x; mapPos.x += stepDir.x; hitSide = 0; }
            else                         { sideDist.z += deltaDist.z; mapPos.z += stepDir.z; hitSide = 2; }
        } else {
            if (sideDist.y < sideDist.z) { sideDist.y += deltaDist.y; mapPos.y += stepDir.y; hitSide = 1; }
            else                         { sideDist.z += deltaDist.z; mapPos.z += stepDir.z; hitSide = 2; }
        }
    }

    uint pixelColor = 0xFF1A1A1Au;

    if (hit) {
        // entry distance along the last-crossed axis is sideDist - deltaDist
        vec3 localUV;
        if (hitSide < 0) {
            // camera spawned inside a solid voxel — degrade gracefully
            localUV = fract(rayPos);
            hitSide = 0;
        } else {
            float d = (hitSide == 0) ? deltaDist.x : ((hitSide == 1) ? deltaDist.y : deltaDist.z);
            float s = (hitSide == 0) ? sideDist.x  : ((hitSide == 1) ? sideDist.y  : sideDist.z);
            localUV = fract(rayPos + rayDir * (s - d));
        }

        uint faceIds    = blockFaces[voxelID];
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

        // integer texel coords — zero filtering cost, no bleeding between atlas tiles
        ivec2 tileOrigin = ivec2(int(tile) % ATLAS_TILES, int(tile) / ATLAS_TILES) * TILE_SIZE;
        ivec2 texelPos   = tileOrigin + clamp(ivec2(faceUV * float(TILE_SIZE)), ivec2(0), ivec2(TILE_SIZE - 1));
        vec4  texColor   = texelFetch(atlasTexture, texelPos, 0);

        float shade = (hitSide == 1) ? 1.0 : ((hitSide == 0) ? 0.75 : 0.55);

        uint r = uint(texColor.r * 255.0 * shade);
        uint g = uint(texColor.g * 255.0 * shade);
        uint b = uint(texColor.b * 255.0 * shade);
        uint a = uint(texColor.a * 255.0);

        pixelColor = (a << 24) | (b << 16) | (g << 8) | r;
    }

    data[index] = pixelColor;
}