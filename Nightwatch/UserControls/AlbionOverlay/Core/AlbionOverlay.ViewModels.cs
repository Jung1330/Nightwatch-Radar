using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AlbionDataHandlers.Entities;
using Nightwatch.UserControls.AlbionOverlay.ViewModels;

namespace Nightwatch
{
    public partial class AlbionOverlay
    {
        // --- ViewModel Listeleri ---
        private List<PlayerViewModel> _playerViewModels = new List<PlayerViewModel>();

        // Bu metot arka planda (örneğin Update thread'inde veya Render'ın en başında) çalıştırılır.
        private void UpdateViewModels()
        {
            var mainPlayer = _gameStateManager.GetPlayer();
            
            lock (_dataLock)
            {
                _playerViewModels.Clear();

                foreach (var p in _playersBuffer)
                {
                    if (IsWhitelisted(p, mainPlayer)) continue;

                    var vm = new PlayerViewModel
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Guild = p.Guild,
                        Alliance = p.Alliance,
                        RawPlayer = p,
                        CurrentLerpedX = p.CurrentLerpedX,
                        CurrentLerpedY = p.CurrentLerpedY,
                        CurrentHealth = p.CurrentHealth,
                        MaxHealth = p.MaxHealth
                    };

                    // Mesafe Hesaplama
                    float dist = 0;
                    if (mainPlayer != null)
                    {
                        dist = Vector2.Distance(
                            new Vector2(p.CurrentLerpedX, p.CurrentLerpedY), 
                            new Vector2(mainPlayer.CurrentLerpedX, mainPlayer.CurrentLerpedY));
                    }
                    vm.DistanceToMainPlayer = dist;

                    // IP ve Ekipman Hesaplama
                    int pWeap = 0, pOff = 0, pCap = 0, pArm = 0, pShoe = 0, pCape = 0;
                    string wName = "-", oName = "-", hName = "-", aName = "-", sName = "-", cName = "-";

                    // A4: eşya dizisini TEK kez oku (paket thread'i arada değiştirirse IndexOutOfRange olur)
                    var eqArr = p.Equipment;
                    if (eqArr != null)
                    {
                        vm.EquipmentRaw = eqArr;
                        if (eqArr.Length > 0) { pWeap = GetItemPower(eqArr[0]); wName = GetItemName(eqArr[0]); }
                        if (eqArr.Length > 1) { pOff = GetItemPower(eqArr[1]); oName = GetItemName(eqArr[1]); }
                        if (eqArr.Length > 2) { pCap = GetItemPower(eqArr[2]); hName = GetItemName(eqArr[2]); }
                        if (eqArr.Length > 3) { pArm = GetItemPower(eqArr[3]); aName = GetItemName(eqArr[3]); }
                        if (eqArr.Length > 4) { pShoe = GetItemPower(eqArr[4]); sName = GetItemName(eqArr[4]); }
                        if (eqArr.Length > 6) { pCape = GetItemPower(eqArr[6]); cName = GetItemName(eqArr[6]); }
                    }

                    if (pWeap > 0 && pOff == 0) pOff = pWeap;

                    vm.WeaponIP = pWeap; vm.HeadIP = pCap; vm.ArmorIP = pArm; vm.ShoesIP = pShoe; vm.CapeIP = pCape;
                    vm.WeaponName = wName; vm.HeadName = hName; vm.ArmorName = aName; vm.ShoesName = sName; vm.CapeName = cName;
                    vm.AverageIP = (pWeap + pOff + pCap + pArm + pShoe + pCape) / 6;

                    // İsim Rengi
                    if (vm.AverageIP >= 1300) vm.NameColor = new Vector4(1.0f, 0.15f, 0.15f, 1);
                    else if (vm.AverageIP >= 1000) vm.NameColor = new Vector4(1.0f, 0.55f, 0.0f, 1);
                    else if (vm.AverageIP >= 700) vm.NameColor = new Vector4(1.0f, 0.95f, 0.2f, 1);
                    else if (vm.AverageIP > 0) vm.NameColor = new Vector4(0.3f, 1.0f, 0.3f, 1);
                    else vm.NameColor = new Vector4(0.7f, 0.7f, 0.7f, 1);

                    // Yön Oku (Düşman yaklaşıyor mu?)
                    vm.DirectionArrow = "  ";
                    vm.ArrowColor = new Vector4(0.7f, 0.7f, 0.7f, 1);
                    if (_prevPlayerPos.TryGetValue(p.Id, out var prev))
                    {
                        float deltaDist = dist - prev.dist;
                        if (MathF.Abs(deltaDist) > 0.5f)
                        {
                            if (deltaDist < 0) { vm.DirectionArrow = ">>"; vm.ArrowColor = new Vector4(1f, 0.3f, 0.3f, 1); }
                            else { vm.DirectionArrow = "<<"; vm.ArrowColor = new Vector4(0.4f, 0.9f, 0.4f, 1); }
                        }
                    }
                    _prevPlayerPos[p.Id] = (p.CurrentLerpedX, p.CurrentLerpedY, dist);

                    // Sağlık (HP) Metni
                    if (p.MaxHealth > 0)
                    {
                        vm.HealthRatio = Math.Clamp(p.CurrentHealth / p.MaxHealth, 0f, 1f);
                        var (displayHp, displayMax) = GetDisplayHealthValues(p.CurrentHealth, p.MaxHealth);
                        vm.HealthText = $"[{displayHp}/{displayMax}]";
                        
                        vm.HealthColor = vm.HealthRatio > 0.7f ? new Vector4(0.3f, 1f, 0.3f, 1f) :
                                         vm.HealthRatio > 0.4f ? new Vector4(1f, 0.8f, 0.2f, 1f) :
                                                                 new Vector4(1f, 0.2f, 0.2f, 1f);
                    }
                    else
                    {
                        vm.HealthText = "";
                        vm.HealthColor = new Vector4(1, 1, 1, 1);
                    }

                    _playerViewModels.Add(vm);
                }
            }
        }
    }
}
