using ImGuiNET;
using System;
using System.Numerics;
using System.Collections.Generic;

namespace Nightwatch.UserControls
{
    public enum ThemeType
    {
        DeepSpaceBlack,
        Obsidian,
        BloodMoon
    }

    public static class MentalityTheme
    {
        private static Dictionary<uint, float> _animations = new Dictionary<uint, float>();
        private static Dictionary<uint, float> _hoverAnimations = new Dictionary<uint, float>();
        
        public static ThemeType CurrentTheme = ThemeType.DeepSpaceBlack;

        public static class Colors
        {
            public static Vector4 Background       = new Vector4(10f/255f, 10f/255f, 10f/255f, 0.98f); 
            public static Vector4 Sidebar          = new Vector4(14f/255f, 14f/255f, 14f/255f, 1.0f); 
            public static Vector4 Card             = new Vector4(18f/255f, 18f/255f, 18f/255f, 1.0f);
            public static Vector4 CardHover        = new Vector4(24f/255f, 24f/255f, 24f/255f, 1.0f);
            public static Vector4 InputBg          = new Vector4(22f/255f, 22f/255f, 22f/255f, 1.0f);
            public static Vector4 InputBgHover     = new Vector4(28f/255f, 28f/255f, 28f/255f, 1.0f);

            public static Vector4 AccentPrimary    = new Vector4(108f/255f, 92f/255f, 231f/255f, 1.0f);
            public static Vector4 AccentPrimaryLt  = new Vector4(162f/255f, 155f/255f, 254f/255f, 1.0f);
            public static Vector4 AccentSecondary  = new Vector4(0f/255f, 206f/255f, 201f/255f, 1.0f);
            public static Vector4 AccentDanger     = new Vector4(255f/255f, 107f/255f, 107f/255f, 1.0f);
            public static Vector4 AccentSuccess    = new Vector4(81f/255f, 207f/255f, 102f/255f, 1.0f);
            public static Vector4 AccentWarning    = new Vector4(255f/255f, 212f/255f, 59f/255f, 1.0f);

            public static Vector4 TextPrimary      = new Vector4(241f/255f, 243f/255f, 245f/255f, 1.0f);
            public static Vector4 TextSecondary    = new Vector4(134f/255f, 142f/255f, 150f/255f, 1.0f);
            public static Vector4 TextMuted        = new Vector4(73f/255f, 80f/255f, 87f/255f, 1.0f);

            public static Vector4 Border           = new Vector4(30f/255f, 41f/255f, 59f/255f, 1.0f);
            public static Vector4 BorderLight      = new Vector4(30f/255f, 41f/255f, 59f/255f, 0.5f);
            public static Vector4 BorderGlow       = new Vector4(108f/255f, 92f/255f, 231f/255f, 0.15f);

            public static Vector4 GlowPurple       = new Vector4(108f/255f, 92f/255f, 231f/255f, 0.25f);
            public static Vector4 GlowTeal         = new Vector4(0f/255f, 206f/255f, 201f/255f, 0.20f);
            public static Vector4 Transparent      = new Vector4(0, 0, 0, 0);

            public static uint TextPrimaryU32 => ImGui.ColorConvertFloat4ToU32(TextPrimary);
            public static uint TextSecondaryU32 => ImGui.ColorConvertFloat4ToU32(TextSecondary);
            public static uint TextMutedU32 => ImGui.ColorConvertFloat4ToU32(TextMuted);
            public static uint AccentPrimaryU32 => ImGui.ColorConvertFloat4ToU32(AccentPrimary);
            public static uint AccentSecondaryU32 => ImGui.ColorConvertFloat4ToU32(AccentSecondary);
            public static uint AccentDangerU32 => ImGui.ColorConvertFloat4ToU32(AccentDanger);
            public static uint AccentSuccessU32 => ImGui.ColorConvertFloat4ToU32(AccentSuccess);
            public static uint AccentWarningU32 => ImGui.ColorConvertFloat4ToU32(AccentWarning);
            public static uint CardU32 => ImGui.ColorConvertFloat4ToU32(Card);
            public static uint BorderU32 => ImGui.ColorConvertFloat4ToU32(Border);
            public static uint SidebarU32 => ImGui.ColorConvertFloat4ToU32(Sidebar);
            public static uint GlowPurpleU32 => ImGui.ColorConvertFloat4ToU32(GlowPurple);

