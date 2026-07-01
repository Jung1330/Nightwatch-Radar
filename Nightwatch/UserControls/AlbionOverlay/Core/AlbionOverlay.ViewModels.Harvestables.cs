using System;
using System.Collections.Generic;
using System.Numerics;
using AlbionDataHandlers.Entities;
using AlbionDataHandlers.Enums;
using Nightwatch.UserControls.AlbionOverlay.ViewModels;
using Nightwatch.UserControls.Language;

namespace Nightwatch
{
    public partial class AlbionOverlay
    {
        private void UpdateHarvestViewModels(Player mainPlayer)
        {
            lock (_dataLock)
            {
                _harvestViewModels.Clear();

                foreach (var h in _harvestBuffer)
                {
                    if (_ignoredMobIds.Contains(h.Type)) continue;
                    if (h.Count <= 0) continue;

                    var cat = GetCategoryFromTypeId(h.Type);
                    int networkTier = h.Tier;
                    int metadataTier = 0;
                    if (cat != HarvestableCategory.None)
                    {
                        // Metadata for harvestables is not available in Mappers
                        metadataTier = 0;
                    }

                    int tier = networkTier;
                    if (_resourceTruthMode == 2 && metadataTier > 0) // Metadata First
                    {
                        tier = metadataTier;
                    }
                    else if (_resourceTruthMode == 0) // Name First -> Fallback to Network since static resources don't have distinct names parsed easily without Type
                    {
                        tier = networkTier > 0 ? networkTier : metadataTier;
                    }
                    else // Network First
                    {
                        tier = networkTier > 0 ? networkTier : metadataTier;
                    }
                    
                    if (tier <= 0) tier = 1;
                    if (tier > 8) tier = 8;

                    int enchant = h.EnchantmentLevel;

                    uint tCol = GetTierEnchantColor(tier, enchant);

                    string translatedName = Lang.Get(cat.ToString());
                    string resName = translatedName != cat.ToString() ? translatedName : cat.ToString();
                    string label = (enchant > 0) ? $"T{tier}.{enchant} {resName}" : $"T{tier} {resName}";
                    string imgPath = GetResourceImagePath(cat, tier, enchant);

                    var vm = new HarvestViewModel
                    {
                        Id = h.Id,
                        Type = h.Type,
                        CurrentLerpedX = h.CurrentLerpedX,
                        CurrentLerpedY = h.CurrentLerpedY,
                        Tier = tier,
                        Enchant = enchant,
                        Category = cat,
                        Size = h.Count, // Current remaining resources
                        TierColor = tCol,
                        ResourceImagePath = imgPath,
                        ResourceLabel = label,
                        RawHarvestable = h
                    };

                    _harvestViewModels.Add(vm);
                }
            }
        }
    }
}
