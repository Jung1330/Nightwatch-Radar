using System;

namespace AOSnifferNET
{
    // {"0":3978,"1":"/rjqYKIGrkKfUuUcq51rxA==","2":466,"3":"MOUNTAIN_GREEN_REPAIRSHOP_OUTPOST","4":[285.0,-294.0],"8":"System","9":"System","13":true,"16":17496000000,"17":638132642575260623,"18":7950000000,"19":-1,"20":638238907980059788,"21":7949930000,"22":638132642575260623,"26":true,"27":true,"28":true,"29":0,"252":41}

    internal class evNewBuilding
    {
        public int packetID;
        public string name;
        public Single[] pos;

        public evNewBuilding(int packetID, string name, float[] pos)
        {
            this.packetID = packetID;
            this.name = name;
            this.pos = pos;
        }
    }
}