            public static void ApplyTheme(ThemeType theme)
            {
                CurrentTheme = theme;
                switch (theme)
                {
                    case ThemeType.DeepSpaceBlack:
                        Background       = new Vector4(10f/255f, 10f/255f, 10f/255f, 0.98f); 
                        Sidebar          = new Vector4(14f/255f, 14f/255f, 14f/255f, 1.0f); 
                        Card             = new Vector4(18f/255f, 18f/255f, 18f/255f, 1.0f);
                        CardHover        = new Vector4(24f/255f, 24f/255f, 24f/255f, 1.0f);
                        InputBg          = new Vector4(22f/255f, 22f/255f, 22f/255f, 1.0f);
                        InputBgHover     = new Vector4(28f/255f, 28f/255f, 28f/255f, 1.0f);
                        AccentPrimary    = new Vector4(108f/255f, 92f/255f, 231f/255f, 1.0f); // Keep purple accent
                        AccentPrimaryLt  = new Vector4(162f/255f, 155f/255f, 254f/255f, 1.0f);
                        GlowPurple       = new Vector4(108f/255f, 92f/255f, 231f/255f, 0.25f);
                        break;
                    case ThemeType.Obsidian:
                        Background       = new Vector4(10f/255f, 10f/255f, 10f/255f, 0.98f); 
                        Sidebar          = new Vector4(15f/255f, 15f/255f, 15f/255f, 1.0f); 
                        Card             = new Vector4(20f/255f, 20f/255f, 20f/255f, 1.0f);
                        CardHover        = new Vector4(26f/255f, 26f/255f, 26f/255f, 1.0f);
                        InputBg          = new Vector4(12f/255f, 12f/255f, 12f/255f, 1.0f);
                        InputBgHover     = new Vector4(18f/255f, 18f/255f, 18f/255f, 1.0f);
                        AccentPrimary    = new Vector4(255f/255f, 184f/255f, 108f/255f, 1.0f); 
                        AccentPrimaryLt  = new Vector4(255f/255f, 204f/255f, 128f/255f, 1.0f);
                        GlowPurple       = new Vector4(255f/255f, 184f/255f, 108f/255f, 0.25f);
                        break;
                    case ThemeType.BloodMoon:
                        Background       = new Vector4(12f/255f, 5f/255f, 5f/255f, 0.98f); 
                        Sidebar          = new Vector4(18f/255f, 8f/255f, 8f/255f, 1.0f); 
                        Card             = new Vector4(25f/255f, 12f/255f, 12f/255f, 1.0f);
                        CardHover        = new Vector4(32f/255f, 16f/255f, 16f/255f, 1.0f);
                        InputBg          = new Vector4(16f/255f, 8f/255f, 8f/255f, 1.0f);
                        InputBgHover     = new Vector4(22f/255f, 12f/255f, 12f/255f, 1.0f);
                        AccentPrimary    = new Vector4(220f/255f, 53f/255f, 69f/255f, 1.0f); 
                        AccentPrimaryLt  = new Vector4(235f/255f, 85f/255f, 100f/255f, 1.0f);
                        GlowPurple       = new Vector4(220f/255f, 53f/255f, 69f/255f, 0.25f);
                        break;
                }
                MentalityTheme.Setup();
            }
        }

        public static void SetTheme(ThemeType theme) { Colors.ApplyTheme(theme); }

        public static void Setup()
        {
            var style = ImGui.GetStyle();

            style.WindowRounding = 14f;
            style.ChildRounding = 12f;
            style.FrameRounding = 10f;
            style.PopupRounding = 10f;
            style.ScrollbarRounding = 8f;
            style.GrabRounding = 8f;
            style.WindowBorderSize = 1f;
            style.ChildBorderSize = 1f;

            style.FrameBorderSize = 0f;
            style.WindowPadding = new Vector2(12, 12);
            style.FramePadding = new Vector2(8, 6);
            style.ItemSpacing = new Vector2(8, 6);

            style.Colors[(int)ImGuiCol.Text] = Colors.TextPrimary;
            style.Colors[(int)ImGuiCol.TextDisabled] = Colors.TextMuted;
            style.Colors[(int)ImGuiCol.WindowBg] = Colors.Background;
            style.Colors[(int)ImGuiCol.ChildBg] = Colors.Card;
            style.Colors[(int)ImGuiCol.Border] = Colors.Border;
            style.Colors[(int)ImGuiCol.FrameBg] = Colors.InputBg;
            style.Colors[(int)ImGuiCol.FrameBgHovered] = Colors.InputBgHover;
            style.Colors[(int)ImGuiCol.FrameBgActive] = Colors.AccentPrimary;
            
            style.Colors[(int)ImGuiCol.Header] = Colors.CardHover;
            style.Colors[(int)ImGuiCol.HeaderHovered] = Colors.AccentPrimaryLt;
            style.Colors[(int)ImGuiCol.HeaderActive] = Colors.AccentPrimary;
            
            style.Colors[(int)ImGuiCol.SliderGrab] = Colors.AccentPrimary;
            style.Colors[(int)ImGuiCol.SliderGrabActive] = Colors.AccentPrimaryLt;

            style.Colors[(int)ImGuiCol.Tab] = Colors.Card;
            style.Colors[(int)ImGuiCol.TabHovered] = Colors.AccentPrimaryLt;
            style.Colors[(int)ImGuiCol.TabSelected] = Colors.AccentPrimary;
            style.Colors[(int)ImGuiCol.TabSelectedOverline] = Colors.AccentPrimary;
            style.Colors[(int)ImGuiCol.TabDimmed] = Colors.InputBg;
            style.Colors[(int)ImGuiCol.TabDimmedSelected] = Colors.AccentPrimary;
        }

