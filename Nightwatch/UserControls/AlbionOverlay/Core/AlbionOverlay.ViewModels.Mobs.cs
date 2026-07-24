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
        private List<MobViewModel> _mobViewModels = new List<MobViewModel>();
        private List<HarvestViewModel> _harvestViewModels = new List<HarvestViewModel>();

        private void UpdateMobViewModels(Player mainPlayer)
        {
            lock (_dataLock)
            {
                _mobViewModels.Clear();

                foreach (var m in _mobBuffer)
                {
                    if (_ignoredMobIds.Contains(m.TypeId)) continue;
                    MobInfo info = null;
                    _mobDatabase.TryGetValue(m.TypeId, out info);

                    var typeInfo2 = AlbionDataHandlers.Mappers.MobMapper.Instance.GetMobInfo(m.TypeId);
                    string uniqueNameUpper = (info?.HarvestType ?? typeInfo2?.UniqueName ?? m.Name ?? "").ToUpperInvariant();

                    // Check if hidden chest by ID or keyword matching
                    bool isHiddenChest = _hiddenChestIds.Contains(m.TypeId) ||
                        (uniqueNameUpper != null && 
                         (uniqueNameUpper.Contains("CHEST") || 
                          uniqueNameUpper.Contains("COFFER") || 
                          uniqueNameUpper.Contains("CACHE") || 
                          uniqueNameUpper.Contains("TREASURE")) && 
                         !uniqueNameUpper.Contains("MINION") && 
                         !uniqueNameUpper.Contains("DRONE"));

                    string displayName = "";
                    if (m.TypeId == 51900)
                    {
                        string rarityStr = m.Rarity switch
                        {
                            1 => Lang.Get("Rarity_Uncommon") ?? "Uncommon",
                            2 => Lang.Get("Rarity_Rare") ?? "Rare",
                            3 => Lang.Get("Rarity_Epic") ?? "Epic",
                            4 => Lang.Get("Rarity_Legendary") ?? "Legendary",
                            _ => Lang.Get("Rarity_Common") ?? "Common"
                        };
                        displayName = $"{rarityStr} {Lang.Get("Mob_LootChest") ?? "Loot Chest"}";
                    }
                    else if (isHiddenChest)
                    {
                        string rarityStr = m.Rarity switch
                        {
                            1 => Lang.Get("Rarity_Uncommon") ?? "Uncommon",
                            2 => Lang.Get("Rarity_Rare") ?? "Rare",
                            3 => Lang.Get("Rarity_Epic") ?? "Epic",
                            4 => Lang.Get("Rarity_Legendary") ?? "Legendary",
                            _ => Lang.Get("Rarity_Common") ?? "Common"
                        };
                        displayName = $"{rarityStr} {Lang.Get("Mob_ShowHiddenChests") ?? "Hidden Chest"}";
                    }
                    else if (info != null && !string.IsNullOrEmpty(info.Name))
                        displayName = info.Name;
                    else if (!string.IsNullOrEmpty(m.Name))
                        displayName = CleanName(m.Name);
                    else
                        displayName = $"TypeId:{m.TypeId}";

                    if (displayName != "Unknown")
                    {
                        displayName = displayName.Replace("Mob ", "").Replace("Enemy ", "").Trim();
                        if (string.IsNullOrEmpty(displayName)) displayName = "Unknown";
                    }

                    string upperName = displayName.ToUpperInvariant();
                    // UniqueName her zaman İngilizce — dil bağımsız ikon/kategori tespiti için
                    
                    bool isAspectOrWorldBoss = uniqueNameUpper.Contains("ASPECT") 
                        || uniqueNameUpper.Contains("WORLD_BOSS") 
                        || uniqueNameUpper.Contains("WORLD BOSS") 
                        || (uniqueNameUpper.Contains("TITAN") && !uniqueNameUpper.Contains("TITANIUM")) 
                        || uniqueNameUpper.Contains("GUARDIAN");

                    bool isMistBoss = uniqueNameUpper.Contains("FAIRYDRAGON") || uniqueNameUpper.Contains("GRIFFIN") || uniqueNameUpper.Contains("VEILWEAVER") || uniqueNameUpper.Contains("MISTS_SPIDER");

                    // Crystal kontrolleri: sadece uniqueName
                    bool isCrystalMob = uniqueNameUpper.Contains("CRYSTAL");

                    string specificIcon = null;
                    if (uniqueNameUpper.Contains("FAIRY") || (uniqueNameUpper.Contains("FEY") && uniqueNameUpper.Contains("DRAGON")) || uniqueNameUpper.Contains("FAIRYDRAGON")) specificIcon = _feyDragonPath;
                    else if (uniqueNameUpper.Contains("GRIFFIN")) specificIcon = _griffinPath;
                    else if ((uniqueNameUpper.Contains("VEIL") && uniqueNameUpper.Contains("WEAVER")) || uniqueNameUpper.Contains("VEILWEAVER")) specificIcon = _veilWeaverPath;
                    else if (isAspectOrWorldBoss && IsImageExistsCached(_aspectBossIconPath)) specificIcon = _aspectBossIconPath;
                    else if ((GetMobCategory(displayName, info?.Tier ?? 0) == "Crystals") || isCrystalMob) specificIcon = _spiderImagePath;
                    else if ((m.TypeId >= 908 && m.TypeId <= 923) || uniqueNameUpper.Contains("AVALON_TREASURE_MINION") || uniqueNameUpper.Contains("AVALONIAN TREASURE DRONE")) specificIcon = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Resources", "AVALONMINIONCHEST.png");

                    var typeInfo = AlbionDataHandlers.Mappers.MobMapper.Instance.GetMobInfo(m.TypeId);
                    HarvestableCategory mobCategory = HarvestableCategory.None;
                    int resolvedLivingTier = 0;
                    bool isLivingResource = false;

                    bool isExplicitLivingType = m.Type == MobTypes.LivingHarvestable || m.Type == MobTypes.LivingSkinnable;
                    if (isExplicitLivingType)
                    {
                        string livingNameSource = typeInfo?.UniqueName ?? typeInfo?.Name ?? m.Name ?? displayName;
                        mobCategory = ParseCategoryFromString(livingNameSource);
                        if (mobCategory == HarvestableCategory.None)
                            mobCategory = ParseCategoryFromString(displayName);

                        if (mobCategory != HarvestableCategory.None)
                        {
                            resolvedLivingTier = typeInfo?.LootTier ?? (int)(typeInfo?.Tier ?? 0);
                            if (resolvedLivingTier <= 0)
                                resolvedLivingTier = ParseTier(livingNameSource);
                        }

                        isLivingResource = mobCategory != HarvestableCategory.None;
                    }

                    if (!isLivingResource)
                    {
                        if (_livingResourceTypeMap.TryGetValue(m.TypeId, out var livingMap))
                        {
                            mobCategory = livingMap.category;
                            resolvedLivingTier = livingMap.tier;
                        }
                        else if (info?.IsHarvestable == true && !string.IsNullOrEmpty(info.HarvestType))
                        {
                            mobCategory = ParseCategoryFromString(info.HarvestType);
                            resolvedLivingTier = info.Tier;
                        }
                        else
                        {
                            string livingNameSource = typeInfo?.UniqueName ?? typeInfo?.Name ?? m.Name ?? displayName;
                            mobCategory = ParseCategoryFromString(livingNameSource);
                            if (mobCategory != HarvestableCategory.None)
                            {
                                resolvedLivingTier = info?.Tier ?? typeInfo?.LootTier ?? (int)(typeInfo?.Tier ?? 0);
                                if (resolvedLivingTier <= 0)
                                    resolvedLivingTier = ParseTier(livingNameSource);
                            }
                        }

                        isLivingResource = (mobCategory != HarvestableCategory.None);
                    }

                    if (uniqueNameUpper.Contains("GATHERER"))
                    {
                        isLivingResource = false;
                        mobCategory = HarvestableCategory.None;
                    }

                    int finalTier = 0;
                    if (isLivingResource)
                    {
                        string uName = typeInfo?.UniqueName ?? "";
                        if (!string.IsNullOrEmpty(uName))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(uName, @"T(\d+)");
                            if (match.Success && int.TryParse(match.Groups[1].Value, out int matchedTier))
                                finalTier = matchedTier;
                        }
                        if (finalTier == 0)
                        {
                            int parsedTier = ParseTier(displayName);
                            if (parsedTier <= 0) parsedTier = ParseTier(m.Name);
                            if (parsedTier <= 0) parsedTier = ParseTier(typeInfo?.UniqueName);

                            int metadataTier = typeInfo?.LootTier ?? (int)(typeInfo?.Tier ?? 0);
                            if (metadataTier <= 0) metadataTier = resolvedLivingTier;

                            if (_resourceTruthMode == 0) // Name First
                            {
                                if (parsedTier > 0) finalTier = parsedTier;
                                else if (metadataTier > 0) finalTier = metadataTier;
                                else if (m.NetworkTier > 0 && m.NetworkTier <= 8) finalTier = m.NetworkTier;
                            }
                            else if (_resourceTruthMode == 2) // Metadata First
                            {
                                if (metadataTier > 0) finalTier = metadataTier;
                                else if (parsedTier > 0) finalTier = parsedTier;
                                else if (m.NetworkTier > 0 && m.NetworkTier <= 8) finalTier = m.NetworkTier;
                            }
                            else // 1 = Network First (Default)
                            {
                                // For living resources, NetworkTier is often combat strength, not resource tier. So prioritize metadata.
                                if (metadataTier > 0) finalTier = metadataTier;
                                else if (m.NetworkTier > 0 && m.NetworkTier <= 8) finalTier = m.NetworkTier;
                                else if (parsedTier > 0) finalTier = parsedTier;
                            }
                        }
                        if (finalTier <= 0 || finalTier > 8)
                        {
                            int pT = ParseTier(m.Name);
                            if (pT <= 0) pT = ParseTier(typeInfo?.UniqueName);
                            if (pT <= 0) pT = ParseTier(typeInfo?.Name);
                            if (pT <= 0) pT = ParseTier(displayName);

                            if (pT > 0 && pT <= 8)
                                finalTier = pT;
                        }

                        if (finalTier <= 0) finalTier = 1;
                        else if (finalTier > 8) finalTier = 8;
                    }

                    int enchant = ParseEnchant(m.Name);
                    if (enchant <= 0) enchant = m.EnchantmentLevel;

                    if (uniqueNameUpper != null && 
                        (uniqueNameUpper.Contains("MIST") || 
                         uniqueNameUpper.Contains("PORTAL") || 
                         (uniqueNameUpper.Contains("WISP") && !uniqueNameUpper.Contains("CAGE"))))
                    {
                        if (uniqueNameUpper.Contains("UNCOMMON")) enchant = 1;
                        else if (uniqueNameUpper.Contains("RARE")) enchant = 2;
                        else if (uniqueNameUpper.Contains("EPIC")) enchant = 3;
                        else if (uniqueNameUpper.Contains("LEGENDARY")) enchant = 4;
                    }

                    float dist = 0;
                    if (mainPlayer != null)
                    {
                        dist = Vector2.Distance(
                            new Vector2(m.CurrentLerpedX, m.CurrentLerpedY), 
                            new Vector2(mainPlayer.CurrentLerpedX, mainPlayer.CurrentLerpedY));
                    }

                    var vm = new MobViewModel
                    {
                        Id = m.Id,
                        TypeId = m.TypeId,
                        CurrentLerpedX = m.CurrentLerpedX,
                        CurrentLerpedY = m.CurrentLerpedY,
                        DistanceToMainPlayer = dist,
                        DisplayName = displayName,
                        UniqueName = uniqueNameUpper,
                        SpecificIconPath = specificIcon,
                        IsLivingResource = isLivingResource,
                        Tier = finalTier,
                        Enchant = enchant,
                        Category = mobCategory,
                        IsPriority = _customPriorityMobs.Contains(m.TypeId),
                        IsTrackerCustom = _trackerCustomMobs.Contains(m.TypeId),
                        IsHarvestableTypeId = IsHarvestableTypeId(m.TypeId),
                        IsMist = !isLivingResource && !isMistBoss && !isHiddenChest && ((uniqueNameUpper.Contains("WISP") && !uniqueNameUpper.Contains("CAGE")) || uniqueNameUpper.Contains("PORTAL") || m.TypeId == 51800 || m.TypeId == 51900),
                        RawMob = m
                    };

                    _mobViewModels.Add(vm);
                }
            }
        }
    }
}
