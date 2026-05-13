using ImGuiNET;
using System.Numerics;
using System.Collections.Generic;

namespace AlbionRadar.UserControls
{
    public static class MentalityTheme
    {
        // Animasyon durumlarýný hafýzada tutmak için
        private static Dictionary<uint, float> _animations = new Dictionary<uint, float>();

        // Renk Paleti (Mentality v2'ye uygun)
        public static class Colors
        {
            public static uint Background = ImGui.GetColorU32(new Vector4(0.09f, 0.09f, 0.10f, 1.0f)); // Koyu Gri
            public static uint Accent = ImGui.GetColorU32(new Vector4(0.22f, 0.52f, 0.92f, 1.0f));     // Obsidian Blue
            public static uint Text = ImGui.GetColorU32(new Vector4(0.95f, 0.95f, 0.95f, 1.0f));       // Açýk Beyaz
            public static uint Inactive = ImGui.GetColorU32(new Vector4(0.18f, 0.18f, 0.19f, 1.0f));   // Pasif Gri
        }

        // --- Temayý Baþlatan Fonksiyon ---
        // Bunu Render döngüsünün en baþýnda bir kere çaðýrmalýsýn.
        public static void Setup()
        {
            var style = ImGui.GetStyle();

            // Yuvarlak hatlar
            style.WindowRounding = 10.0f;
            style.FrameRounding = 4.0f;
            style.GrabRounding = 4.0f;
            style.PopupRounding = 5.0f;
            style.ScrollbarRounding = 4.0f;

            // Padding ve Spacing
            style.WindowPadding = new Vector2(15, 15);
            style.FramePadding = new Vector2(10, 6);
            style.ItemSpacing = new Vector2(10, 10);

            // Renk Atamalarý (Global)
            style.Colors[(int)ImGuiCol.WindowBg] = new Vector4(0.11f, 0.13f, 0.19f, 0.95f);
            style.Colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.11f, 0.13f, 0.19f, 1.0f);
            style.Colors[(int)ImGuiCol.Border] = new Vector4(0.13f, 0.19f, 0.25f, 0.25f); // Nightwatch border
            style.Colors[(int)ImGuiCol.Text] = new Vector4(0.95f, 0.95f, 0.95f, 1.0f);
        }

        // --- Yardýmcý Lerp (Yumuþak Geçiþ) Fonksiyonu ---
        private static float Lerp(float start, float end, float delta)
        {
            return start + (end - start) * delta;
        }

        // --- Mentality Tarzý Checkbox ---
        public static bool Checkbox(string label, ref bool v)
        {
            var id = ImGui.GetID(label);
            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();

            float size = 20.0f;
            float textOffset = 10.0f;

            // Týklanabilir alan oluþtur (Görünmez buton)
            bool clicked = ImGui.InvisibleButton(label, new Vector2(size + 200, size)); // Geniþlik tahmini

            if (clicked) v = !v;

            bool hovered = ImGui.IsItemHovered();

            // Animasyon (0.0 -> 1.0 arasý geçiþ)
            if (!_animations.ContainsKey(id)) _animations[id] = 0.0f;
            float target = v ? 1.0f : 0.0f;
            _animations[id] = Lerp(_animations[id], target, ImGui.GetIO().DeltaTime * 14.0f); // Hýz
            float anim = _animations[id];

            // Arka Plan (Pasif)
            drawList.AddRectFilled(pos, new Vector2(pos.X + size, pos.Y + size), Colors.Inactive, 4.0f);

            // Aktif Dolum Animasyonu (Fade)
            if (anim > 0.01f)
            {
                // Mor dolgu, alpha deðerine göre parlar
                uint animColor = ImGui.GetColorU32(new Vector4(0.13f, 0.19f, 0.25f, anim));
                drawList.AddRectFilled(pos, new Vector2(pos.X + size, pos.Y + size), animColor, 4.0f);

                // Basit Tik iþareti (V)
                if (anim > 0.5f)
                {
                    drawList.AddLine(new Vector2(pos.X + 5, pos.Y + 10), new Vector2(pos.X + 8, pos.Y + 14), ImGui.GetColorU32(new Vector4(1, 1, 1, anim)), 2f);
                    drawList.AddLine(new Vector2(pos.X + 8, pos.Y + 14), new Vector2(pos.X + 15, pos.Y + 5), ImGui.GetColorU32(new Vector4(1, 1, 1, anim)), 2f);
                }
            }

            // Çerçeve (Hover efekti)
            if (hovered)
                drawList.AddRect(pos, new Vector2(pos.X + size, pos.Y + size), Colors.Accent, 4.0f);

            // Yazý
            drawList.AddText(new Vector2(pos.X + size + textOffset, pos.Y + (size / 2) - (ImGui.GetFontSize() / 2)), Colors.Text, label);

            // Hizalama için boþluk býrak
            // ImGui.Dummy(new Vector2(size, size)); 

            return clicked;
        }

        // --- Mentality Tarzý Buton ---
        public static bool Button(string label)
        {
            var id = ImGui.GetID(label);
            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var textSize = ImGui.CalcTextSize(label);
            var size = new Vector2(textSize.X + 30, textSize.Y + 10); // Otomatik boyut

            bool clicked = ImGui.InvisibleButton(label, size);
            bool hovered = ImGui.IsItemHovered();
            bool active = ImGui.IsItemActive();

            // Animasyon
            if (!_animations.ContainsKey(id)) _animations[id] = 0.0f;
            float target = active ? 1.0f : (hovered ? 0.3f : 0.0f);
            _animations[id] = Lerp(_animations[id], target, ImGui.GetIO().DeltaTime * 10.0f);
            float anim = _animations[id];

            // Renk Hesaplama
            // Baz renk (Inactive) ile Mor (Accent) arasýnda animasyonlu geçiþ
            uint rectCol = ImGui.GetColorU32(new Vector4(
                0.06f + ((0.13f - 0.06f) * anim),
                0.13f + ((0.19f - 0.13f) * anim),
                0.19f + ((0.25f - 0.19f) * anim),
                1.0f));

            drawList.AddRectFilled(pos, pos + size, rectCol, 5.0f);
            drawList.AddText(new Vector2(pos.X + 15, pos.Y + 5), Colors.Text, label);

            return clicked;
        }
    }
}


