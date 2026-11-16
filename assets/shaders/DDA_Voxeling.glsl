#version 430 core

layout(local_size_x = 256, local_size_y = 1, local_size_z = 1) in;

layout(std430, binding = 0) buffer RawDataBuffer {
    uint data[];
};

layout(std430, binding = 1) buffer BlockFacesBuffer {
    uint blockFaces[256];
};

// ---------- Uniforms ----------
uniform int   screenWidth;
uniform int   screenHeight;
uniform float uTime; 

uniform int   uBreakX;
uniform int   uBreakY;
uniform int   uBreakZ;
uniform float uBreakProgress;

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

// ---------- Day/Night Uniforms ----------
uniform vec3 uSunDir;
uniform vec3 uSkyTop;
uniform vec3 uSkyHorizon;
uniform vec3 uSunColor;
uniform vec3 uAmbientColor;

// ---------- Constants ----------
const uint WATER_ID = 12u; 

const float FOG_DENSITY = 0.008;
const int   ATLAS_TILES = 16;
const int   TILE_SIZE   = 16;

const int   SHADOW_STEPS = 64; 
const float AO_MIN       = 0.30;

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
    vec3 base = mix(uSkyHorizon, uSkyTop, pow(t, 0.85));

    float sunDot = max(dot(dir, uSunDir), 0.0);
    float sunDisc = pow(sunDot, 1024.0) * 3.0;
    float sunHalo = pow(sunDot, 12.0)  * 0.20;
    
    float moonDot = max(dot(dir, -uSunDir), 0.0);
    float moonDisc = pow(moonDot, 2048.0) * 4.0;
    
    float nightFactor = smoothstep(0.0, -0.2, uSunDir.y);
    float star = 0.0;
    if (nightFactor > 0.0) {
        vec3 p = dir * 500.0;
        vec3 f = fract(p) - 0.5;
        float d = dot(f, f);
        float hash = fract(sin(dot(floor(p), vec3(12.9898, 78.233, 45.164))) * 43758.5453);
        star = smoothstep(0.05, 0.0, d) * step(0.995, hash) * nightFactor;
    }

    vec3 color = base;
    color += uSunColor * (sunDisc + sunHalo);
    color += vec3(0.9, 0.9, 1.0) * moonDisc;
    color += vec3(1.0) * star;
    return color;
}

bool isSolid(ivec3 p) {
    if (p.x < 0 || p.x >= volSizeX || p.y < 0 || p.y >= volSizeY || p.z < 0 || p.z >= volSizeZ) return false;
    uint id = texelFetch(volumeTexture, p, 0).r;
    return id != 0u && id != WATER_ID; // 0 = воздух, WATER_ID = вода
}
float shadowRay(vec3 origin) {
    vec3 p = origin;
    ivec3 mapPos  = ivec3(floor(p));
    ivec3 stepDir = ivec3(sign(uSunDir));
    vec3 deltaDist = abs(1.0 / uSunDir);

    vec3 sideDist = vec3(
        (uSunDir.x > 0.0) ? (float(mapPos.x) + 1.0 - p.x) * deltaDist.x : (p.x - float(mapPos.x)) * deltaDist.x,
        (uSunDir.y > 0.0) ? (float(mapPos.y) + 1.0 - p.y) * deltaDist.y : (p.y - float(mapPos.y)) * deltaDist.y,
        (uSunDir.z > 0.0) ? (float(mapPos.z) + 1.0 - p.z) * deltaDist.z : (p.z - float(mapPos.z)) * deltaDist.z
    );

    for (int i = 0; i < SHADOW_STEPS; i++) {
        if (sideDist.x < sideDist.y) {
            if (sideDist.x < sideDist.z) { sideDist.x += deltaDist.x; mapPos.x += stepDir.x; }
            else                          { sideDist.z += deltaDist.z; mapPos.z += stepDir.z; }
        } else {
            if (sideDist.y < sideDist.z) { sideDist.y += deltaDist.y; mapPos.y += stepDir.y; }
            else                          { sideDist.z += deltaDist.z; mapPos.z += stepDir.z; }
        }

        if (mapPos.x < 0 || mapPos.x >= volSizeX || mapPos.y < 0 || mapPos.y >= volSizeY || mapPos.z < 0 || mapPos.z >= volSizeZ) return 1.0;
        
        if (isSolid(mapPos)) return 0.0; 
    }
    return 1.0;
}

