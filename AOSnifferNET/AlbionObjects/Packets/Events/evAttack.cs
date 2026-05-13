using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    internal class evAttack
    {
        public int attackerID;
        public int targetID;

        public evAttack(int attackerID, int targetID)
        {
            this.attackerID = attackerID;
            this.targetID = targetID;
        }
    }
}


