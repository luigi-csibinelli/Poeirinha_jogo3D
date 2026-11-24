using System.Collections.Generic;
using UnityEngine;

namespace Poeirinha
{
    /// <summary>
    /// Móvel que pode ficar semitransparente quando tapa o robô (sofá, cama, mesa...).
    ///
    /// No URP, transparência depende do material ("Surface Type = Transparent"). Então, no
    /// Awake, criamos uma cópia transparente de cada material (a partir de um material
    /// modelo) e trocamos os materiais só enquanto o móvel está "sumindo". Quando o alpha
    /// volta a 1, devolvemos os materiais opacos originais (que têm sombra e ordenação melhores).
    /// </summary>
    public class FadeableObject : MonoBehaviour
    {
        [SerializeField] Material transparentTemplate;
        [SerializeField, Range(0f, 1f)] float fadedAlpha = 0.3f;
        [SerializeField] float fadeSpeed = 5f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        Renderer[] renderers;
        Material[][] opaqueMaterials;
        Material[][] transparentMaterials;
        readonly List<Material> allTransparent = new List<Material>();
        readonly List<Color> baseColors = new List<Color>();

        bool occluding;
        bool usingTransparent;
        float alpha = 1f;

        public bool IsFaded => alpha < 0.99f;
        public float Alpha => alpha;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            opaqueMaterials = new Material[renderers.Length][];
            transparentMaterials = new Material[renderers.Length][];
            var cache = new Dictionary<Material, Material>();

            for (int r = 0; r < renderers.Length; r++)
            {
                Material[] shared = renderers[r].sharedMaterials;
                opaqueMaterials[r] = shared;
                transparentMaterials[r] = new Material[shared.Length];
                for (int m = 0; m < shared.Length; m++)
                {
                    Material src = shared[m];
                    if (src == null || transparentTemplate == null) { transparentMaterials[r][m] = src; continue; }
                    if (!cache.TryGetValue(src, out Material copy))
                    {
                        copy = new Material(transparentTemplate) { name = src.name + " (Fade)" };
                        Color c = src.HasProperty(BaseColorId) ? src.GetColor(BaseColorId) : Color.white;
                        copy.SetColor(BaseColorId, c);
                        if (src.HasProperty(BaseMapId))
                        {
                            copy.SetTexture(BaseMapId, src.GetTexture(BaseMapId));
                            copy.SetTextureScale(BaseMapId, src.GetTextureScale(BaseMapId));
                        }
                        cache[src] = copy;
                        allTransparent.Add(copy);
                        baseColors.Add(c);
                    }
                    transparentMaterials[r][m] = copy;
                }
            }
        }

        /// <summary>Chamado pelo OcclusionFader da câmera a cada frame.</summary>
        public void SetOccluding(bool value) => occluding = value;

        void Update()
        {
            float target = occluding ? fadedAlpha : 1f;
            if (Mathf.Approximately(alpha, target) && !(alpha >= 1f && usingTransparent)) return;

            alpha = Mathf.MoveTowards(alpha, target, fadeSpeed * Time.unscaledDeltaTime);

            if (alpha < 1f && !usingTransparent) Swap(true);
            for (int i = 0; i < allTransparent.Count; i++)
            {
                Color c = baseColors[i];
                c.a = alpha;
                allTransparent[i].SetColor(BaseColorId, c);
            }
            if (alpha >= 1f && usingTransparent) Swap(false);
        }

        void Swap(bool transparent)
        {
            usingTransparent = transparent;
            for (int r = 0; r < renderers.Length; r++)
                renderers[r].sharedMaterials = transparent ? transparentMaterials[r] : opaqueMaterials[r];
        }

        void OnDestroy()
        {
            foreach (Material m in allTransparent) Destroy(m);
        }
    }
}
