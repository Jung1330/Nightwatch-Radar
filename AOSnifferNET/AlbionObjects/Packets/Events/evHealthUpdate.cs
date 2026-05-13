using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    // mi id: "characterId":166380" PACKETE: {"0: targetEntityId":166414,"1":14992275,"2: healthUpdate":-10.0,"3: targetEntityCurrentHealth":591.0,"4":1,"5":5,"6: attackerEntityId":166380,"7":1757,"252":6}
    internal class evHealthUpdate
    {
        public int targetEntityId;
        public int targetHealthUpdate;
        public int targetCurrentHealth;
        public int attackerEntityId;

        public evHealthUpdate(int targetEntityId, int targetHealthUpdate, int targetCurrentHealth, int attackerEntityId)
        {
            this.targetEntityId = targetEntityId;
            this.targetHealthUpdate = targetHealthUpdate;
            this.targetCurrentHealth = targetCurrentHealth;
            this.attackerEntityId = attackerEntityId;
        }
    }
}


