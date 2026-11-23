using UnityEngine;

namespace Poeirinha
{
    /// <summary>
    /// Gera todos os sons do jogo por código (síntese simples: senos, ruído e envelopes).
    /// Vantagem: zero arquivos de áudio de terceiros, nada de licença para checar.
    /// Para trocar por um som de verdade, basta arrastar um AudioClip no AudioManager.
    /// </summary>
    public static class ProceduralAudio
    {
        const int Rate = 22050; // suficiente para efeitos sonoros simples
        static System.Random rng = new System.Random(1234);

        static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

        static AudioClip Make(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Envelope ataque/decaimento simples (0..1).</summary>
        static float Env(float t, float dur, float attack, float release)
        {
            if (t < attack) return t / attack;
            if (t > dur - release) return Mathf.Max(0f, (dur - t) / release);
            return 1f;
        }

        static float Tri(float phase) => 1f - 4f * Mathf.Abs(Mathf.Repeat(phase + 0.25f, 1f) - 0.5f);

        /// <summary>Zumbido de motor em loop (1 s exato para emendar sem "clique").</summary>
        public static AudioClip MotorLoop()
        {
            int n = Rate;
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                // Frequências inteiras → o loop de 1 s fecha certinho.
                float hum = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.35f
                          + Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.18f
                          + Mathf.Sin(2f * Mathf.PI * 360f * t) * 0.08f;
                lp += (Noise() - lp) * 0.08f; // ruído filtrado = "ar sendo sugado"
                d[i] = (hum + lp * 0.9f) * 0.5f;
            }
            // Funde o fim com o começo para o ruído não estalar no loop.
            int fade = Rate / 20;
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                d[n - fade + i] = Mathf.Lerp(d[n - fade + i], d[i], k);
            }
            return Make("Motor", d);
        }

