using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    internal class HarvestableChangeState
    {
        public int id;
        public int charges;
        public int enchantment;

        public HarvestableChangeState(int id, int charges, int enchantment)
        {
            this.id = id;
            this.charges = charges;
            this.enchantment = enchantment;
        }
    }
}


