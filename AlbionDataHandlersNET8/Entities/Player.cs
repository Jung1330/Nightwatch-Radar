using System;

namespace AlbionDataHandlers.Entities
{
    public class Player
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Guild { get; set; }
        public string Alliance { get; set; } // Ýttifak
        public int Faction { get; set; } // 0=Passive, 255=Hostile

        // --- PVP ANALÝZÝ ÝÇÝN EKÝPMAN ---
        // [0]=MainHand, [1]=OffHand, [2]=Head, [3]=Armor, [4]=Shoes, [5]=Bag, [6]=Cape
        public int[] Equipment { get; set; }

        public float CurrentHealth { get; set; }
        public float MaxHealth { get; set; }

        public float PositionX { get; set; }
        public float PositionY { get; set; }

        // Hareket yumuþatma için
        public float CurrentLerpedX { get; set; }
        public float CurrentLerpedY { get; set; }

        public Player()
        {
            Equipment = new int[0];
        }

        public override string ToString()
        {
            return $"{Name} [{Guild}]";
        }
    }
}


