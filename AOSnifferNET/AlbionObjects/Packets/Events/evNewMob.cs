using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    // map[0:830511 1:753 2:-1 6: 7:[-270 -68] 8:[-270 -68] 9:63249422 10:180 11:2.25 13:385 14:385 16:63249422 17:138 18:138 19:4 20:63249422 28:0 252:113]
    internal class evNewMob
    {
        public int id;
        public int typeId;
        public Single[] pos;
        public int health;
        public int rarity;

        public evNewMob(int id, int typeId, float[] pos, int health, int rarity)
        {
            this.id = id;
            this.typeId = typeId;
            this.pos = pos;
            this.health = health;
            this.rarity = rarity;
        }
    }
}


