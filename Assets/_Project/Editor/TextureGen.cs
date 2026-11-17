using UnityEngine;

namespace Poeirinha.EditorTools
{
    /// <summary>Texturas geradas por código (madeira, azulejo, tapete, poeira, estrela...).</summary>
    public static class TextureGen
    {
        static Texture2D New(int size) => new Texture2D(size, size, TextureFormat.RGBA32, false);

        /// <summary>Tacos de madeira: 4 tábuas por metro, emendas desencontradas.</summary>
        public static Texture2D WoodPlanks(int size)
        {
            var t = New(size);
            var rnd = new System.Random(10);
            int planks = 4;
            float[] tone = new float[planks * 2];
            for (int i = 0; i < tone.Length; i++) tone[i] = 0.85f + (float)rnd.NextDouble() * 0.2f;
            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                int row = (int)(v * planks);
                float local = v * planks - row;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    // Tábuas de 1 m, com as emendas desencontradas entre fileiras.
                    float shifted = Mathf.Repeat(u + row * 0.37f, 1f);
                    int seg = (int)(Mathf.Repeat(u + row * 0.37f + 0.5f, 2f));
                    float k = tone[row * 2 + seg];
                    float grain = Mathf.PerlinNoise(u * 40f + row * 7f, v * 4f) * 0.18f
                                + Mathf.Sin((u * 30f + Mathf.PerlinNoise(u * 3f, v * 10f + row) * 6f)) * 0.03f;
                    Color c = new Color(0.8f, 0.63f, 0.46f) * (k - grain);
                    bool seam = local < 0.02f || shifted < 0.005f;
                    if (seam) c *= 0.72f;
                    c.a = 1f;
                    t.SetPixel(x, y, c);
                }
            }
            t.Apply();
            return t;
        }

        /// <summary>Azulejo 3×3 por metro com rejunte.</summary>
        public static Texture2D Tiles(int size)
        {
            var t = New(size);
            int n = 3;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size * n, v = (float)y / size * n;
                    float fu = u - Mathf.Floor(u), fv = v - Mathf.Floor(v);
                    bool grout = fu < 0.025f || fv < 0.025f || fu > 0.975f || fv > 0.975f;
                    bool alt = ((int)u + (int)v) % 2 == 0;
                    float noise = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.05f;
                    Color c = grout ? new Color(0.68f, 0.68f, 0.66f) : (alt ? new Color(0.93f, 0.94f, 0.95f) : new Color(0.84f, 0.9f, 0.9f));
                    c *= 1f - noise;
                    c.a = 1f;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        public static Texture2D Rug(int size)
        {
            var t = New(size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    float n = Mathf.PerlinNoise(x * 0.5f, y * 0.5f) * 0.12f;
                    float stripe = Mathf.Sin(u * Mathf.PI * 8f) > 0.85f ? 0.15f : 0f;
                    float c = 0.9f - n - stripe;
                    t.SetPixel(x, y, new Color(c, c, c, 1f));
                }
            t.Apply();
            return t;
        }

        /// <summary>Mancha de poeira: borrão irregular com alfa.</summary>
        public static Texture2D Dust(int size, int seed)
        {
            var t = New(size);
            float ox = seed * 13.1f, oy = seed * 7.7f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size - 0.5f, v = (float)y / size - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v) * 2f;
                    float n = Mathf.PerlinNoise(x * 0.06f + ox, y * 0.06f + oy);
                    float fine = Mathf.PerlinNoise(x * 0.35f + oy, y * 0.35f + ox);
                    float a = Mathf.Clamp01((1f - r * (0.8f + n * 0.6f)) * 2.2f) * (0.55f + fine * 0.45f);
                    float shade = 0.75f + fine * 0.3f;
                    t.SetPixel(x, y, new Color(shade, shade, shade, a));
                }
            t.Apply();
            return t;
        }

        /// <summary>Migalhas: vários pontinhos.</summary>
        public static Texture2D Crumbs(int size, int seed)
        {
            var t = New(size);
            var clear = new Color(1, 1, 1, 0);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) t.SetPixel(x, y, clear);
            var rnd = new System.Random(seed);
            for (int k = 0; k < 40; k++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float dist = (float)System.Math.Sqrt(rnd.NextDouble()) * size * 0.38f;
                float cx = size / 2f + Mathf.Cos(ang) * dist, cy = size / 2f + Mathf.Sin(ang) * dist;
                float rad = 1.5f + (float)rnd.NextDouble() * 3.5f;
                float shade = 0.6f + (float)rnd.NextDouble() * 0.5f;
                for (int y = (int)(cy - rad - 1); y <= cy + rad + 1; y++)
                    for (int x = (int)(cx - rad - 1); x <= cx + rad + 1; x++)
                    {
                        if (x < 0 || y < 0 || x >= size || y >= size) continue;
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        float a = Mathf.Clamp01(rad - d + 0.5f);
                        if (a <= 0f) continue;
                        Color old = t.GetPixel(x, y);
                        t.SetPixel(x, y, new Color(shade, shade * 0.95f, shade * 0.9f, Mathf.Max(old.a, a)));
                    }
            }
            t.Apply();
            return t;
        }

        public static Texture2D SoftCircle(int size)
        {
            var t = New(size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size - 0.5f, v = (y + 0.5f) / size - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v) * 2f;
                    float a = Mathf.Clamp01(1f - r);
                    t.SetPixel(x, y, new Color(1, 1, 1, a * a));
                }
            t.Apply();
            return t;
        }

        /// <summary>Estrela de 5 pontas branca (a cor vem da Image), com antisserrilhado por supersampling.</summary>
        public static Texture2D Star(int size)
        {
            var t = New(size);
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? 0.48f : 0.2f;
                pts[i] = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r - 0.03f);
            }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                        {
                            var p = new Vector2((x + (sx + 0.5f) / 4f) / size - 0.5f, (y + (sy + 0.5f) / 4f) / size - 0.5f);
                            if (Inside(p, pts)) inside++;
                        }
                    t.SetPixel(x, y, new Color(1, 1, 1, inside / 16f));
                }
            t.Apply();
            return t;
        }

        static bool Inside(Vector2 p, Vector2[] poly)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                    (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                    c = !c;
            return c;
        }
    }
}