float cornerAO(bool s1, bool s2, bool d) {
    if (s1 && s2) return 0.0;
    return 1.0 - float(int(s1) + int(s2) + int(d)) / 3.0;
}

float computeAO(ivec3 basePos, ivec3 T1, ivec3 T2, vec2 uv) {
    bool s_n1   = isSolid(basePos - T1); bool s_p1   = isSolid(basePos + T1);
    bool s_n2   = isSolid(basePos - T2); bool s_p2   = isSolid(basePos + T2);
    bool s_n1n2 = isSolid(basePos - T1 - T2); bool s_p1n2 = isSolid(basePos + T1 - T2);
    bool s_n1p2 = isSolid(basePos - T1 + T2); bool s_p1p2 = isSolid(basePos + T1 + T2);

    float ao00 = cornerAO(s_n1, s_n2, s_n1n2); float ao10 = cornerAO(s_p1, s_n2, s_p1n2);
    float ao01 = cornerAO(s_n1, s_p2, s_n1p2); float ao11 = cornerAO(s_p1, s_p2, s_p1p2);
    return mix(mix(ao00, ao10, uv.x), mix(ao01, ao11, uv.x), uv.y);
}

vec3 getLighting(vec3 normal, float ao, float ndl, float shadow) {
    vec3 ambient = uAmbientColor * ao;
    vec3 direct = uSunColor * (ndl * shadow);
    return ambient + direct;
}

