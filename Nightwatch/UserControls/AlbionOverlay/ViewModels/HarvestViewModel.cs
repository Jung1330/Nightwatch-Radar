using System;
using System.Numerics;
using AlbionDataHandlers.Entities;

namespace Nightwatch.UserControls.AlbionOverlay.ViewModels
{
    public class HarvestViewModel
    {
        public int Id { get; set; }
        public int Type { get; set; }
        public float CurrentLerpedX { get; set; }
        public float CurrentLerpedY { get; set; }
        
        public int Tier { get; set; }
        public int Enchant { get; set; }
        public Nightwatch.HarvestableCategory Category { get; set; }
        public int Size { get; set; }
        
        public uint TierColor { get; set; }
        public string ResourceImagePath { get; set; }
        public string ResourceLabel { get; set; }
        
        public Harvestable RawHarvestable { get; set; }
    }
}
