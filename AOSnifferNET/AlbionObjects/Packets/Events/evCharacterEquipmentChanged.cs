using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    internal class evCharacterEquipmentChanged
    {
        public short[] items;
        public short[] skills;

        public evCharacterEquipmentChanged(short[] items, short[] skills)
        {
            this.items = items;
            this.skills = skills;
        }
    }
}


