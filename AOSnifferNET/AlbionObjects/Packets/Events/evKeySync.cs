using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET.AlbionObjects.Packets.Events
{
    internal class evKeySync
    {
        public string xorKey;

        public evKeySync(string key)
        {
            this.xorKey = key;
        }
    }
}