        private static float Lerp(float start, float end, float delta) => start + (end - start) * Math.Clamp(delta, 0f, 1f);

        private static Vector4 LerpColor(Vector4 a, Vector4 b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return new Vector4(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, a.W + (b.W - a.W) * t);
        }

        public static void SectionHeader(string title, Vector4? customColor = null)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8f);
            var dl = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            Vector4 color = customColor ?? Colors.AccentPrimaryLt;

            ImGui.TextColored(color, title);
            ImGui.Spacing();

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8f);
            float width = ImGui.GetContentRegionAvail().X;
            Vector2 lineStart = new Vector2(pos.X, ImGui.GetCursorScreenPos().Y - 2);
            Vector2 lineEnd = new Vector2(pos.X + width, lineStart.Y);

            uint startCol = ImGui.ColorConvertFloat4ToU32(color);
            uint endCol = ImGui.ColorConvertFloat4ToU32(new Vector4(color.X, color.Y, color.Z, 0.0f));
            dl.AddRectFilledMultiColor(lineStart, new Vector2(lineEnd.X, lineEnd.Y + 2), startCol, endCol, endCol, startCol);
        }

        public static void GradientSeparator(Vector4? color = null, float thickness = 1f)
        {
            var dl = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            float width = ImGui.GetContentRegionAvail().X;

            Vector4 c = color ?? Colors.Border;
            
            uint startCol = ImGui.ColorConvertFloat4ToU32(new Vector4(c.X, c.Y, c.Z, c.W * 0.8f));
            uint midCol = ImGui.ColorConvertFloat4ToU32(new Vector4(c.X, c.Y, c.Z, c.W * 0.3f));
            uint endCol = ImGui.ColorConvertFloat4ToU32(new Vector4(c.X, c.Y, c.Z, 0.0f));

            dl.AddRectFilledMultiColor(pos, new Vector2(pos.X + width * 0.5f, pos.Y + thickness), startCol, midCol, midCol, startCol);
            dl.AddRectFilledMultiColor(new Vector2(pos.X + width * 0.5f, pos.Y), new Vector2(pos.X + width, pos.Y + thickness), midCol, endCol, endCol, midCol);

            ImGui.Dummy(new Vector2(0, thickness + 4));
        }

        public static bool GlowButton(string label, Vector2? size = null)
        {
            var id = ImGui.GetID(label);
            string displayLabel = label;
            int hashIdx = label.IndexOf("##");
            if (hashIdx >= 0) displayLabel = label.Substring(0, hashIdx);

            var dl = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var textSize = ImGui.CalcTextSize(displayLabel);
            var btnSize = size ?? new Vector2(Math.Max(textSize.X + 32, 100), textSize.Y + 16);

            bool clicked = ImGui.InvisibleButton(label, btnSize);
            bool hovered = ImGui.IsItemHovered();
            bool active = ImGui.IsItemActive();

            if (!_hoverAnimations.ContainsKey(id)) _hoverAnimations[id] = 0f;
            float target = active ? 1.0f : (hovered ? 0.7f : 0f);
            _hoverAnimations[id] = Lerp(_hoverAnimations[id], target, ImGui.GetIO().DeltaTime * 12f);
            float anim = _hoverAnimations[id];

            if (anim > 0.01f)
            {
                float glowSize = 6f * anim;
                uint glowCol = ImGui.ColorConvertFloat4ToU32(new Vector4(Colors.AccentPrimary.X, Colors.AccentPrimary.Y, Colors.AccentPrimary.Z, 0.25f * anim));
                dl.AddRectFilled(pos - new Vector2(glowSize), pos + btnSize + new Vector2(glowSize), glowCol, 14f);
            }

            Vector4 btnColor = LerpColor(Colors.AccentPrimary, Colors.AccentPrimaryLt, anim * 0.5f);
            if (active) btnColor = new Vector4(btnColor.X * 0.85f, btnColor.Y * 0.85f, btnColor.Z * 0.85f, 1f);
            dl.AddRectFilled(pos, pos + btnSize, ImGui.ColorConvertFloat4ToU32(btnColor), 8f);

            Vector2 textPos = pos + (btnSize - textSize) * 0.5f;
            dl.AddText(textPos, Colors.TextPrimaryU32, displayLabel);

            return clicked;
        }

        public static bool Checkbox(string label, ref bool v)
        {
            var id = ImGui.GetID(label);
            string displayLabel = label;
            int hashIdx = label.IndexOf("##");
            if (hashIdx >= 0) displayLabel = label.Substring(0, hashIdx);

            var dl = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();

            float size = 20f;
            float textOffset = 10f;
            float totalWidth = size + textOffset + ImGui.CalcTextSize(displayLabel).X;

            bool clicked = ImGui.InvisibleButton(label, new Vector2(totalWidth, size + 2));
            if (clicked) v = !v;
            bool hovered = ImGui.IsItemHovered();

            if (!_animations.ContainsKey(id)) _animations[id] = v ? 1f : 0f;
            float target = v ? 1f : 0f;
            _animations[id] = Lerp(_animations[id], target, ImGui.GetIO().DeltaTime * 14f);
            float anim = _animations[id];

            float rounding = 5f;

            Vector4 bgColor = LerpColor(Colors.InputBg, Colors.AccentPrimary, anim);
            dl.AddRectFilled(pos, new Vector2(pos.X + size, pos.Y + size), ImGui.ColorConvertFloat4ToU32(bgColor), rounding);

            Vector4 borderColor = hovered ? Colors.AccentPrimaryLt : (v ? Colors.AccentPrimary : Colors.Border);
            dl.AddRect(pos, new Vector2(pos.X + size, pos.Y + size), ImGui.ColorConvertFloat4ToU32(borderColor), rounding, ImDrawFlags.None, 1.5f);

            if (anim > 0.3f)
            {
                uint checkCol = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, anim));
                dl.AddLine(new Vector2(pos.X + 5, pos.Y + 10), new Vector2(pos.X + 8, pos.Y + 14), checkCol, 2.2f);
                dl.AddLine(new Vector2(pos.X + 8, pos.Y + 14), new Vector2(pos.X + 15, pos.Y + 5), checkCol, 2.2f);
            }

            dl.AddText(new Vector2(pos.X + size + textOffset, pos.Y + (size / 2) - (ImGui.GetFontSize() / 2)), hovered ? Colors.TextPrimaryU32 : Colors.TextSecondaryU32, displayLabel);
            return clicked;
        }

        public static bool AnimatedToggle(string label, ref bool v)
        {
            var id = ImGui.GetID(label);
            string displayLabel = label;
            int hashIdx = label.IndexOf("##");
            if (hashIdx >= 0) displayLabel = label.Substring(0, hashIdx);

            var dl = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();

            float width = 42f, height = 22f;
            float radius = height * 0.5f;
            float textOffset = 10f;
            float totalWidth = width + textOffset + ImGui.CalcTextSize(displayLabel).X;

            bool clicked = ImGui.InvisibleButton(label, new Vector2(totalWidth, height + 2));
            if (clicked) v = !v;
            bool hovered = ImGui.IsItemHovered();

            if (!_animations.ContainsKey(id)) _animations[id] = v ? 1f : 0f;
            float target = v ? 1f : 0f;
            _animations[id] = Lerp(_animations[id], target, ImGui.GetIO().DeltaTime * 12f);
            float anim = _animations[id];

            Vector4 trackColor = LerpColor(Colors.InputBg, Colors.AccentPrimary, anim);
            if (hovered && !v) trackColor = LerpColor(trackColor, Colors.AccentPrimary, 0.2f);
            dl.AddRectFilled(pos, new Vector2(pos.X + width, pos.Y + height), ImGui.ColorConvertFloat4ToU32(trackColor), radius);

            dl.AddRect(pos, new Vector2(pos.X + width, pos.Y + height), ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 0.1f)), radius, ImDrawFlags.None, 1f);

            float knobRadius = height * 0.5f - 3f;
            float knobX = Lerp(pos.X + radius, pos.X + width - radius, anim);
            float knobY = pos.Y + radius;

            dl.AddCircleFilled(new Vector2(knobX + 1, knobY + 1), knobRadius + 1, ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.3f)));
            dl.AddCircleFilled(new Vector2(knobX, knobY), knobRadius, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 0.95f)));

            dl.AddText(new Vector2(pos.X + width + textOffset, pos.Y + (height / 2) - (ImGui.GetFontSize() / 2)), hovered ? Colors.TextPrimaryU32 : Colors.TextSecondaryU32, displayLabel);
            return clicked;
        }

        public static bool TreeNode(string label, bool defaultOpen = false)
        {
            ImGui.PushStyleColor(ImGuiCol.Header, Colors.CardHover);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Colors.AccentPrimaryLt);
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, Colors.AccentPrimary);
            ImGui.PushStyleColor(ImGuiCol.Text, Colors.TextPrimary);
            
            ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.Framed | ImGuiTreeNodeFlags.SpanAvailWidth;
            if (defaultOpen) flags |= ImGuiTreeNodeFlags.DefaultOpen;
            
            bool isOpen = ImGui.TreeNodeEx(label, flags);
            ImGui.PopStyleColor(4);
            return isOpen;
        }

        public static void StatusBadge(string text, Vector4 color, float parentWidth = 0f)
        {
            var textSize = ImGui.CalcTextSize(text);
            float padX = 10f, padY = 4f;
            Vector2 totalSize = new Vector2(textSize.X + padX * 2 + 12, textSize.Y + padY * 2);

            if (parentWidth > 0)
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (parentWidth - totalSize.X) / 2f);
            }

            var dl = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();

            float rounding = totalSize.Y * 0.5f;
            Vector4 bgColor = new Vector4(color.X, color.Y, color.Z, 0.15f);
            dl.AddRectFilled(pos, pos + totalSize, ImGui.ColorConvertFloat4ToU32(bgColor), rounding);

            float dotRadius = 4f;
            Vector2 dotPos = new Vector2(pos.X + padX + 4f, pos.Y + totalSize.Y * 0.5f);
            
            dl.AddCircleFilled(dotPos, dotRadius, ImGui.ColorConvertFloat4ToU32(color));
            dl.AddCircleFilled(dotPos, dotRadius + 2f, ImGui.ColorConvertFloat4ToU32(new Vector4(color.X, color.Y, color.Z, 0.3f)));

            dl.AddText(new Vector2(pos.X + padX + dotRadius * 2 + 6, pos.Y + padY), ImGui.ColorConvertFloat4ToU32(color), text);
            ImGui.Dummy(totalSize);
        }

        public static void BeginCard(string id, float height = 0)
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Colors.Card);
            ImGui.PushStyleColor(ImGuiCol.Border, Colors.Border);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 10f);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1f);

            Vector2 size = height > 0 ? new Vector2(0, height) : new Vector2(0, 0);
            ImGui.BeginChild(id, size, ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY);
            ImGui.Indent(8);
        }

        public static void EndCard()
        {
            ImGui.Unindent(8);
            ImGui.EndChild();
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(2);
        }

        public static bool Button(string label, Vector2? size = null) => GlowButton(label, size);

        public static bool SmallButton(string label, Vector4? color = null)
        {
            string displayLabel = label;
            int hashIdx = label.IndexOf("##");
            if (hashIdx >= 0) displayLabel = label.Substring(0, hashIdx);
            float fixedWidth = 70f;
            float h = ImGui.CalcTextSize(displayLabel).Y + 8;
            return GlowButton(label, new Vector2(fixedWidth, h));
        }

        public static void Breadcrumb(string parentName, string currentPage)
        {
            ImGui.TextColored(Colors.TextMuted, parentName); ImGui.SameLine(0, 4);
            ImGui.TextColored(Colors.TextMuted, ">"); ImGui.SameLine(0, 4);
            ImGui.TextColored(Colors.TextPrimary, currentPage); ImGui.Spacing();
            GradientSeparator(Colors.AccentPrimary, 1f);
        }

        public static void InfoRow(string label, string value, Vector4? valueColor = null)
        {
            ImGui.TextColored(Colors.TextSecondary, label); ImGui.SameLine();
            ImGui.TextColored(valueColor ?? Colors.TextPrimary, value);
        }
    }
}
