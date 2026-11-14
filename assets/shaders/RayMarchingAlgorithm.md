```c++

// Voxel ray marching from http://www.cse.chalmers.se/edu/year/2010/course/TDA361/grid.pdf
// Optimised by keeping block lookups within the current chunk, which minimises bitshifts, masks and multiplication operations
void RayMarch(in vec3 start, vec3 velocity, in double max, out bool hit, out Axis axis)
{
    int x = int(start.x);
    int y = int(start.y);
    int z = int(start.z);

    if (y < 0 || y >= MAP_SIZE_Y || x < 0 || x >= MAP_SIZE_X || z < 0 || z >= MAP_SIZE_Z)
    {
        hit = false;
        return;
    }

    // 2^5 = 32 (chunkSize)
    int chunkIndexX = x >> SHIFT;
    int chunkIndexY = y >> SHIFT;
    int chunkIndexZ = z >> SHIFT;

    var c = chunks[chunkIndexX, chunkIndexY, chunkIndexZ];

    // Determine the chunk-relative position of the ray using a bit-mask
    int i = x & MASK;
    int j = y & MASK;
    int k = z & MASK;

    // Calculate the index of this block in the chunk data[] array
    int access = j + i * CHUNK_SIZE + k * CHUNK_SIZE_SQUARED;

    // Calculate the end position of the ray
    var end = start + velocity;

    // If the start and end positions of the ray both lie on the same coordinate on the voxel grid
    if (x == int(end.x) && y == int(end.y) && z == int(end.z))
    {
        // The chunk is null if it contains no blocks
        if (c == null)
        {
            hit = false;
        }

        // If the block is empty
        else if (c.data[access].kind == 0)
        {
            hit = false;
        }

        // Else the ray begins and ends within the same non-empty block
        else
        {
            hit = true;
        }

        return;
    }

    // These variables are used to determine whether the ray has left the current working chunk.
    //  For example when travelling in the negative Y direction,
    //  if j == -1 then we have left the current working chunk
    int iComparison, jComparison, kComparison;

    // When leaving the current working chunk, the chunk-relative position must be reset.
    //  For example when travelling in the negative Y direction,
    //  j should be reset to CHUNK_SIZE - 1 when entering the new current working chunk
    int iReset, jReset, kReset;

    // When leaving the current working chunk, the access variable must also be updated.
    //  These values store how much to add or subtract from the access, depending on
    //  the direction of the ray:
    int xAccessReset, yAccessReset, zAccessReset;

    // The amount to increase i, j and k in each axis (either 1 or -1)
    int iStep, jStep, kStep;

    // When incrementing j, the chunk access is simply increased by 1
    // When incrementing i, the chunk access is increased by 32 (CHUNK_SIZE)
    // When incrementing k, the chunk access is increased by 1024 (CHUNK_SIZE_SQUARED)
    // These variables store whether to increase or decrease by the above amounts
    int xAccessIncrement, zAccessIncrement;

    // The distance to the closest voxel boundary in map units
    double xDist, yDist, zDist;

    if (velocity.x > 0)
    {
        iStep = 1;
        iComparison = CHUNK_SIZE;
        iReset = 0;
        xAccessReset = -CHUNK_SIZE_SQUARED;
        xAccessIncrement = CHUNK_SIZE;
        xDist = (x - start.x + 1);
    }
    else
    {
        iStep = -1;
        iComparison = -1;
        iReset = CHUNK_SIZE - 1;
        xAccessReset = CHUNK_SIZE_SQUARED;
        xAccessIncrement = -CHUNK_SIZE;
        xDist = (start.x - x);
    }

    if (velocity.y > 0)
    {
        jStep = 1;
        jComparison = CHUNK_SIZE;
        jReset = 0;
        yAccessReset = -CHUNK_SIZE;
        yDist = (y - start.y + 1);
    }
    else
    {
        jStep = -1;
        jComparison = -1;
        jReset = CHUNK_SIZE - 1;
        yAccessReset = CHUNK_SIZE;
        yDist = (start.y - y);
    }

    if (velocity.z > 0)
    {
        kStep = 1;
        kComparison = CHUNK_SIZE;
        kReset = 0;
        zAccessIncrement = CHUNK_SIZE_SQUARED;
        zAccessReset = -CHUNK_SIZE_CUBED;
        zDist = (z - start.z + 1);
    }
    else
    {
        kStep = -1;
        kComparison = -1;
        kReset = CHUNK_SIZE - 1;
        zAccessIncrement = -CHUNK_SIZE_SQUARED;
        zAccessReset = CHUNK_SIZE_CUBED;
        zDist = (start.z - z);
    }

    // This variable is used to track the current progress throughout the ray march
    double t = 0.0;

    velocity.Normalize();
    double xInverted = Math.Abs(1 / velocity.x);
    double yInverted = Math.Abs(1 / velocity.y);
    double zInverted = Math.Abs(1 / velocity.z);

    // Determine the distance to the closest voxel boundary in units of t
    //  - These values indicate how far we have to travel along the ray to reach the next voxel
    //  - If any component of the direction is perpendicular to an axis, the distance is double.PositiveInfinity
    double xDistance = velocity.x == 0 ? DOUBLE_INFINITY : xInverted * xDist;
    double yDistance = velocity.y == 0 ? DOUBLE_INFINITY : yInverted * yDist;
    double zDistance = velocity.z == 0 ? DOUBLE_INFINITY : zInverted * zDist;

    while (t <= max)
    {
        // Exit check
        if (c != null && c.data[access].kind != 0)
        {
            hit = true;
            return;
        }

        // Determine the closest voxel boundary
        if (yDistance < xDistance)
        {
            if (yDistance < zDistance)
            {
                // Advance to the closest voxel boundary in the Y direction

                // Increment the chunk-relative position and the block access position
                j += jStep;
                access += jStep;

                // Check if we have exited the current working chunk.
                // This means that j is either -1 or 32
                if (j == jComparison)
                {
                    // If moving in the positive direction, reset j to 0.
                    // If moving in the negative Y direction, reset j to 31
                    j = jReset;

                    // Reset the chunk access
                    access += yAccessReset;

                    // Calculate the new chunk index
                    chunkIndexY += jStep;

                    // If the new chunk is outside the map, exit
                    if (chunkIndexY < 0 || chunkIndexY >= CHUNK_AMOUNT_Y)
                    {
                        hit = false;
                        return;
                    }

                    // Get a reference to the new working chunk
                    c = chunks[chunkIndexX, chunkIndexY, chunkIndexZ];
                }

                // Update our progress in the ray 
                t = yDistance;

                // Set the new distance to the next voxel Y boundary
                yDistance += yInverted;

                // For collision purposes we also store the last axis that the ray collided with
                // This allows us to reflect particle velocity on the correct axis
                axis = Axis.y;
            }
            else
            {
                k += kStep;
                access += zAccessIncrement;

                if (k == kComparison)
                {
                    k = kReset;
                    access += zAccessReset;

                    chunkIndexZ += kStep;

                    if (chunkIndexZ < 0 || chunkIndexZ >= CHUNK_AMOUNT_Z)
                    {
                        hit = false;
                        return;
                    }

                    c = chunks[chunkIndexX, chunkIndexY, chunkIndexZ];
                }

                t = zDistance;
                zDistance += zInverted;
                axis = Axis.z;
            }
        }
        else if (xDistance < zDistance)
        {
            i += iStep;
            access += xAccessIncrement;

            if (i == iComparison)
            {
                i = iReset;
                access += xAccessReset;

                chunkIndexX += iStep;

                if (chunkIndexX < 0 || chunkIndexX >= CHUNK_AMOUNT_X)
                {
                    hit = false;
                    return;
                }

                c = chunks[chunkIndexX, chunkIndexY, chunkIndexZ];
            }

            t = xDistance;
            xDistance += xInverted;
            axis = Axis.x;
        }
        else
        {
            k += kStep;
            access += zAccessIncrement;

            if (k == kComparison)
            {
                k = kReset;
                access += zAccessReset;

                chunkIndexZ += kStep;

                if (chunkIndexZ < 0 || chunkIndexZ >= CHUNK_AMOUNT_Z)
                {
                    hit = false;
                    return;
                }

                c = chunks[chunkIndexX, chunkIndexY, chunkIndexZ];
            }

            t = zDistance;
            zDistance += zInverted;
            axis = Axis.z;
        }
    }

    hit = false;
}

```