using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    internal class evNewLoot
    {
        public int lootId;
        public Single[] pos;

        public evNewLoot(int lootId, float[] pos)
        {
            this.lootId = lootId;
            this.pos = pos;
        }
    }
}


