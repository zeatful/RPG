using System.Collections.Generic;
using RPG.Content;
using RPG.Items;
using UnityEngine;

namespace RPG.UI
{
    /// <summary>
    /// Pixel-art item icons drawn in code on a 32x32 canvas: shapes per item kind, coloured by the item
    /// (so tiers recolour automatically), with an automatic dark outline. Cached per item id.
    /// </summary>
    public static class ItemIcons
    {
        const int N = 32;
        const int Upscale = 4;

        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        static readonly Color Outline = new Color(0.08f, 0.06f, 0.05f, 1f);
        static readonly Color Wood = Hex("#5A3A22");
        static readonly Color Gold = Hex("#D9B23A");
        static readonly Color Steel = Hex("#C8CDD2");
        static readonly Color Bone = Hex("#EDE6D6");
        static readonly Color Blood = Hex("#B8433E");
        static readonly Color Leaf = Hex("#5E8C31");
        static readonly Color Thistle = Hex("#9B59B6");
        static readonly Color Spark = Hex("#FFB347");
        static readonly Color White = new Color(1f, 0.98f, 0.92f, 1f);

        public static Texture2D Get(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            Texture2D texture;
            if (cache.TryGetValue(itemId, out texture) && texture != null) return texture;
            ItemInfo info = GameContent.Item(itemId);
            texture = Build(info, itemId);
            cache[itemId] = texture;
            return texture;
        }

        public static Sprite GetSprite(string itemId, float worldSize)
        {
            Sprite sprite;
            if (sprites.TryGetValue(itemId, out sprite) && sprite != null) return sprite;
            Texture2D texture = Get(itemId);
            if (texture == null) return null;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width / worldSize);
            sprites[itemId] = sprite;
            return sprite;
        }

        enum Kind { Sword, Bow, Wand, Arrows, Plate, Leather, Logs, Ore, Bar, Meat, Bones, Hide, Coins, Gem, Spark, Seed, Map, Crown, Quill, Fang, Hook, Pet, Sack, Axe, Pickaxe, Hammer, Flint, Bread, Cheese, Pie }

        static Kind KindOf(string id)
        {
            if (id.StartsWith("pickaxe")) return Kind.Pickaxe;
            if (id.StartsWith("axe_")) return Kind.Axe;
            if (id == "hammer") return Kind.Hammer;
            if (id.StartsWith("flint")) return Kind.Flint;
            if (id == "bread") return Kind.Bread;
            if (id == "cheese") return Kind.Cheese;
            if (id.Contains("pie")) return Kind.Pie;
            if (id.Contains("steak")) return Kind.Meat;
            if (id.StartsWith("sword")) return Kind.Sword;
            if (id.Contains("bow")) return Kind.Bow;
            if (id.StartsWith("wand")) return Kind.Wand;
            if (id.Contains("arrow")) return Kind.Arrows;
            if (id.StartsWith("platebody")) return Kind.Plate;
            if (id.EndsWith("_body")) return Kind.Leather;
            if (id.StartsWith("logs")) return Kind.Logs;
            if (id.StartsWith("ore_")) return Kind.Ore;
            if (id.StartsWith("bar_")) return Kind.Bar;
            if (id.Contains("meat")) return Kind.Meat;
            if (id == "bones") return Kind.Bones;
            if (id.EndsWith("hide")) return Kind.Hide;
            if (id == "coins") return Kind.Coins;
            if (id.StartsWith("gem_")) return Kind.Gem;
            if (id.Contains("spark")) return Kind.Spark;
            if (id.StartsWith("seed")) return Kind.Seed;
            if (id.Contains("map")) return Kind.Map;
            if (id.Contains("crown")) return Kind.Crown;
            if (id.Contains("quill")) return Kind.Quill;
            if (id.Contains("fang")) return Kind.Fang;
            if (id.Contains("hook")) return Kind.Hook;
            if (id.StartsWith("pet_")) return Kind.Pet;
            return Kind.Sack;
        }

