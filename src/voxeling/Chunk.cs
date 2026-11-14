using System;
using System.Collections.Generic;
using System.Text;

namespace VoxelChaos.Voxeling
{
    public class Chunk
    {
        public byte[] data = new byte[Constants.CHUNK_SIZE_CUBED];
    }
}
