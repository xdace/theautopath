using UnityEngine;

namespace Betaknight.Overworld.Views
{
    /// <summary>
    /// Erzeugt Platzhalter-Grafiken zur Laufzeit, damit der Prototyp ohne Art-Assets läuft.
    /// Später einfach durch echte Sprites in den Views ersetzen.
    /// </summary>
    public static class ProceduralSprites
    {
        private const int Resolution = 128;
        private static Sprite _hex;
        private static Sprite _circle;
        private static Font _font;

        /// <summary>Spitz-oben Hexagon mit Aussenradius 1 Welteinheit (Höhe 2, Breite √3).</summary>
        public static Sprite Hex
        {
            get
            {
                if (_hex == null) _hex = CreateSprite("Hex", HexAlpha);
                return _hex;
            }
        }

        /// <summary>Kreis mit Radius 1 Welteinheit.</summary>
        public static Sprite Circle
        {
            get
            {
                if (_circle == null) _circle = CreateSprite("Circle", CircleAlpha);
                return _circle;
            }
        }

        public static Font LabelFont
        {
            get
            {
                if (_font == null)
                {
#if UNITY_2022_2_OR_NEWER
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                    _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
                }
                return _font;
            }
        }

        private delegate float AlphaFunc(float x, float y, float pixel);

        private static Sprite CreateSprite(string name, AlphaFunc alpha)
        {
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[Resolution * Resolution];
            float pixel = 2f / Resolution; // Breite eines Pixels in Welteinheiten (Textur deckt -1..1 ab)

            for (int py = 0; py < Resolution; py++)
            {
                for (int px = 0; px < Resolution; px++)
                {
                    float x = (px + 0.5f) * pixel - 1f;
                    float y = (py + 0.5f) * pixel - 1f;
                    byte a = (byte)(Mathf.Clamp01(alpha(x, y, pixel)) * 255f);
                    pixels[py * Resolution + px] = new Color32(255, 255, 255, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            // pixelsPerUnit so wählen, dass die Textur genau 2 Welteinheiten breit ist.
            return Sprite.Create(texture, new Rect(0, 0, Resolution, Resolution), new Vector2(0.5f, 0.5f), Resolution / 2f);
        }

        private static float HexAlpha(float x, float y, float pixel)
        {
            // Spitz-oben Hexagon mit Radius 1: |x| <= √3/2 und |y| + |x|/√3 <= 1.
            float ax = Mathf.Abs(x);
            float ay = Mathf.Abs(y);
            float d1 = 0.8660254f - ax;
            float d2 = (1f - ay - ax * 0.57735027f) * 0.8660254f; // in echten Abstand umgerechnet
            float distanceInside = Mathf.Min(d1, d2);
            return distanceInside / pixel + 0.5f; // weiche 1-Pixel-Kante
        }

        private static float CircleAlpha(float x, float y, float pixel)
        {
            float distanceInside = 1f - Mathf.Sqrt(x * x + y * y);
            return distanceInside / pixel + 0.5f;
        }
    }
}
