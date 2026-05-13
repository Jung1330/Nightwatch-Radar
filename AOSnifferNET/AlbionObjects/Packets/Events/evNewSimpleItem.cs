using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    internal class evNewSimpleItem
    {
        public int objectID;
        public int itemID;
        public short amount;
        public long avgValue;

        public evNewSimpleItem(int objectID, int itemID, short amount, long avgValue)
        {
            this.objectID = objectID;
            this.itemID = itemID;
            this.amount = amount;
            this.avgValue = avgValue;
        }
    }
}


