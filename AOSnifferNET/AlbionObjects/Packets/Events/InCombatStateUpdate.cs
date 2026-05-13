using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    internal class InCombatStateUpdate
    {
        //InCombatStateUpdate: {"0":297731,"2":true,"252":255}
        public long playerId;
        public bool playerAttaking;
        public bool enemyAttaking;

        public InCombatStateUpdate(long playerId, bool playerAttaking, bool enemyAttaking)
        {
            this.playerId = playerId;
            this.playerAttaking = playerAttaking;
            this.enemyAttaking = enemyAttaking;
        }
    }
}


