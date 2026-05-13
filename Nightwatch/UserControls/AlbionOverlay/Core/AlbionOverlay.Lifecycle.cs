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
        #region Font
        protected override unsafe System.Threading.Tasks.Task PostInitialized()
        {
            string fontPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Helper", "Font.ttf");

            if (File.Exists(fontPath))
            {
                ReplaceFont(config =>
                {
                    _trRangesHandle = GCHandle.Alloc(_trRanges, GCHandleType.Pinned);
                    ImGui.GetIO().Fonts.AddFontFromFileTTF(fontPath, 16.0f, config, _trRangesHandle.AddrOfPinnedObject());
                });
            }

            return base.PostInitialized();
        }

        // Standart IDisposable deseni (managed + unmanaged kaynak ayrımı)
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
                // Managed kaynaklar
                if (_trRangesHandle.IsAllocated)
                    _trRangesHandle.Free();

                _trayContextMenu?.Dispose();
                _trayIcon?.Dispose();

                // Temp PNG dosyalarını temizle
                foreach (var path in _resourceCache.Values)
                {
                    try { if (File.Exists(path)) File.Delete(path); }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Error Code : 28 | {ex.Message}");
                        /* Silme başarısız olursa sessizce geç */
                    }
                }
                _resourceCache.Clear();
            }

            // Unmanaged GDI kaynaklar (disposing=false olsa da serbest bırak)
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


