using System;
using System.Numerics;
using AlbionDataHandlers.Entities;

namespace Nightwatch.UserControls.AlbionOverlay.ViewModels
{
    public class MobViewModel
    {
        public int Id { get; set; }
        public int TypeId { get; set; }
        public float CurrentLerpedX { get; set; }
        public float CurrentLerpedY { get; set; }
        public float DistanceToMainPlayer { get; set; }
        
        public string DisplayName { get; set; }
        public string UniqueName { get; set; }
        public string SpecificIconPath { get; set; }
        
        // --- Kategorilendirme ---
        public bool IsLivingResource { get; set; }
        public bool IsPriority { get; set; }
        public bool IsTrackerCustom { get; set; }
        public bool IsHarvestableTypeId { get; set; }
        public bool IsMist { get; set; }
        
        // --- Living Resource Özellikleri ---
        public int Tier { get; set; }
        public int Enchant { get; set; }
        public Nightwatch.HarvestableCategory Category { get; set; }
        
        // --- Arayüz Bilgileri ---
        public uint TierColor { get; set; }
        public string ResourceImagePath { get; set; }
        public string ResourceLabel { get; set; }
        
        public Mob RawMob { get; set; }
    }
}
