#version 430 core

layout(local_size_x = 256, local_size_y = 1, local_size_z = 1) in;

layout(std430, binding = 0) buffer RawDataBuffer {
    uint data[];
};

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

uniform float volOriginX, volOriginY, volOriginZ;
uniform int   volSizeX,   volSizeY,   volSizeZ;

uniform int maxSteps = 768;

// ---------- Lighting constants ----------
const vec3  SUN_DIR       = normalize(vec3(0.55, 1.0, 0.75));
const vec3  SUN_COLOR     = vec3(1.00, 0.95, 0.82);
const float SUN_AMBIENT   = 0.35;
const float SUN_DIFFUSE   = 0.70;
const float SUN_INTENSITY = 1.00;

const vec3  SKY_TOP       = vec3(0.26, 0.50, 0.92);
const vec3  SKY_HORIZON   = vec3(0.74, 0.86, 0.98);

const vec3  FOG_COLOR     = vec3(0.72, 0.83, 0.96);
const float FOG_START     = 90.0;
const float FOG_END       = 280.0;

const int ATLAS_TILES = 16;
const int TILE_SIZE   = 16;

// Shadow ray step budget. 96 corresponds to ~96 voxels of sun travel
// (enough for local shadowing in typical scenes).
const int   SHADOW_STEPS = 96;
const float AO_MIN       = 0.30;

// Precomputed shadow DDA constants (uniforms collapsed at compile time)
const vec3 SHADOW_STEP_DIR   = sign(SUN_DIR);
const vec3 SHADOW_DELTA_DIST = abs(1.0 / SUN_DIR);

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
    vec3 base = mix(SKY_HORIZON, SKY_TOP, pow(t, 0.85));
    // Sun disc + soft halo
    float sunDot = max(dot(dir, SUN_DIR), 0.0);
    float disc   = pow(sunDot, 1024.0) * 3.0;
    float halo   = pow(sunDot, 12.0)  * 0.20;
    return base + SUN_COLOR * (disc + halo);
}

bool isSolid(ivec3 p) {
    if (p.x < 0 || p.x >= volSizeX ||
        p.y < 0 || p.y >= volSizeY ||
        p.z < 0 || p.z >= volSizeZ) return false;
    return texelFetch(volumeTexture, p, 0).r != 0u;
}

// DDA shadow ray toward SUN_DIR. Returns 1.0 = lit, 0.0 = shadowed.
// Origin should already be offset from the surface.
float shadowRay(vec3 origin) {
    vec3 p = origin;
    ivec3 mapPos  = ivec3(floor(p));
    ivec3 stepDir = ivec3(SHADOW_STEP_DIR);

    vec3 sideDist = vec3(
        (SUN_DIR.x > 0.0) ? (float(mapPos.x) + 1.0 - p.x) * SHADOW_DELTA_DIST.x
                          : (p.x - float(mapPos.x)) * SHADOW_DELTA_DIST.x,
        (SUN_DIR.y > 0.0) ? (float(mapPos.y) + 1.0 - p.y) * SHADOW_DELTA_DIST.y
                          : (p.y - float(mapPos.y)) * SHADOW_DELTA_DIST.y,
        (SUN_DIR.z > 0.0) ? (float(mapPos.z) + 1.0 - p.z) * SHADOW_DELTA_DIST.z
                          : (p.z - float(mapPos.z)) * SHADOW_DELTA_DIST.z
    );

    for (int i = 0; i < SHADOW_STEPS; i++) {
        if (sideDist.x < sideDist.y) {
            if (sideDist.x < sideDist.z) { sideDist.x += SHADOW_DELTA_DIST.x; mapPos.x += stepDir.x; }
            else                          { sideDist.z += SHADOW_DELTA_DIST.z; mapPos.z += stepDir.z; }
        } else {
            if (sideDist.y < sideDist.z) { sideDist.y += SHADOW_DELTA_DIST.y; mapPos.y += stepDir.y; }
            else                          { sideDist.z += SHADOW_DELTA_DIST.z; mapPos.z += stepDir.z; }
        }

        // Left the volume → sunlight reaches this point
        if (mapPos.x < 0 || mapPos.x >= volSizeX ||
            mapPos.y < 0 || mapPos.y >= volSizeY ||
            mapPos.z < 0 || mapPos.z >= volSizeZ) return 1.0;

        if (texelFetch(volumeTexture, mapPos, 0).r != 0u) return 0.0;
    }
    return 1.0;
}

