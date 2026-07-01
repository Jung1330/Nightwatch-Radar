using System;
using System.Collections.Generic;
using System.Numerics;
using AlbionDataHandlers.Entities;
using AlbionDataHandlers.Enums;
using Nightwatch.UserControls.AlbionOverlay.ViewModels;

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

                    string displayName = "";
                    if (_hiddenChestIds.Contains(m.TypeId))
                        displayName = "Hidden Chest";
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
                    var typeInfo2 = AlbionDataHandlers.Mappers.MobMapper.Instance.GetMobInfo(m.TypeId);
                    string uniqueNameUpper = (typeInfo2?.UniqueName ?? m.Name ?? "").ToUpperInvariant();

                    bool isAspectOrWorldBoss = upperName.Contains("ASPECT") || upperName.Contains("WORLD_BOSS") || upperName.Contains("WORLD BOSS") || (upperName.Contains("TITAN") && !upperName.Contains("TITANIUM")) || upperName.Contains("GUARDIAN")
                        || uniqueNameUpper.Contains("ASPECT") || uniqueNameUpper.Contains("WORLD_BOSS");

                    bool isMistBoss = uniqueNameUpper.Contains("FAIRYDRAGON") || uniqueNameUpper.Contains("GRIFFIN") || uniqueNameUpper.Contains("VEILWEAVER") || uniqueNameUpper.Contains("MISTS_SPIDER");

                    // Crystal kontrolleri: hem displayName hem uniqueName
                    bool isCrystalMob = upperName.Contains("CRYSTAL") || uniqueNameUpper.Contains("CRYSTAL")
                        || upperName.Contains("KRİSTAL") || upperName.Contains("KRISTAL");

                    string specificIcon = null;
                    if (upperName.Contains("FAIRY") || (upperName.Contains("FEY") && upperName.Contains("DRAGON")) || uniqueNameUpper.Contains("FAIRYDRAGON")) specificIcon = _feyDragonPath;
                    else if (upperName.Contains("GRIFFIN") || uniqueNameUpper.Contains("GRIFFIN")) specificIcon = _griffinPath;
                    else if ((upperName.Contains("VEIL") && upperName.Contains("WEAVER")) || uniqueNameUpper.Contains("VEILWEAVER")) specificIcon = _veilWeaverPath;
                    else if (isAspectOrWorldBoss && IsImageExistsCached(_aspectBossIconPath)) specificIcon = _aspectBossIconPath;
                    else if ((GetMobCategory(displayName, info?.Tier ?? 0) == "Crystals") || isCrystalMob) specificIcon = _spiderImagePath;
                    else if ((m.TypeId >= 908 && m.TypeId <= 923) || uniqueNameUpper.Contains("AVALON_TREASURE_MINION") || upperName.Contains("AVALONIAN TREASURE DRONE")) specificIcon = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Resources", "AVALONMINIONCHEST.png");

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
                        SpecificIconPath = specificIcon,
                        IsLivingResource = isLivingResource,
                        Tier = finalTier,
                        Enchant = enchant,
                        Category = mobCategory,
                        IsPriority = _customPriorityMobs.Contains(m.TypeId),
                        IsTrackerCustom = _trackerCustomMobs.Contains(m.TypeId),
                        IsHarvestableTypeId = IsHarvestableTypeId(m.TypeId),
                        IsMist = !isLivingResource && !isMistBoss && (upperName.Contains("MIST") || (upperName.Contains("WISP") && !upperName.Contains("CAGE") && !uniqueNameUpper.Contains("CAGE")) || upperName.Contains("PORTAL")),
                        RawMob = m
                    };

                    _mobViewModels.Add(vm);
                }
            }
        }
    }
}
