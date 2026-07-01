using System;

namespace AOSnifferNET
{
    //NewFishingZoneObject: { "0":1182,"1":[253.4,52.8],"2":3,"3":2,"4":"FishingNodeSwarm","252":341}
    //0: objectID 1: zone pos 2:charges (not present when empty) 3:times fished from 4:zone tipe
    internal class evNewFishingZoneObject
    {
        public int objectID;
        public Single[] zonePos;
        public short charges;
        public short fished;
        public string zoneType;

        public evNewFishingZoneObject(int objectID, float[] zonePos, short charges, short fished, string zoneType)
        {
            this.objectID = objectID;
            this.zonePos = zonePos;
            this.charges = charges;
            this.fished = fished;
            this.zoneType = zoneType;
        }
    }
}


