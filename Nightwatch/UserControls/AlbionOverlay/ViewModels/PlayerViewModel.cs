using System;
using System.Numerics;
using AlbionDataHandlers.Entities;

namespace Nightwatch.UserControls.AlbionOverlay.ViewModels
{
    public class PlayerViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Guild { get; set; }
        public string Alliance { get; set; }
        
        // --- Ekipman ve IP ---
        public int AverageIP { get; set; }
        public int WeaponIP { get; set; }
        public int HeadIP { get; set; }
        public int ArmorIP { get; set; }
        public int ShoesIP { get; set; }
        public int CapeIP { get; set; }
        
        public string WeaponName { get; set; }
        public string HeadName { get; set; }
        public string ArmorName { get; set; }
        public string ShoesName { get; set; }
        public string CapeName { get; set; }
        
        public int[] EquipmentRaw { get; set; }
        
        // --- Konum ve Hareket ---
        public float CurrentLerpedX { get; set; }
        public float CurrentLerpedY { get; set; }
        public float DistanceToMainPlayer { get; set; }
        
        // Delta Distance için ok yönü ve rengi
        public string DirectionArrow { get; set; }
        public Vector4 ArrowColor { get; set; }
        
        // --- Sağlık (Health) ---
        public float CurrentHealth { get; set; }
        public float MaxHealth { get; set; }
        public float HealthRatio { get; set; }
        public string HealthText { get; set; }
        public Vector4 HealthColor { get; set; }
        
        // --- Arayüz Renkleri ---
        public Vector4 NameColor { get; set; }
        
        // Orijinal Player referansı (Eğer arayüzün direkt modele ulaşması gerekirse)
        public Player RawPlayer { get; set; }
    }
}
