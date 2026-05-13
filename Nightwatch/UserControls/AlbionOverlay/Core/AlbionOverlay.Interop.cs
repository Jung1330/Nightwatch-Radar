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
        #region DLL Imports & Constants

        // --- YENİ EKLENEN ÇOKLU EKRAN (MULTI-MONITOR) API'LERİ ---
        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);

        const int SM_XVIRTUALSCREEN = 76;
        const int SM_YVIRTUALSCREEN = 77;
        const int SM_CXVIRTUALSCREEN = 78;
        const int SM_CYVIRTUALSCREEN = 79;

        // --- YENİ: ANA MONİTÖR (PRIMARY) İÇİN SABİTLER ---
        const int SM_CXSCREEN = 0;
        const int SM_CYSCREEN = 1;

        // ---------------------------------------------------------

        // --- WINDOWS'UN DPI YALANLARINI ENGELLEYEN API ---
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        // StreamModule (OBS Bypass)
        [DllImport("user32.dll")]
        static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);
        private const uint WDA_NONE = 0x00000000;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
        // -------------------------------------------------

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        const uint SPI_GETWORKAREA = 0x0030;
        const int HWND_TOPMOST = -1;
        const uint SWP_SHOWWINDOW = 0x0040;
        const uint WM_SETICON = 0x0080;
        const int ICON_SMALL = 0;
        const int ICON_BIG = 1;
        const uint IMAGE_ICON = 1;
        const uint LR_LOADFROMFILE = 0x00000010;

        private const uint COL_RED = 0xFF0000FF;
        private const uint COL_GOLD = 0xFF00D7FF;
        private const uint COL_ORANGE = 0xFF00A5FF;
        private const uint COL_PURPLE = 0xFF800080;
        private const uint COL_PLAYER = 0xFF0000FF;
        private const uint COL_MIST = 0xFFFFA500;
        private const uint COL_SPECIAL = 0xFFFF00FF;

        private static readonly HashSet<int> _hiddenChestIds = new HashSet<int>
        {
            795, 796, 797, 798, 799, 800, 801, 802, 803, 804, 805, 806, 807, 808, 809,
            810, 811, 812, 813, 814, 815, 816, 817, 818, 819, 820, 821, 822, 823, 824,
            2637, 2638, 2639, 2640, 2641, 2642 // Corrupted Dungeon gizli sandıkları
        };

        private static readonly Dictionary<HarvestableCategory, string> _resourceMobNames = new()
        {
            { HarvestableCategory.Ore,   "Ore Elemental" },
            { HarvestableCategory.Log,   "Wood Elemental" },
            { HarvestableCategory.Hide,  "Hide Creature" },
            { HarvestableCategory.Fiber, "Fiber Creature" },
            { HarvestableCategory.Rock,  "Stone Elemental" }
        };
        #endregion
    }
}


