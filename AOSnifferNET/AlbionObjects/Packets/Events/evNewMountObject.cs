using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    internal class evNewMountObject
    {
        public int id;
        public float[] pos;
        public byte[] ownerMarkId;

        public evNewMountObject(int id, float[] pos, byte[] ownerMarkId)
        {
            this.id = id;
            this.pos = pos;
            this.ownerMarkId = ownerMarkId;
        }
    }
}


