using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    internal class evUpdateSilver
    {
        public int id;
        public long currentSilver;

        public evUpdateSilver(int id, long currentSilver)
        {
            this.id = id;
            this.currentSilver = currentSilver;
        }
    }
}


