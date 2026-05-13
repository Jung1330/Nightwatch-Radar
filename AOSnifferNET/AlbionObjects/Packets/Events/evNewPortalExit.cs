using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    // [0:1871612 1:[70 -201] 3:MOUNTAIN_GREEN_RANDOM_EXIT_10x10_PORTAL_SOLO_B 4:true 5:0 11:0 12:1 252:301]
    internal class evNewPortalExit
    {
        public int id;
        public Single[] pos;
        public String type;

        public evNewPortalExit(int id, float[] pos, string type)
        {
            this.id = id;
            this.pos = pos;
            this.type = type;
            /*
            if (type.Contains("_CORRUPT"))
                this.type = "CORRUPT";
            else if (type.Contains("_SOLO"))
                this.type = "SOLO";
            else if (type.Contains("_ELITE"))
                this.type = "ELITE";
            else
                this.type = "GROUP";
            */
        }
    }
}


