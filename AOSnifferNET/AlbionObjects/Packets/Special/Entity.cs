using System;

namespace AOSnifferNET
{
    public class Entity
    {
        public int ID;
        public Single posX;
        public Single posY;

        public Entity(int iD, float posX, float posY)
        {
            ID = iD;
            this.posX = posX;
            this.posY = posY;
        }
    }
}


