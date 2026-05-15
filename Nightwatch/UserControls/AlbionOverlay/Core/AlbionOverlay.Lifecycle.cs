#region Using Directives
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AlbionDataHandlers.Entities;
using AlbionDataHandlers.Handlers;
using ClickableTransparentOverlay;
using ImGuiNET;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nightwatch.Managers;
using Nightwatch.UserControls.Language;
#endregion

namespace Nightwatch
{
    public partial class AlbionOverlay
    {
        #region Font & Lifecycle

        // --- YENİ: DİLE GÖRE FONT VE KARAKTER SETİ (GLYPH RANGE) YÜKLEME ---
        // --- DİLE GÖRE FONT VE KARAKTER SETİ (GLYPH RANGE) YÜKLEME ---
        public unsafe void ApplyLanguageFont(string langCode)
        {
            ReplaceFont(config =>
            {
                var io = ImGui.GetIO();
                string basePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Helper");
                string fontPath = System.IO.Path.Combine(basePath, "Font.ttf"); // Varsayılan
                IntPtr ranges = IntPtr.Zero;

                // Varsayılan font boyutu
                float fontSize = 16.0f;

                // Eski font hafızasını serbest bırak (Memory Leak önleme)
                if (_trRangesHandle.IsAllocated)
                    _trRangesHandle.Free();

                switch (langCode)
                {
                    case "RU":
                        string ruPath = System.IO.Path.Combine(basePath, "FontRU.ttf");
                        if (File.Exists(ruPath)) fontPath = ruPath;
                        ranges = io.Fonts.GetGlyphRangesCyrillic();
                        break;

                    case "ZH":
                        string zhPath = System.IO.Path.Combine(basePath, "FontZH.ttf");
                        if (File.Exists(zhPath)) fontPath = zhPath;
                        ranges = io.Fonts.GetGlyphRangesChineseSimplifiedCommon();

                        // ÇİNCE İÇİN FONT BOYUTUNU BÜYÜTÜYORUZ!
                        fontSize = 20.0f;
                        break;

                    default: // TR ve EN için
                        _trRangesHandle = GCHandle.Alloc(_trRanges, GCHandleType.Pinned);
                        ranges = _trRangesHandle.AddrOfPinnedObject();
                        break;
                }

                if (File.Exists(fontPath))
                {
                    // Artık sabit 16.0f yerine fontSize değişkenini kullanıyoruz
                    io.Fonts.AddFontFromFileTTF(fontPath, fontSize, config, ranges);
                }
            });
        }

        protected override unsafe System.Threading.Tasks.Task PostInitialized()
        {
            string startupLang = "TR"; // Varsayılan

            // --- KALICI DİL YÜKLEME ---
            try
            {
                string langPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "lang.txt");
                if (File.Exists(langPath))
                {
                    startupLang = File.ReadAllText(langPath).Trim().ToUpper();
                    Lang.LoadLanguage(startupLang);

                    _selectedLangIndex = startupLang switch
                    {
                        "EN" => 1,
                        "RU" => 2,
                        "ZH" => 3,
                        _ => 0
                    };
                }
            }
            catch { }

            // Açılışta okunan dile uygun FONT'u ve karakter setini yükle!
            ApplyLanguageFont(startupLang);

            return base.PostInitialized();
        }

        // Standart IDisposable deseni (Kaynak Temizliği)
        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                if (_trRangesHandle.IsAllocated)
                    _trRangesHandle.Free();

                _trayContextMenu?.Dispose();
                _trayIcon?.Dispose();

                foreach (var path in _resourceCache.Values)
                {
                    try { if (File.Exists(path)) File.Delete(path); }
                    catch { }
                }
                _resourceCache.Clear();
            }

            if (_hIconBig != IntPtr.Zero)
            {
                DestroyIcon(_hIconBig);
                _hIconBig = IntPtr.Zero;
            }
            if (_hIconSmall != IntPtr.Zero)
            {
                DestroyIcon(_hIconSmall);
                _hIconSmall = IntPtr.Zero;
            }
        }

        ~AlbionOverlay()
        {
            Dispose(false);
        }

        #endregion
    }
}