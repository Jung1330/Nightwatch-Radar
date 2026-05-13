using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{ 
    internal class evAttachItemContainer
    {
        public int objectID;
        public byte[] ownerMarkID;
        public int[] itemsObjectID;

        public evAttachItemContainer(int objectID, byte[] ownerMarkID, int[] itemsObjectID)
        {
            this.objectID = objectID;
            this.ownerMarkID = ownerMarkID;
            this.itemsObjectID = itemsObjectID;
        }
    }
}


