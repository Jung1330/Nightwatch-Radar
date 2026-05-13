using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    internal class evMounted
    {
        public int playerId;
        public bool isMounted;

        public evMounted(int playerId, bool isMounted)
        {
            this.playerId = playerId;
            this.isMounted = isMounted;
        }
    }
}


