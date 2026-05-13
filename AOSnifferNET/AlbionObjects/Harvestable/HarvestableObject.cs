using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    internal class HarvestableObject
    {
        public int id;
        public byte type;
        public byte tier;
        public Single[] pos;
        public byte charges;
        public byte enchantment;

        public HarvestableObject(int id, byte type, byte tier, float[] pos, byte charges, byte enchantment)
        {
            this.id = id;
            this.type = type;
            this.tier = tier;
            this.pos = pos;
            this.charges = charges;
            this.enchantment = enchantment;
        }
    }
}


