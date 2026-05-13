using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    //HarvestStart: {"0":807374,"1":637995874087127581,"2":637995874087127581,"3":3802,"4":23,"5":1.4,"6":807547,"7":2151,"252":52} 0: playerId 3: harvestableId 4:type 5:time to finish
    internal class evHarvestStart
    {
        public int harvestableId;
        public float timeToFinish;

        public evHarvestStart(int harvestableId, float timeToFinish)
        {
            this.harvestableId = harvestableId;
            this.timeToFinish = timeToFinish;
        }
    }
}