        static Texture2D Build(ItemInfo info, string id)
        {
            Color m = Hex(info != null ? info.ColorHex : "#999999");
            Color l = Color.Lerp(m, White, 0.45f);
            Color d = new Color(m.r * 0.62f, m.g * 0.62f, m.b * 0.62f, 1f);
            Canvas c = new Canvas();
            List<Vector2> sparkles = new List<Vector2>();
            Color sparkleColor = White;

            switch (KindOf(id))
            {
                case Kind.Sword:
                    c.Line(5, 19, 13, 27, 2.4f, Gold);            // crossguard
                    c.Line(9, 23, 26, 6, 3.4f, m);                // blade
                    c.Line(10, 21, 25.5f, 5.5f, 1.1f, l);         // edge shine
                    c.Line(8, 24, 4, 28, 2.4f, Wood);             // grip
                    c.Disc(3.5f, 28.5f, 2f, Gold);                // pommel
                    sparkles.Add(new Vector2(22, 9));
                    break;
                case Kind.Bow:
                    {
                        Vector2 top = Arc(22, 16, 13, 245f);
                        Vector2 bottom = Arc(22, 16, 13, 115f);
                        c.Line(top.x, top.y, bottom.x, bottom.y, 1f, White);
                        for (float a = 115f; a < 245f; a += 5f)
                        {
                            Vector2 p = Arc(22, 16, 13, a);
                            Vector2 q = Arc(22, 16, 13, a + 5f);
                            c.Line(p.x, p.y, q.x, q.y, 2.8f, m);
                        }
                        c.Rect(8, 14, 3, 5, d);
                        break;
                    }
                case Kind.Wand:
                    c.Line(6, 27, 21, 12, 2.6f, m);
                    c.Line(7, 25, 20, 12, 0.9f, l);
                    c.Disc(23.5f, 9.5f, 4.2f, Spark);
                    c.Disc(22.5f, 8.5f, 1.8f, Hex("#FFF1C4"));
                    sparkles.Add(new Vector2(28, 4)); sparkles.Add(new Vector2(29, 12)); sparkles.Add(new Vector2(19, 4));
                    sparkleColor = Hex("#FFE08A");
                    break;
                case Kind.Arrows:
                    for (int k = -1; k <= 1; k++)
                    {
                        float o = k * 3.6f;
                        c.Line(6 + o - 2, 24 + o + 2, 22 + o - 2, 8 + o + 2, 1.4f, m);
                        c.Poly(Steel, new Vector2(25.5f + o - 2, 4.5f + o + 2), new Vector2(23.8f + o - 2, 9.8f + o + 2), new Vector2(20.2f + o - 2, 6.2f + o + 2));
                        c.Line(6 + o - 2, 24 + o + 2, 9 + o - 2, 21 + o + 2, 2.8f, Blood);
                    }
                    break;
                case Kind.Plate:
                case Kind.Leather:
                    {
                        bool plate = KindOf(id) == Kind.Plate;
                        c.Poly(m, new Vector2(5, 7), new Vector2(11, 4), new Vector2(14, 6), new Vector2(18, 6), new Vector2(21, 4), new Vector2(27, 7),
                            new Vector2(29, 15), new Vector2(24, 16), new Vector2(24, 28), new Vector2(8, 28), new Vector2(8, 16), new Vector2(3, 15));
                        c.Ellipse(16, 5, 3.6f, 2.6f, Color.clear);
                        c.Poly(d, new Vector2(16, 7.5f), new Vector2(24, 16), new Vector2(24, 28), new Vector2(16, 28));
                        if (plate)
                        {
                            c.Ellipse(11.5f, 12, 3.2f, 3f, l);
                            c.Rect(8, 22, 16, 2, Gold);
                        }
                        else
                        {
                            c.Line(16, 8, 16, 20, 1f, Outline);
                            for (int y = 10; y <= 18; y += 3) c.Line(14.5f, y, 17.5f, y + 1.5f, 0.9f, Hex("#E8D3A8"));
                            c.Rect(8, 23, 16, 2, Wood);
                        }
                        break;
                    }
                case Kind.Logs:
                    Log(c, 6, 9, 18, m, l, d);
                    Log(c, 9, 17, 18, m, l, d);
                    Log(c, 3, 24, 18, m, l, d);
                    break;
                case Kind.Ore:
                    {
                        Color rock = Hex("#6E6A66");
                        c.Poly(rock, new Vector2(5, 20), new Vector2(8, 11), new Vector2(14, 6), new Vector2(22, 7), new Vector2(28, 14), new Vector2(27, 23), new Vector2(19, 28), new Vector2(9, 27));
                        c.Ellipse(13, 11, 4.5f, 2.5f, Hex("#8E8984"));
                        c.Disc(12, 19, 2.8f, m); c.Disc(20.5f, 13, 2.4f, m); c.Disc(20, 21.5f, 3f, m); c.Disc(25, 17, 1.8f, m);
                        c.Disc(11.4f, 18.3f, 0.9f, l); c.Disc(19.4f, 20.6f, 1f, l);
                        break;
                    }
                case Kind.Bar:
                    c.Poly(m, new Vector2(4, 15), new Vector2(14, 19), new Vector2(14, 25), new Vector2(4, 21));
                    c.Poly(d, new Vector2(14, 19), new Vector2(28, 14), new Vector2(28, 20), new Vector2(14, 25));
                    c.Poly(l, new Vector2(4, 15), new Vector2(18, 9), new Vector2(28, 14), new Vector2(14, 19));
                    sparkles.Add(new Vector2(11, 14));
                    break;
                case Kind.Meat:
                    c.Line(19, 19, 25.5f, 25.5f, 3.2f, Bone);
                    c.Disc(27.5f, 25, 2.4f, Bone); c.Disc(25, 27.5f, 2.4f, Bone);
                    c.Ellipse(13, 13, 9.5f, 8.5f, m);
                    c.Ellipse(15, 16, 6f, 4.5f, d);
                    c.Ellipse(10, 10, 3.5f, 2.2f, l);
                    c.Line(8, 15, 13, 13, 0.9f, Hex("#F2C4BE"));
                    break;
                case Kind.Bones:
                    c.Line(8, 24, 24, 8, 3.2f, m);
                    c.Disc(5.2f, 23.6f, 2.5f, m); c.Disc(8.4f, 26.8f, 2.5f, m);
                    c.Disc(23.6f, 5.2f, 2.5f, m); c.Disc(26.8f, 8.4f, 2.5f, m);
                    c.Line(9, 21, 21, 9, 0.9f, White);
                    break;
                case Kind.Hide:
                    c.Poly(m, new Vector2(8, 6), new Vector2(13, 8), new Vector2(19, 8), new Vector2(24, 6), new Vector2(25, 11), new Vector2(23, 14),
                        new Vector2(25, 22), new Vector2(22, 27), new Vector2(18, 24), new Vector2(14, 24), new Vector2(10, 27), new Vector2(7, 22), new Vector2(9, 14), new Vector2(7, 11));
                    c.Ellipse(16, 16, 5, 6, d);
                    c.Ellipse(14, 12, 2.5f, 2f, l);
                    break;
                case Kind.Coins:
                    Coin(c, 16, 11, m, l, d);
                    Coin(c, 10, 19, m, l, d);
                    Coin(c, 21, 21, m, l, d);
                    break;
                case Kind.Gem:
                    c.Poly(m, new Vector2(16, 4), new Vector2(26, 12), new Vector2(16, 28), new Vector2(6, 12));
                    c.Poly(l, new Vector2(16, 4), new Vector2(26, 12), new Vector2(6, 12));
                    c.Poly(d, new Vector2(6, 12), new Vector2(16, 12), new Vector2(16, 28));
                    sparkles.Add(new Vector2(12, 8));
                    break;
                case Kind.Spark:
                    c.Ellipse(16, 18, 10, 8.5f, Hex("#4E4038"));
                    c.Ellipse(13, 14, 4, 2.5f, Hex("#6A5A50"));
                    c.Line(9, 15, 15, 19, 1.3f, m); c.Line(15, 19, 22, 13, 1.3f, m); c.Line(15, 19, 17, 25, 1.3f, m);
                    c.Disc(15, 19, 2.2f, Hex("#FFE9A0"));
                    sparkles.Add(new Vector2(26, 6)); sparkles.Add(new Vector2(23, 3)); sparkles.Add(new Vector2(29, 10));
                    sparkleColor = Hex("#FFD24A");
                    break;
                case Kind.Seed:
                    c.Line(16, 11, 19, 4, 1.5f, Leaf);
                    c.Ellipse(22, 5, 3.4f, 1.8f, Leaf);
                    c.Ellipse(15, 19, 6.5f, 8.5f, m);
                    c.Ellipse(13, 15.5f, 2f, 3f, l);
                    sparkles.Add(new Vector2(24, 20));
                    break;
                case Kind.Map:
                    c.Rect(5, 7, 22, 18, m);
                    c.Rect(4, 5, 24, 3, d);
                    c.Rect(4, 24, 24, 3, d);
                    c.Line(8, 21, 12, 16, 1f, Hex("#A0302A")); c.Line(12, 16, 16, 18, 1f, Hex("#A0302A")); c.Line(16, 18, 20, 13, 1f, Hex("#A0302A"));
                    c.Line(20, 10, 24, 14, 1.3f, Hex("#A0302A")); c.Line(24, 10, 20, 14, 1.3f, Hex("#A0302A"));
                    break;
                case Kind.Crown:
                    c.Poly(m, new Vector2(5, 19), new Vector2(7, 7), new Vector2(11, 19));
                    c.Poly(m, new Vector2(12, 19), new Vector2(16, 4), new Vector2(20, 19));
                    c.Poly(m, new Vector2(21, 19), new Vector2(25, 7), new Vector2(27, 19));
                    c.Rect(5, 18, 22, 8, m);
                    c.Rect(5, 23, 22, 3, d);
                    c.Ellipse(7, 7, 2.2f, 1.5f, l); c.Ellipse(16, 4.5f, 2.2f, 1.5f, l); c.Ellipse(25, 7, 2.2f, 1.5f, l);
                    c.Disc(16, 21, 2.2f, Gold); c.Disc(9.5f, 21.5f, 1.4f, Hex("#3FA55A")); c.Disc(22.5f, 21.5f, 1.4f, Hex("#3FA55A"));
                    break;
                case Kind.Quill:
                    c.Poly(m, new Vector2(24, 6), new Vector2(22, 13), new Vector2(15, 20), new Vector2(8, 25), new Vector2(11, 18), new Vector2(17, 11));
                    c.Line(5, 28, 24, 6, 1f, d);
                    c.Disc(25, 5, 3.2f, Thistle);
                    c.Line(25, 5, 29, 1.5f, 1.1f, Thistle); c.Line(25, 5, 30, 6, 1.1f, Thistle); c.Line(25, 5, 23, 0.5f, 1.1f, Thistle);
                    break;
                case Kind.Fang:
                    c.Poly(m, new Vector2(9, 5), new Vector2(21, 5), new Vector2(20, 12), new Vector2(17, 22), new Vector2(14, 28), new Vector2(12, 20), new Vector2(10, 12));
                    c.Poly(d, new Vector2(17, 8), new Vector2(21, 5), new Vector2(20, 12), new Vector2(17, 22), new Vector2(14, 28), new Vector2(16, 18));
                    c.Rect(9, 5, 12, 3, Hex("#B8A890"));
                    c.Line(12, 9, 13.5f, 19, 1.1f, White);
                    break;
                case Kind.Hook:
                    for (float a = 0f; a < 180f; a += 10f)
                    {
                        Vector2 p = Arc(14, 20, 5.5f, a);
                        Vector2 q = Arc(14, 20, 5.5f, a + 10f);
                        c.Line(p.x, p.y, q.x, q.y, 2.8f, m);
                    }
                    c.Line(19.5f, 5, 19.5f, 20, 2.8f, m);
                    c.Line(8.5f, 20, 10.5f, 14.5f, 2.2f, m);
                    c.Disc(19.5f, 4, 3f, m);
                    c.Disc(19.5f, 4, 1.3f, Color.clear);
                    c.Line(18.7f, 8, 18.7f, 19, 0.8f, l);
                    break;
                case Kind.Pet:
                    c.Ellipse(16, 19, 11.5f, 9.5f, m);
                    c.Rect(0, 27, 32, 5, Color.clear);
                    c.Ellipse(10.5f, 13.5f, 3.2f, 2f, l);
                    c.Disc(12, 18, 2.6f, White); c.Disc(20, 18, 2.6f, White);
                    c.Disc(12.6f, 18.6f, 1.3f, Outline); c.Disc(20.6f, 18.6f, 1.3f, Outline);
                    c.Line(14, 23.5f, 18, 23.5f, 1f, Outline);
                    break;
                case Kind.Axe:
                    c.Line(7, 28, 22, 6, 2.6f, Wood);
                    c.Line(8, 26, 21, 7, 0.8f, Hex("#8A6A4A"));
                    c.Poly(m, new Vector2(17, 6), new Vector2(23, 3), new Vector2(29, 8), new Vector2(28, 16), new Vector2(23, 13), new Vector2(20, 11));
                    c.Poly(l, new Vector2(23, 3), new Vector2(29, 8), new Vector2(28, 16), new Vector2(27, 9));
                    sparkles.Add(new Vector2(27, 6));
                    break;
                case Kind.Pickaxe:
                    c.Line(8, 28, 20, 8, 2.6f, Wood);
                    for (float a = 200f; a < 340f; a += 10f)
                    {
                        Vector2 p = Arc(19, 15, 12, a);
                        Vector2 q = Arc(19, 15, 12, a + 10f);
                        c.Line(p.x, p.y, q.x, q.y, a > 250f && a < 290f ? 3.4f : 2.4f, m);
                    }
                    for (float a = 210f; a < 330f; a += 10f)
                    {
                        Vector2 p = Arc(19, 14, 12, a);
                        Vector2 q = Arc(19, 14, 12, a + 10f);
                        c.Line(p.x, p.y, q.x, q.y, 0.8f, l);
                    }
                    break;
                case Kind.Hammer:
                    c.Line(8, 28, 18, 12, 2.6f, Wood);
                    c.Poly(m, new Vector2(12, 4), new Vector2(26, 13), new Vector2(23, 18), new Vector2(9, 9));
                    c.Poly(l, new Vector2(12, 4), new Vector2(26, 13), new Vector2(25, 14.5f), new Vector2(11, 5.5f));
                    break;
                case Kind.Flint:
                    c.Ellipse(11, 20, 7, 6, Hex("#5A5550"));
                    c.Ellipse(9, 18, 3, 2, Hex("#8A8580"));
                    c.Rect(15, 9, 13, 5, m);
                    c.Rect(15, 9, 13, 1.5f, l);
                    c.Poly(m, new Vector2(15, 9), new Vector2(17, 14), new Vector2(14, 18), new Vector2(12, 14));
                    sparkles.Add(new Vector2(16, 20)); sparkles.Add(new Vector2(20, 18)); sparkles.Add(new Vector2(18, 23));
                    sparkleColor = Hex("#FFD24A");
                    break;
                case Kind.Bread:
                    c.Ellipse(16, 18, 12, 8, m);
                    c.Ellipse(16, 15, 11, 6, l);
                    c.Line(10, 12, 12, 16, 1f, d); c.Line(15, 11, 17, 15, 1f, d); c.Line(20, 11, 22, 15, 1f, d);
                    break;
                case Kind.Cheese:
                    c.Poly(m, new Vector2(4, 22), new Vector2(26, 10), new Vector2(28, 24), new Vector2(6, 26));
                    c.Poly(l, new Vector2(4, 22), new Vector2(26, 10), new Vector2(28, 13), new Vector2(7, 23));
                    c.Disc(14, 22, 1.6f, d); c.Disc(21, 20, 1.3f, d); c.Disc(24, 23, 1.0f, d);
                    break;
                case Kind.Pie:
                    c.Ellipse(16, 20, 12, 7, Hex("#8A5A2A"));
                    c.Ellipse(16, 17, 11, 6, m);
                    c.Ellipse(14, 15, 5, 2.2f, l);
                    c.Line(11, 17, 15, 17, 1f, d); c.Line(18, 18, 22, 17, 1f, d);
                    break;
                default:
                    c.Ellipse(16, 19, 9.5f, 9, m);
                    c.Poly(m, new Vector2(12, 11), new Vector2(16, 8), new Vector2(20, 11));
                    c.Rect(12, 9, 8, 2, d);
                    c.Ellipse(12, 16, 2.5f, 3, l);
                    break;
            }

            c.AddOutline(Outline);
            foreach (Vector2 p in sparkles) c.Sparkle((int)p.x, (int)p.y, sparkleColor);
            return c.ToTexture(Upscale);
        }

