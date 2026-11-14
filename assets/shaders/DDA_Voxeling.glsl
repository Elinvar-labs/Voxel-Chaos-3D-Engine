#version 430 core

// Const local group size (256 Threads)
layout(local_size_x = 256, local_size_y = 1, local_size_z = 1) in;

// Link buffer with binding = 0 (must equal code running on CPU)
// Shader recognize byte[] array as array of 4-bytes uint
layout(std430, binding = 0) buffer RawDataBuffer {
    uint data[];
};

void main() {
    // Get global ID of current thread (pixel index)
    uint index = gl_GlobalInvocationID.x;

    // Index is Out Of Bounds Protection (if screen size don't divide to 256)
    if (index >= data.length()) {
        return;
    }
    
    //uint blueColor = 0x0000FFFFu;

    uint blueColor = 0xFFFF0000u;

    // Write packed 4 bytes of color to array
    data[index] = blueColor;
}