void main() {
    uint index = gl_GlobalInvocationID.x;
    if (index >= uint(screenWidth * screenHeight)) return;

    int pixelX = int(index % uint(screenWidth));
    int pixelY = int(index / uint(screenWidth));

    vec2 ndc = vec2((float(pixelX) + 0.5) / float(screenWidth)  * 2.0 - 1.0, 1.0 - (float(pixelY) + 0.5) / float(screenHeight) * 2.0);
    float aspect = float(screenWidth) / float(screenHeight);
    vec3 rayDirWorld = normalize(cameraFront + cameraRight * (ndc.x * aspect * tan(radians(fov) * 0.5)) + cameraUp * (ndc.y * tan(radians(fov) * 0.5)));

    vec3 volOrigin = vec3(volOriginX, volOriginY, volOriginZ);
    vec3 rayPos    = cameraPos - volOrigin;
    vec3 rayDir    = rayDirWorld;

    // Ray-AABB
    vec3 volSize = vec3(float(volSizeX), float(volSizeY), float(volSizeZ));
    vec3 safeDir = rayDir; if (safeDir.x == 0.0) safeDir.x = 1e-30; if (safeDir.y == 0.0) safeDir.y = 1e-30; if (safeDir.z == 0.0) safeDir.z = 1e-30;
    vec3 invDir = 1.0 / safeDir; vec3 t0 = -rayPos * invDir; vec3 t1 = (volSize - rayPos) * invDir;
    vec3 tmin = min(t0, t1); vec3 tmax = max(t0, t1);
    float tEnter = max(max(tmin.x, tmin.y), max(tmin.z, 0.0)); float tExit  = min(min(tmax.x, tmax.y), tmax.z);

    if (tExit <= tEnter) { data[index] = packRGBA(vec4(skyColor(rayDirWorld), 1.0)); return; }

    rayPos += rayDir * tEnter;
    float travelOffset = tEnter;

    // DDA setup
    ivec3 mapPos  = ivec3(floor(rayPos));
    ivec3 stepDir = ivec3(sign(rayDir));
    vec3 deltaDist = vec3((rayDir.x == 0.0) ? 1e30 : abs(1.0 / rayDir.x), (rayDir.y == 0.0) ? 1e30 : abs(1.0 / rayDir.y), (rayDir.z == 0.0) ? 1e30 : abs(1.0 / rayDir.z));
    vec3 sideDist;
    sideDist.x = (rayDir.x > 0.0) ? (float(mapPos.x) + 1.0 - rayPos.x) * deltaDist.x : (rayPos.x - float(mapPos.x)) * deltaDist.x;
    sideDist.y = (rayDir.y > 0.0) ? (float(mapPos.y) + 1.0 - rayPos.y) * deltaDist.y : (rayPos.y - float(mapPos.y)) * deltaDist.y;
    sideDist.z = (rayDir.z > 0.0) ? (float(mapPos.z) + 1.0 - rayPos.z) * deltaDist.z : (rayPos.z - float(mapPos.z)) * deltaDist.z;

    vec4 accumulatedColor = vec4(0.0);
    bool hitSolid = false;
    int  hitSide  = -1;
    float travel  = 0.0;
    int ddaSteps = min(maxSteps, int((tExit - tEnter) * 1.732) + 3);

    // ---------- DDA loop ----------
    for (int i = 0; i < ddaSteps; i++) {
        if (mapPos.x < 0 || mapPos.x >= volSizeX || mapPos.y < 0 || mapPos.y >= volSizeY || mapPos.z < 0 || mapPos.z >= volSizeZ) break;

        uint voxelID = texelFetch(volumeTexture, mapPos, 0).r;
        bool isWater = (voxelID == WATER_ID);

        if (voxelID != 0u) {
            bool insideBlock = (hitSide < 0);
            vec3 localUV;
            ivec3 ivecNormal;
            
            if (insideBlock) { 
                localUV = fract(rayPos); 
                hitSide = 0; 
                ivecNormal = ivec3(1, 0, 0); 
            } else {
                float d = (hitSide == 0) ? deltaDist.x : ((hitSide == 1) ? deltaDist.y : deltaDist.z);
                float s = (hitSide == 0) ? sideDist.x  : ((hitSide == 1) ? sideDist.y  : sideDist.z);
                localUV = fract(rayPos + rayDir * (s - d));
                ivecNormal = ivec3((hitSide == 0) ? -stepDir.x : 0, (hitSide == 1) ? -stepDir.y : 0, (hitSide == 2) ? -stepDir.z : 0);
            }

            // 1) ОБЪЕМНАЯ ВОДА: Добавляем цвет за каждый блок воды
            if (isWater) {
                float absorb = 0.03; // Прозрачность на 1 блок глубины (0.03 = 3%)
                vec3 waterVolColor = vec3(0.1, 0.35, 0.6) * (uAmbientColor + uSunColor * 0.5);
                accumulatedColor.rgb += (1.0 - accumulatedColor.a) * waterVolColor * absorb;
                accumulatedColor.a   += (1.0 - accumulatedColor.a) * absorb;
            }

            // 2) ПРОПУСК ВНУТРЕННИХ ГРАНЕЙ
            bool skipFace = false;
            if (isWater) {
                if (insideBlock) {
                    skipFace = true; // Камера внутри воды — не рисуем стену
                } else {
                    ivec3 neighborPos = mapPos + ivecNormal;
                    if (neighborPos.x >= 0 && neighborPos.x < volSizeX && 
                        neighborPos.y >= 0 && neighborPos.y < volSizeY && 
                        neighborPos.z >= 0 && neighborPos.z < volSizeZ) {
                        // Если сосед тоже вода — это внутренняя грань, пропускаем
                        if (texelFetch(volumeTexture, neighborPos, 0).r == WATER_ID) {
                            skipFace = true;
                        }
                    }
                }
            }

            // 3) РЕНДЕР ГРАНИ (если не пропущена)
            if (!skipFace) {
                uint faceIds = blockFaces[min(voxelID, 255u)];
                uint sideTile = faceIds & 0xFFu, topTile = (faceIds >> 8) & 0xFFu, bottomTile = (faceIds >> 16) & 0xFFu;
                uint tile; vec2 faceUV; vec2 aoUV; ivec3 T1, T2;

                if (hitSide == 1) {
                    faceUV = localUV.xz; aoUV = localUV.xz; T1 = ivec3(1,0,0); T2 = ivec3(0,0,1);
                    tile = (stepDir.y < 0) ? topTile : bottomTile;
                    if (stepDir.y > 0) faceUV.y = 1.0 - faceUV.y;
                } else if (hitSide == 0) {
                    faceUV = localUV.zy; aoUV = localUV.zy; T1 = ivec3(0,0,1); T2 = ivec3(0,1,0);
                    tile = sideTile; if (stepDir.x > 0) faceUV.x = 1.0 - faceUV.x;
                } else {
                    faceUV = localUV.xy; aoUV = localUV.xy; T1 = ivec3(1,0,0); T2 = ivec3(0,1,0);
                    tile = sideTile; if (stepDir.z < 0) faceUV.x = 1.0 - faceUV.x;
                }

                if (isWater) {
                    float wTime = uTime * 0.5;
                    faceUV += vec2(sin(wTime + localUV.y * 10.0) * 0.05, cos(wTime + localUV.x * 10.0) * 0.05);
                }

                ivec2 tileOrigin = ivec2(int(tile) % ATLAS_TILES, int(tile) / ATLAS_TILES) * TILE_SIZE;
                ivec2 texelPos   = tileOrigin + clamp(ivec2(faceUV * float(TILE_SIZE)), ivec2(0), ivec2(TILE_SIZE - 1));
                vec4 texColor    = texelFetch(atlasTexture, texelPos, 0);

                float faceShade = (hitSide == 1) ? 1.00 : ((hitSide == 0) ? 0.78 : 0.60);
                vec3 faceNormal = vec3(ivecNormal);
                float ndl = max(dot(faceNormal, uSunDir), 0.0);
                
                float shadow = 1.0;
                if (ndl > 0.0 && !isWater) {
                    vec3 hitPos = rayPos + rayDir * (travel - travelOffset);
                    shadow = shadowRay(hitPos + faceNormal * 0.01);
                }

                float ao = 1.0;
                if (!isWater) ao = computeAO(mapPos + ivecNormal, T1, T2, aoUV);

                vec3 litColor = texColor.rgb * faceShade * getLighting(faceNormal, ao, ndl, shadow);
                
                // ---------- ПРОЦЕДУРНЫЕ ТРЕЩИНЫ ----------
                if (uBreakProgress > 0.0 && mapPos.x == uBreakX && mapPos.y == uBreakY && mapPos.z == uBreakZ) {
                    // Разбиваем грань на сетку 8x8 (пиксельные трещины)
                    vec2 crackUV = floor(faceUV * 8.0);
                    // Псевдослучайный шум
                    float noise = fract(sin(dot(crackUV, vec2(12.9898, 78.233))) * 43758.5453);
                    
                    // Чем больше прогресс, тем больше "пикселей" становятся трещинами
                    if (noise < uBreakProgress * 1.5) {
                        litColor = mix(litColor, vec3(0.05, 0.05, 0.05), 0.9); // Тёмная трещина
                    } else {
                        litColor *= (1.0 - uBreakProgress * 0.4); // Общее легкое затемнение блока
                    }
                }

                if (isWater) {
                    vec3 viewDir = normalize(cameraPos - (rayPos + volOrigin + rayDir * travel));
                    vec3 halfDir = normalize(uSunDir + viewDir);
                    float spec = pow(max(dot(faceNormal, halfDir), 0.0), 64.0);
                    litColor += uSunColor * spec * 1.5;
                }

                float alpha = texColor.a;
                if (isWater) {
                    accumulatedColor.rgb += (1.0 - accumulatedColor.a) * litColor * alpha;
                    accumulatedColor.a   += (1.0 - accumulatedColor.a) * alpha;
                    if (accumulatedColor.a > 0.95) { hitSolid = true; break; } // Больше не нужно ограничение i > 64
                } else {
                    accumulatedColor.rgb += (1.0 - accumulatedColor.a) * litColor;
                    accumulatedColor.a   = 1.0;
                    hitSolid = true;
                    break;
                }
            }
        }

        // Шаг DDA
        if (sideDist.x < sideDist.y) {
            if (sideDist.x < sideDist.z) { travel = sideDist.x; sideDist.x += deltaDist.x; mapPos.x += stepDir.x; hitSide = 0; }
            else                          { travel = sideDist.z; sideDist.z += deltaDist.z; mapPos.z += stepDir.z; hitSide = 2; }
        } else {
            if (sideDist.y < sideDist.z) { travel = sideDist.y; sideDist.y += deltaDist.y; mapPos.y += stepDir.y; hitSide = 1; }
            else                          { travel = sideDist.z; sideDist.z += deltaDist.z; mapPos.z += stepDir.z; hitSide = 2; }
        }
    }

    travel += travelOffset;
    uint pixelColor;

    if (hitSolid) {
        vec3 rgb = accumulatedColor.rgb / max(accumulatedColor.a, 0.001);
        float fogT = 1.0 - exp(-pow(travel * FOG_DENSITY, 2.0)); 
        rgb = mix(rgb, uSkyHorizon, fogT);
        pixelColor = packRGBA(vec4(rgb, 1.0));
    } else {
        vec3 sky = skyColor(rayDirWorld);
        vec3 rgb = accumulatedColor.rgb + (1.0 - accumulatedColor.a) * sky;
        float fogT = 1.0 - exp(-pow(travel * FOG_DENSITY, 2.0));
        rgb = mix(rgb, uSkyHorizon, fogT);
        pixelColor = packRGBA(vec4(rgb, 1.0));
    }

    data[index] = pixelColor;
}