        /// <summary>"Fssup-pop" de sucção.</summary>
        public static AudioClip Suck()
        {
            float dur = 0.2f;
            int n = (int)(dur * Rate);
            var d = new float[n];
            float phase = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float f = Mathf.Lerp(250f, 1100f, t / dur);
                phase += f / Rate;
                lp += (Noise() - lp) * 0.3f;
                float env = Env(t, dur, 0.01f, 0.12f);
                d[i] = (Mathf.Sin(2f * Mathf.PI * phase) * 0.5f + lp * 0.6f) * env * 0.6f;
            }
            return Make("Suck", d);
        }

        public static AudioClip Beep(float freq = 1250f, float dur = 0.11f)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float s = Mathf.Sin(2f * Mathf.PI * freq * t);
                s = Mathf.Clamp(s * 2.5f, -1f, 1f); // quase quadrada: soa "eletrônico"
                d[i] = s * Env(t, dur, 0.005f, 0.03f) * 0.35f;
            }
            return Make("Beep", d);
        }

        /// <summary>Clique de plugue + dois tons subindo.</summary>
        public static AudioClip Plug()
        {
            float dur = 0.32f;
            int n = (int)(dur * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float click = t < 0.015f ? Noise() * (1f - t / 0.015f) : 0f;
                float f = t < 0.14f ? 660f : 990f;
                float local = t < 0.14f ? t - 0.02f : t - 0.14f;
                float tone = t > 0.02f ? Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-local * 14f) : 0f;
                d[i] = (click * 0.6f + tone * 0.45f) * Env(t, dur, 0.001f, 0.05f);
            }
            return Make("Plug", d);
        }

        /// <summary>"Miau": um tom que sobe e desce com vibrato e harmônicos.</summary>
        public static AudioClip Meow()
        {
            float dur = 0.55f;
            int n = (int)(dur * Rate);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float u = t / dur;
                // "mi" sobe, "au" desce.
                float f = u < 0.35f ? Mathf.Lerp(520f, 820f, u / 0.35f) : Mathf.Lerp(820f, 430f, (u - 0.35f) / 0.65f);
                f += Mathf.Sin(t * 2f * Mathf.PI * 7f) * 18f; // vibrato
                phase += f / Rate;
                float p = 2f * Mathf.PI * phase;
                // A mistura de harmônicos muda ao longo do som: imita a boca abrindo ("m" → "au").
                float open = Mathf.Sin(Mathf.PI * u);
                float s = Mathf.Sin(p) + Mathf.Sin(2f * p) * 0.5f * open + Mathf.Sin(3f * p) * 0.3f * open;
                d[i] = s * Env(t, dur, 0.05f, 0.2f) * 0.3f;
            }
            return Make("Meow", d);
        }

        /// <summary>Batidinha surda (encostou no gato).</summary>
        public static AudioClip Bonk()
        {
            float dur = 0.15f;
            int n = (int)(dur * Rate);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                phase += Mathf.Lerp(220f, 90f, t / dur) / Rate;
                d[i] = Mathf.Sin(2f * Mathf.PI * phase) * Mathf.Exp(-t * 25f) * 0.7f;
            }
            return Make("Bonk", d);
        }

        /// <summary>Puxão de pano (aperto de E na meia).</summary>
        public static AudioClip Rip()
        {
            float dur = 0.09f;
            int n = (int)(dur * Rate);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                lp += (Noise() - lp) * 0.5f;
                d[i] = lp * Env(t, dur, 0.005f, 0.06f) * 0.5f;
            }
            return Make("Rip", d);
        }

        /// <summary>Sequência de notas (jingles). Frequências em Hz, duração de cada nota em s.</summary>
        static AudioClip Melody(string name, float[] notes, float noteDur, float tail)
        {
            float dur = notes.Length * noteDur + tail;
            int n = (int)(dur * Rate);
            var d = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(k * noteDur * Rate);
                float len = (k == notes.Length - 1) ? noteDur + tail : noteDur * 1.6f;
                int count = Mathf.Min(n - start, (int)(len * Rate));
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / Rate;
                    float env = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * (k == notes.Length - 1 ? 2.5f : 6f));
                    float s = Tri(notes[k] * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * notes[k] * 2f * t) * 0.15f;
                    d[start + i] += s * env * 0.35f;
                }
            }
            return Make(name, d);
        }

        public static AudioClip VictoryJingle() =>
            Melody("Victory", new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 783.99f, 1046.5f }, 0.12f, 0.6f);

        public static AudioClip DefeatJingle() =>
            Melody("Defeat", new[] { 392f, 369.99f, 349.23f, 261.63f }, 0.22f, 0.7f);

        public static AudioClip Chime() =>
            Melody("Chime", new[] { 880f, 1318.5f }, 0.09f, 0.25f);

        /// <summary>Chaves balançando + porta rangendo: "o dono está chegando".</summary>
        public static AudioClip DoorKeys()
        {
            float dur = 1.4f;
            int n = (int)(dur * Rate);
            var d = new float[n];
            // Chaves: vários "tins" metálicos curtos.
            for (int k = 0; k < 9; k++)
            {
                int start = (int)((0.05f + k * 0.07f + (float)rng.NextDouble() * 0.03f) * Rate);
                float f = 2800f + (float)rng.NextDouble() * 2600f;
                for (int i = 0; i < Rate / 8 && start + i < n; i++)
                {
                    float t = (float)i / Rate;
                    d[start + i] += (Mathf.Sin(2f * Mathf.PI * f * t) + Mathf.Sin(2f * Mathf.PI * f * 1.48f * t) * 0.5f)
                                    * Mathf.Exp(-t * 40f) * 0.12f;
                }
            }
            // Porta: rangido grave com vibrato.
            int doorStart = (int)(0.75f * Rate);
            float phase = 0f;
            for (int i = doorStart; i < n; i++)
            {
                float t = (float)(i - doorStart) / Rate;
                float f = 140f + Mathf.Sin(t * 2f * Mathf.PI * 9f) * 25f + t * 60f;
                phase += f / Rate;
                float saw = Mathf.Repeat(phase, 1f) * 2f - 1f;
                d[i] += saw * Env(t, dur - 0.75f, 0.08f, 0.25f) * 0.12f;
            }
            return Make("DoorKeys", d);
        }

        /// <summary>
        /// Trilha leve em loop: C – Am – F – G, arpejo em colcheias + baixo.
        /// 8 compassos a 100 BPM ≈ 19 s.
        /// </summary>
        public static AudioClip MusicLoop()
        {
            const float bpm = 100f;
            float beat = 60f / bpm;
            float[][] chords =
            {
                new[] { 261.63f, 329.63f, 392.00f }, // C
                new[] { 220.00f, 261.63f, 329.63f }, // Am
                new[] { 174.61f, 220.00f, 261.63f }, // F
                new[] { 196.00f, 246.94f, 293.66f }, // G
            };
            int bars = 8;
            float dur = bars * 4 * beat;
            int n = (int)(dur * Rate);
            var d = new float[n];
            float eighth = beat / 2f;
            int[] pattern = { 0, 1, 2, 1, 0, 2, 1, 2 };

            for (int bar = 0; bar < bars; bar++)
            {
                float[] chord = chords[bar % 4];
                // Arpejo (uma oitava acima).
                for (int e = 0; e < 8; e++)
                {
                    float f = chord[pattern[e]] * 2f;
                    int start = (int)((bar * 4 * beat + e * eighth) * Rate);
                    int len = (int)(eighth * 1.8f * Rate);
                    for (int i = 0; i < len && start + i < n; i++)
                    {
                        float t = (float)i / Rate;
                        float env = Mathf.Min(1f, t / 0.008f) * Mathf.Exp(-t * 5f);
                        d[start + i] += (Tri(f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.1f) * env * 0.18f;
                    }
                }
                // Baixo: fundamental uma oitava abaixo, nos tempos 1 e 3.
                for (int b = 0; b < 2; b++)
                {
                    float f = chord[0] / 2f;
                    int start = (int)((bar * 4 * beat + b * 2 * beat) * Rate);
                    int len = (int)(2 * beat * Rate);
                    for (int i = 0; i < len && start + i < n; i++)
                    {
                        float t = (float)i / Rate;
                        float env = Mathf.Min(1f, t / 0.02f) * Mathf.Exp(-t * 1.5f);
                        d[start + i] += Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.22f;
                    }
                }
            }
            return Make("Music", d);
        }
    }
}