        static void Log(Canvas c, float x, float y, float length, Color m, Color l, Color d)
        {
            c.Rect(x, y - 3.5f, length, 7, m);
            c.Rect(x, y - 3, length, 1.2f, l);
            c.Rect(x, y + 2.5f, length, 1f, d);
            c.Ellipse(x + length, y, 2.8f, 3.6f, Hex("#D9B27A"));
            c.Ellipse(x + length, y, 1.4f, 1.9f, Hex("#B08050"));
        }

        static void Coin(Canvas c, float x, float y, Color m, Color l, Color d)
        {
            c.Disc(x, y, 6.2f, d);
            c.Disc(x - 0.4f, y - 0.4f, 5.2f, m);
            c.Disc(x - 1.6f, y - 1.6f, 2.2f, l);
            c.Rect(x - 0.5f, y - 2.5f, 1f, 5f, d);
        }

        static Vector2 Arc(float cx, float cy, float r, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
        }

        static Color Hex(string hex)
        {
            Color color;
            return ColorUtility.TryParseHtmlString(hex, out color) ? color : Color.gray;
        }

        /// <summary>32x32 RGBA canvas, y pointing down, shapes tested at pixel centres.</summary>
        class Canvas
        {
            readonly Color[] px = new Color[N * N];

            void Set(int x, int y, Color color)
            {
                if (x < 0 || y < 0 || x >= N || y >= N) return;
                px[y * N + x] = color;
            }

