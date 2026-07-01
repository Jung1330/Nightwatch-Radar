using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    internal class evHarvestFinished
    {
        public int playerId;
        public int harvestableId;
        public short gathered;
        public short yield;
        public short premium;
        public short charges;

        public evHarvestFinished(int playerId, int harvestableId, short gathered, short yield, short premium, short charges)
        {
            this.playerId = playerId;
            this.harvestableId = harvestableId;
            this.gathered = gathered;
            this.yield = yield;
            this.premium = premium;
            this.charges = charges;
        }
    }
}


