#version 430 core

// Local workgroup settings (256 threads in a row)
layout(local_size_x = 256, local_size_y = 1, local_size_z = 1) in;

// Output buffer structure
layout(std430, binding = 0) buffer RawDataBuffer {
    uint data[];
};

// World dimension constants
const int MAP_SIZE_X = 32;
const int MAP_SIZE_Y = 32;
const int MAP_SIZE_Z = 32;

// Screen resolution uniforms (required to compute rays from a 1D index)
uniform int screenWidth;
uniform int screenHeight;

// Camera uniforms
uniform vec3 cameraPos;
uniform vec3 cameraFront;
uniform vec3 cameraUp;
uniform vec3 cameraRight;
uniform float fov;

// Voxel texture (in GL_R32UI or a similar format to store block IDs)
uniform sampler3D volumeTexture; 

// Maximum distance/steps setting to prevent infinite loops
uniform int maxSteps = 512;

void main() {
    // Get the global thread ID
    uint index = gl_GlobalInvocationID.x;

    // Protection against out-of-bounds frame buffer access
    if (index >= uint(screenWidth * screenHeight)) {
        return;
    }

    // Reconstruct 2D pixel coordinates (X, Y) from the 1D index
    int pixelX = int(index % uint(screenWidth));
    int pixelY = int(index / uint(screenWidth));

    // Convert screen coordinates to the [-1, 1] range (Normalized Device Coordinates)
    vec2 ndc = vec2(
        (float(pixelX) + 0.5) / float(screenWidth) * 2.0 - 1.0,
        (float(pixelY) + 0.5) / float(screenHeight) * 2.0 - 1.0
    );

    // Account for Aspect Ratio
    float aspect = float(screenWidth) / float(screenHeight);
    float tanHalfFov = tan(radians(fov) * 0.5);

    // Compute the ray direction for the current pixel
    vec3 rayDir = normalize(
        cameraFront + 
        cameraRight * (ndc.x * aspect * tanHalfFov) + 
        cameraUp * (ndc.y * tanHalfFov)
    );

    // Initial ray position
    vec3 rayPos = cameraPos;

    // Step direction along the axes (+1 or -1)
    ivec3 stepDir = ivec3(sign(rayDir));

    // Current integer coordinates of the voxel
    ivec3 mapPos = ivec3(floor(rayPos));

    // Compute delta — the distance the ray needs to travel along each axis 
    // to cover a distance equal to the size of exactly one voxel (1.0)
    vec3 deltaDist = vec3(
        (rayDir.x == 0.0) ? 1e30 : abs(1.0 / rayDir.x),
        (rayDir.y == 0.0) ? 1e30 : abs(1.0 / rayDir.y),
        (rayDir.z == 0.0) ? 1e30 : abs(1.0 / rayDir.z)
    );

    // Compute the initial sideDist values — distances to the nearest voxel faces
    vec3 sideDist;
    if (rayDir.x > 0.0) {
        sideDist.x = (float(mapPos.x) + 1.0 - rayPos.x) * deltaDist.x;
    } else {
        sideDist.x = (rayPos.x - float(mapPos.x)) * deltaDist.x;
    }
    if (rayDir.y > 0.0) {
        sideDist.y = (float(mapPos.y) + 1.0 - rayPos.y) * deltaDist.y;
    } else {
        sideDist.y = (rayPos.y - float(mapPos.y)) * deltaDist.y;
    }
    if (rayDir.z > 0.0) {
        sideDist.z = (float(mapPos.z) + 1.0 - rayPos.z) * deltaDist.z;
    } else {
        sideDist.z = (rayPos.z - float(mapPos.z)) * deltaDist.z;
    }

    // Variables to store intersection results
    uint voxelID = 0u;
    int hitSide = 0; // 0 = X, 1 = Y, 2 = Z (needed for simple lighting/shadows)
    bool hit = false;

    // DDA (Digital Differential Analysis) raycasting loop
    for (int i = 0; i < maxSteps; i++) {
        // Check map boundaries before sampling the texture
        if (mapPos.x >= 0 && mapPos.x < MAP_SIZE_X &&
            mapPos.y >= 0 && mapPos.y < MAP_SIZE_Y &&
            mapPos.z >= 0 && mapPos.z < MAP_SIZE_Z) 
        {
            // Voxel sampling. Texture coordinates are normalized to [0, 1]
            vec3 texCoords = (vec3(mapPos) + 0.5) / vec3(MAP_SIZE_X, MAP_SIZE_Y, MAP_SIZE_Z);
            float voxelIdFloat = texture(volumeTexture, texCoords).r;

            // Stop if a non-empty voxel is found
            if (voxelIdFloat > 0.0) {
                hit = true;
                break;
            }
        } else {
            // Exit early if the ray flies out of the map bounds along Y, or too far along X/Z
            if (mapPos.y < 0 || mapPos.y >= MAP_SIZE_Y) {
                break;
            }
        }

        // Jump to the next voxel face along the shortest axis
        if (sideDist.x < sideDist.y) {
            if (sideDist.x < sideDist.z) {
                sideDist.x += deltaDist.x;
                mapPos.x += stepDir.x;
                hitSide = 0;
            } else {
                sideDist.z += deltaDist.z;
                mapPos.z += stepDir.z;
                hitSide = 2;
            }
        } else {
            if (sideDist.y < sideDist.z) {
                sideDist.y += deltaDist.y;
                mapPos.y += stepDir.y;
                hitSide = 1;
            } else {
                sideDist.z += deltaDist.z;
                mapPos.z += stepDir.z;
                hitSide = 2;
            }
        }
    }

    // Determine pixel color
    uint pixelColor = 0xFF1A1A1Au; // Default sky color (dark gray ABGR)

    if (hit) {
        // Base voxel color (e.g., generated from voxel ID or hardcoded palette)
        uint r = 50u;
        uint g = 180u;
        uint b = 50u;

        // Quick fake lighting (Directional Shading) based on which face was hit
        if (hitSide == 1) {
            // Top/Bottom face (brighter)
            r = uint(float(r) * 1.0);
            g = uint(float(g) * 1.0);
            b = uint(float(b) * 1.0);
        } else if (hitSide == 0) {
            // X side faces (slightly darker)
            r = uint(float(r) * 0.75);
            g = uint(float(g) * 0.75);
            b = uint(float(b) * 0.75);
        } else {
            // Z side faces (even darker for depth)
            r = uint(float(r) * 0.55);
            g = uint(float(g) * 0.55);
            b = uint(float(b) * 0.55);
        }

        // Pack the color back into 0xAA_BB_GG_RR format
        uint alpha = 0xFF000000u;
        pixelColor = alpha | (b << 16) | (g << 8) | r;
    }

    // Write the result back to the data buffer
    data[index] = pixelColor;
}
