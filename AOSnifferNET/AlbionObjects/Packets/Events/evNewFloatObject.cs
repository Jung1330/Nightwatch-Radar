using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    internal class evNewFloatObject
    {
        public int bobberID;
        public Single[] bobberPos;
        public float angleFromPlayer;
        public int playerID;
        public string fishingState;

        public evNewFloatObject(int bobberID, float[] bobberPos, float angleFromPlayer, int playerID, string fishingState)
        {
            this.bobberID = bobberID;
            this.bobberPos = bobberPos;
            this.angleFromPlayer = angleFromPlayer;
            this.playerID = playerID;
            this.fishingState = fishingState;
        }
    }
}


