using UnityEngine;

namespace GravityShift
{
    internal static class Palette
    {
        public static readonly Color Psychic = Rgb(0xc78bff);
        public static readonly Color Engineer = Rgb(0x6fffb0);
        public static readonly Color Spikes = Rgb(0xff4d6d);
        public static readonly Color Dash = Rgb(0x9fe8ff);
        public static readonly Color Sparks = Rgb(0xffd25f);
        public static readonly Color Walker = Rgb(0xff6b5c);
        public static readonly Color Anchor = Rgb(0x6d5ce8);
        public static readonly Color Inverter = Rgb(0xff8a3d);
        public static readonly Color Ink = Rgb(0x070b10);
        public static readonly Color Ash = Rgb(0xeaf6ff);

        public static readonly Color[] Backgrounds = { Rgb(0x0e1822), Rgb(0x1a1026), Rgb(0x05070d) };
        public static readonly Color[] Walls = { Rgb(0x2f4a60), Rgb(0x4d3466), Rgb(0x3a3f52) };
        public static readonly Color[] WallEdges = { Rgb(0x4f7a96), Rgb(0x7d5aa3), Rgb(0x6a7090) };
        public static readonly Color[] Accents = { Rgb(0x5fd3ff), Rgb(0xe0a8ff), Sparks };

        public static Color Rgb(int hex) => new Color32((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);
        public static Color Alpha(Color color, float opacity) => new Color(color.r, color.g, color.b, opacity);
    }
}