// Classic Minecraft-style corner AO
float cornerAO(bool s1, bool s2, bool d) {
    if (s1 && s2) return 0.0;
    return 1.0 - float(int(s1) + int(s2) + int(d)) / 3.0;
}

// Compute AO for the face at basePos (air-voxel adjacent to the face),
// using tangents T1/T2 and unflipped face-local UV.
// 8 unique neighbors → 4 corner AOs → bilinear blend.
float computeAO(ivec3 basePos, ivec3 T1, ivec3 T2, vec2 uv) {
    bool s_n1   = isSolid(basePos - T1);
    bool s_p1   = isSolid(basePos + T1);
    bool s_n2   = isSolid(basePos - T2);
    bool s_p2   = isSolid(basePos + T2);
    bool s_n1n2 = isSolid(basePos - T1 - T2);
    bool s_p1n2 = isSolid(basePos + T1 - T2);
    bool s_n1p2 = isSolid(basePos - T1 + T2);
    bool s_p1p2 = isSolid(basePos + T1 + T2);

    float ao00 = cornerAO(s_n1, s_n2, s_n1n2);
    float ao10 = cornerAO(s_p1, s_n2, s_p1n2);
    float ao01 = cornerAO(s_n1, s_p2, s_n1p2);
    float ao11 = cornerAO(s_p1, s_p2, s_p1p2);

    return mix(
        mix(ao00, ao10, uv.x),
        mix(ao01, ao11, uv.x),
        uv.y
    );
}

