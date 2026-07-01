using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    internal class evNewCharacter
    {
        public int id;
        public string nick;
        public string guild;
        public string alliance;
        public Single[] pos;
        public short[] items;
        public short[] skills;
        public int faction;
        public int currentHealth;
        public int maxHealth;

        public evNewCharacter(int id, string nick, string guild, string alliance, float[] pos, short[] items, short[] skills, int faction, int currentHealth, int maxHealth)
        {
            this.id = id;
            this.nick = nick;
            this.guild = guild;
            this.alliance = alliance;
            this.pos = pos;
            this.items = items;
            this.skills = skills;
            this.faction = faction;
            this.currentHealth = currentHealth;
            this.maxHealth = maxHealth;
        }
    }
}


