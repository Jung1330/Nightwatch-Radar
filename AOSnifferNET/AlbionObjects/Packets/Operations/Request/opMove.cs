using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    // [OnRequest]Move: [{"Key":0,"Value":637885351667868041},{"Key":1,"Value":[-21.3366661,12.3170137]},{"Key":2,"Value":138.935577},{"Key":3,"Value":[-21.3366661,12.3170137]},{"Key":4,"Value":8.8},{"Key":253,"Value":21}]
    internal class opMove
    {
        public long id;
        public float[] pos;
        public float angle;
        public float[] target;
        public float speed;

        public opMove(long id, float[] pos, float angle, float[] target, float speed)
        {
            this.id = id;
            this.pos = pos;
            this.angle = angle;
            this.target = target;
            this.speed = speed;
        }
    }
}