void main() {
    uint index = gl_GlobalInvocationID.x;
    if (index >= uint(screenWidth * screenHeight)) return;

    int pixelX = int(index % uint(screenWidth));
    int pixelY = int(index / uint(screenWidth));

    // ---------- Ray generation (world space) ----------
    vec2 ndc = vec2(
        (float(pixelX) + 0.5) / float(screenWidth)  * 2.0 - 1.0,
        1.0 - (float(pixelY) + 0.5) / float(screenHeight) * 2.0
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

    // ---------- Ray-AABB с объёмом ----------
    // Позволяет камере быть вне объёма и пропускает пустое пространство.
    vec3 volSize = vec3(float(volSizeX), float(volSizeY), float(volSizeZ));
    vec3 safeDir = rayDir;
    if (safeDir.x == 0.0) safeDir.x = 1e-30;
    if (safeDir.y == 0.0) safeDir.y = 1e-30;
    if (safeDir.z == 0.0) safeDir.z = 1e-30;
    vec3 invDir = 1.0 / safeDir;
    vec3 t0 = -rayPos * invDir;
    vec3 t1 = (volSize - rayPos) * invDir;
    vec3 tmin = min(t0, t1);
    vec3 tmax = max(t0, t1);
    float tEnter = max(max(tmin.x, tmin.y), max(tmin.z, 0.0));
    float tExit  = min(min(tmax.x, tmax.y), tmax.z);

    if (tExit <= tEnter) {
        data[index] = packRGBA(vec4(skyColor(rayDirWorld), 1.0));
        return;
    }

    // Продвигаем луч до точки входа в объём
    rayPos += rayDir * tEnter;
    float travelOffset = tEnter;

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
    // Верхняя граница числа пересечений границ = расстояние * sqrt(3)
    int ddaSteps = min(maxSteps, int((tExit - tEnter) * 1.732) + 3);

    for (int i = 0; i < ddaSteps; i++) {
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

    travel += travelOffset;

    uint pixelColor;

    if (hit) {
        // ---------- Face-local UV and normal ----------
        vec3  localUV;
        ivec3 ivecNormal;

        if (hitSide < 0) {
            // Камера внутри солид-вокселя — деградация
            localUV = fract(rayPos);
            hitSide = 0;
            ivecNormal = ivec3(1, 0, 0);
        } else {
            float d = (hitSide == 0) ? deltaDist.x : ((hitSide == 1) ? deltaDist.y : deltaDist.z);
            float s = (hitSide == 0) ? sideDist.x  : ((hitSide == 1) ? sideDist.y  : sideDist.z);
            localUV = fract(rayPos + rayDir * (s - d));
            ivecNormal = ivec3(
                (hitSide == 0) ? -stepDir.x : 0,
                (hitSide == 1) ? -stepDir.y : 0,
                (hitSide == 2) ? -stepDir.z : 0
            );
        }

        // ---------- Face tiles ----------
        uint faceIds    = blockFaces[min(voxelID, 255u)];
        uint sideTile   =  faceIds        & 0xFFu;
        uint topTile    = (faceIds >>  8) & 0xFFu;
        uint bottomTile = (faceIds >> 16) & 0xFFu;

        uint tile;
        vec2 faceUV;
        vec2 aoUV;
        ivec3 T1, T2;

        if (hitSide == 1) {
            faceUV = localUV.xz;
            aoUV   = localUV.xz;
            T1 = ivec3(1,0,0); T2 = ivec3(0,0,1);
            if (stepDir.y < 0) tile = topTile;
            else             { tile = bottomTile; faceUV.y = 1.0 - faceUV.y; }
        } else if (hitSide == 0) {
            faceUV = localUV.zy;
            aoUV   = localUV.zy;
            T1 = ivec3(0,0,1); T2 = ivec3(0,1,0);
            if (stepDir.x > 0) faceUV.x = 1.0 - faceUV.x;
            tile = sideTile;
        } else {
            faceUV = localUV.xy;
            aoUV   = localUV.xy;
            T1 = ivec3(1,0,0); T2 = ivec3(0,1,0);
            if (stepDir.z < 0) faceUV.x = 1.0 - faceUV.x;
            tile = sideTile;
        }

        // ---------- Texture fetch (no filtering, no bleeding) ----------
        ivec2 tileOrigin = ivec2(int(tile) % ATLAS_TILES, int(tile) / ATLAS_TILES) * TILE_SIZE;
        ivec2 texelPos   = tileOrigin + clamp(ivec2(faceUV * float(TILE_SIZE)), ivec2(0), ivec2(TILE_SIZE - 1));
        vec4  texColor   = texelFetch(atlasTexture, texelPos, 0);

        // ---------- Advanced lighting ----------
        float faceShade = (hitSide == 1) ? 1.00 : ((hitSide == 0) ? 0.78 : 0.60);

        vec3 faceNormal = vec3(ivecNormal);
        float ndl = max(dot(faceNormal, SUN_DIR), 0.0);

        // Теневая DDA (только если грань обращена к солнцу)
        float shadow = 1.0;
        if (ndl > 0.0) {
            vec3 hitPos = rayPos + rayDir * (travel - travelOffset);
            vec3 shadowOrigin = hitPos + faceNormal * 0.01;
            shadow = shadowRay(shadowOrigin);
        }

        // Ambient Occlusion
        ivec3 aoBase = mapPos + ivecNormal;
        float ao = computeAO(aoBase, T1, T2, aoUV);
        float aoFactor = mix(AO_MIN, 1.0, ao);

        // Раздельные ambient / direct каналы
        float direct  = SUN_DIFFUSE * ndl * shadow * SUN_INTENSITY;
        float ambient = SUN_AMBIENT * aoFactor;
        float light   = ambient + direct;

        vec3 rgb = texColor.rgb * faceShade * SUN_COLOR * light;

        // ---------- Fog ----------
        float fogT = clamp((travel - FOG_START) / (FOG_END - FOG_START), 0.0, 1.0);
        if (fogT > 0.0) rgb = mix(rgb, FOG_COLOR, fogT);

        pixelColor = packRGBA(vec4(rgb, texColor.a));
    } else {
        // ---------- Sky ----------
        pixelColor = packRGBA(vec4(skyColor(rayDirWorld), 1.0));
    }

    data[index] = pixelColor;
}