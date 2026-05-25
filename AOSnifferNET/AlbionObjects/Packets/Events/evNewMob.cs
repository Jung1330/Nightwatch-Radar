using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    internal class evNewMob
    {
        public int id;
        public int typeId;
        public Single[] pos;
        public int health;
        public int rarity;
        public int enchant;
        public int tier;

        public evNewMob(int id, int typeId, float[] pos, int health, int rarity)
        {
            this.id = id;
            this.typeId = typeId;
            this.pos = pos;
            this.health = health;
            this.rarity = rarity;
            this.enchant = 0;
            this.tier = 0;
        }

        public evNewMob(int id, int typeId, float[] pos, int health, int rarity, int enchant, int tier)
        {
            this.id = id;
            this.typeId = typeId;
            this.pos = pos;
            this.health = health;
            this.rarity = rarity;
            this.enchant = enchant;
            this.tier = tier;
        }
    }
}