            public void Disc(float cx, float cy, float r, Color color)
            {
                Ellipse(cx, cy, r, r, color);
            }

            public void Ellipse(float cx, float cy, float rx, float ry, Color color)
            {
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = (x + 0.5f - cx) / rx;
                        float dy = (y + 0.5f - cy) / ry;
                        if (dx * dx + dy * dy <= 1f) Set(x, y, color);
                    }
            }

            public void Rect(float rx, float ry, float w, float h, Color color)
            {
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float fx = x + 0.5f, fy = y + 0.5f;
                        if (fx >= rx && fx <= rx + w && fy >= ry && fy <= ry + h) Set(x, y, color);
                    }
            }

            public void Line(float x0, float y0, float x1, float y1, float thickness, Color color)
            {
                Vector2 a = new Vector2(x0, y0), b = new Vector2(x1, y1);
                Vector2 ab = b - a;
                float lengthSq = Mathf.Max(0.0001f, ab.sqrMagnitude);
                float half = thickness * 0.5f;
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
                        if ((a + ab * t - p).magnitude <= half) Set(x, y, color);
                    }
            }

            public void Poly(Color color, params Vector2[] points)
            {
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        if (Inside(new Vector2(x + 0.5f, y + 0.5f), points)) Set(x, y, color);
                    }
            }

            static bool Inside(Vector2 p, Vector2[] poly)
            {
                bool inside = false;
                for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                {
                    if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                        p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    {
                        inside = !inside;
                    }
                }
                return inside;
            }

            public void AddOutline(Color color)
            {
                Color[] source = (Color[])px.Clone();
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        if (source[y * N + x].a > 0.5f) continue;
                        if (Solid(source, x - 1, y) || Solid(source, x + 1, y) || Solid(source, x, y - 1) || Solid(source, x, y + 1)) Set(x, y, color);
                    }
            }

            static bool Solid(Color[] source, int x, int y)
            {
                return x >= 0 && y >= 0 && x < N && y < N && source[y * N + x].a > 0.5f;
            }

            public void Sparkle(int x, int y, Color color)
            {
                Set(x, y, color);
                Color soft = new Color(color.r, color.g, color.b, 0.6f);
                if (px[Mathf.Clamp(y, 0, N - 1) * N + Mathf.Clamp(x - 1, 0, N - 1)].a < 0.5f) Set(x - 1, y, soft);
                if (px[Mathf.Clamp(y, 0, N - 1) * N + Mathf.Clamp(x + 1, 0, N - 1)].a < 0.5f) Set(x + 1, y, soft);
                if (px[Mathf.Clamp(y - 1, 0, N - 1) * N + Mathf.Clamp(x, 0, N - 1)].a < 0.5f) Set(x, y - 1, soft);
                if (px[Mathf.Clamp(y + 1, 0, N - 1) * N + Mathf.Clamp(x, 0, N - 1)].a < 0.5f) Set(x, y + 1, soft);
            }

            public Texture2D ToTexture(int upscale)
            {
                int size = N * upscale;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                Color[] output = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        // texture rows go bottom-up, canvas rows top-down
                        output[y * size + x] = px[(N - 1 - y / upscale) * N + x / upscale];
                    }
                texture.SetPixels(output);
                texture.Apply(false, false);
                return texture;
            }
        }
    }
}
