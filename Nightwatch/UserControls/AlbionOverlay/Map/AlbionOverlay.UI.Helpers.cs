using System;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using ImGuiNET;
using AlbionDataHandlers;
using AlbionDataHandlers.Enums;
using AlbionDataHandlers.Entities;
using AlbionDataHandlers.Handlers;
using AlbionDataHandlers.Utils;
using AlbionDataHandlers.Mappers;
using ClickableTransparentOverlay;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nightwatch.Managers;
using Nightwatch.UserControls.Language;

namespace Nightwatch
{
    public partial class AlbionOverlay
    {
        #region Göster Gizle Buton Ayarları (Show Hide Button Settings)
        private int GetPressedKey()
        {
            // Tüm sanal tuş kodlarını tara (Mouse butonları hariç genelde 0x08'den başlar)
            for (int i = 0x08; i <= 0xFF; i++)
            {
                // Mevcut toggle tuşunu algılamaması için kontrol (opsiyonel) veya direkt algıla
                if ((GetAsyncKeyState(i) & 0x8000) != 0)
                {
                    return i;
                }
            }
            return -1;
        }

        private string GetKeyName(int key)
        {
            // Basit bir eşleştirme (Daha fazlası eklenebilir)
            if (key >= 0x70 && key <= 0x87) return "F" + (key - 0x6F); // F1-F24
            if (key == 0x1B) return "ESC";
            if (key == 0x2D) return "INSERT";
            if (key == 0x2E) return "DELETE";
            if (key == 0x24) return "HOME";
            if (key == 0x23) return "END";
            if (key == 0x21) return "PG UP";
            if (key == 0x22) return "PG DOWN";
            if (key >= 0x30 && key <= 0x39) return ((char)key).ToString(); // 0-9
            if (key >= 0x41 && key <= 0x5A) return ((char)key).ToString(); // A-Z
            return "KEY " + key;
        }

        private void ImportWhitelistByGuildAlliance(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName)) return;

            lock (_dataLock)
            {
                var seed = _playersBuffer.FirstOrDefault(p => string.Equals(p.Name, playerName, StringComparison.OrdinalIgnoreCase));
                if (seed == null) return;

                if (_whitelistImportSameGuild && !string.IsNullOrWhiteSpace(seed.Guild))
                {
                    foreach (var p in _playersBuffer)
                    {
                        if (string.Equals(p.Guild, seed.Guild, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(p.Name))
                            _whitelist.Add(p.Name);
                    }
                }

                if (_whitelistImportSameAlliance && !string.IsNullOrWhiteSpace(seed.Alliance))
                {
                    foreach (var p in _playersBuffer)
                    {
                        if (string.Equals(p.Alliance, seed.Alliance, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(p.Name))
                            _whitelist.Add(p.Name);
                    }
                }
            }
        }
        #endregion
    }
}


