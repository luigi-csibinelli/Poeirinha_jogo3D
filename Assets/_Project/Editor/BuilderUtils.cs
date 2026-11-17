using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Poeirinha.EditorTools
{
    /// <summary>
    /// Funções auxiliares usadas pelo PoeirinhaBuilder: criar caixas, cilindros, materiais,
    /// texturas, elementos de UI e preencher campos [SerializeField] por código.
    /// </summary>
    public static class BuilderUtils
    {
        public const string Root = "Assets/_Project";
        public const string MatDir = Root + "/Materials";
        public const string TexDir = Root + "/Art/Textures";
        public const string GenDir = Root + "/Art/Generated";
        public const string PrefabDir = Root + "/Prefabs";
        public const string SceneDir = Root + "/Scenes";

        public static Shader LitShader => Shader.Find("Universal Render Pipeline/Lit");

        // ------------------------------------------------------------------ geometria

        /// <summary>Caixa definida pelos cantos mínimo e máximo (em metros, espaço do pai).</summary>
        public static GameObject Box(string name, Transform parent, Vector3 min, Vector3 max, Material mat, bool collider = true, int layer = -1)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (min + max) * 0.5f;
            go.transform.localScale = max - min;
            Setup(go, mat, collider, layer);
            return go;
        }

        /// <summary>Caixa definida pelo centro e tamanho.</summary>
        public static GameObject BoxC(string name, Transform parent, Vector3 center, Vector3 size, Material mat, bool collider = true, int layer = -1)
            => Box(name, parent, center - size * 0.5f, center + size * 0.5f, mat, collider, layer);

        /// <summary>Cilindro em pé, com a base em basePos. Collider opcional (caixa, para não virar cápsula).</summary>
        public static GameObject Cyl(string name, Transform parent, Vector3 basePos, float diameter, float height, Material mat, bool collider = false, int layer = -1)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = basePos + Vector3.up * height * 0.5f;
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter); // cilindro padrão tem 2 m
            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (collider)
            {
                // Cilindro primitivo vem com CapsuleCollider (pontas redondas). Para pés de móveis
                // uma caixa é mais previsível e não cria "rampa" para o robô.
                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(1f, 2f, 1f);
            }
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (layer >= 0) go.layer = layer;
            return go;
        }

        public static GameObject Sphere(string name, Transform parent, Vector3 center, Vector3 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        public static GameObject Capsule(string name, Transform parent, Vector3 center, Vector3 euler, float diameter, float length, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = new Vector3(diameter, length * 0.5f, diameter);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static void Setup(GameObject go, Material mat, bool collider, int layer)
        {
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (layer >= 0) go.layer = layer;
        }

        public static GameObject Empty(string name, Transform parent, Vector3 localPos = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        /// <summary>Plano horizontal (virado para cima) com UV em coordenadas de mundo: 1 unidade de UV = 1 m.</summary>
        public static Mesh FloorQuad(string assetName, Vector2 min, Vector2 max, float uvScale = 1f)
        {
            var mesh = new Mesh { name = assetName };
            mesh.vertices = new[]
            {
                new Vector3(min.x, 0, min.y), new Vector3(min.x, 0, max.y),
                new Vector3(max.x, 0, max.y), new Vector3(max.x, 0, min.y)
            };
            mesh.uv = new[]
            {
                new Vector2(min.x, min.y) * uvScale, new Vector2(min.x, max.y) * uvScale,
                new Vector2(max.x, max.y) * uvScale, new Vector2(max.x, min.y) * uvScale
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return SaveAsset(mesh, $"{GenDir}/{assetName}.asset");
        }

        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // ------------------------------------------------------------------ assets

        public static T SaveAsset<T>(T asset, string path) where T : Object
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Material URP/Lit opaco.</summary>
        public static Material Mat(string name, Color color, float smoothness = 0.25f, Texture2D tex = null, float metallic = 0f)
        {
            var m = new Material(LitShader) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.enableInstancing = true;
            return SaveAsset(m, $"{MatDir}/{name}.mat");
        }

        /// <summary>Material com brilho próprio (LED, abajur, tela).</summary>
        public static Material Emissive(string name, Color color, float intensity)
        {
            var m = new Material(LitShader) { name = name };
            m.SetColor("_BaseColor", color);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return SaveAsset(m, $"{MatDir}/{name}.mat");
        }

        /// <summary>
        /// Configura um material URP/Lit como transparente (Surface Type = Transparent, Blend = Alpha).
        /// É o que o Inspector faz quando você troca o "Surface Type".
        /// </summary>
        public static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>Salva uma textura gerada como PNG e devolve o asset importado.</summary>
        public static Texture2D SaveTexture(Texture2D tex, string name, bool sprite = false, bool clamp = false, bool alpha = false)
        {
            Directory.CreateDirectory(TexDir);
            string path = $"{TexDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (sprite) imp.spriteImportMode = SpriteImportMode.Single;
            imp.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            imp.alphaIsTransparency = alpha;
            imp.mipmapEnabled = !sprite;
            imp.anisoLevel = sprite ? 0 : 4;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ campos [SerializeField]

        static SerializedProperty Prop(SerializedObject so, string field)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) throw new Exception($"Campo '{field}' não existe em {so.targetObject.GetType().Name}");
            return p;
        }

        public static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            Prop(so, field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Prop(so, field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetMask(Object target, string field, int mask)
        {
            var so = new SerializedObject(target);
            Prop(so, field).intValue = mask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            Prop(so, field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            Prop(so, field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ UI

        public static TMP_FontAsset UIFont;
        public static Sprite RoundedSprite;

        public static RectTransform UIRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent, float inset = 0f)
        {
            RectTransform rt = UIRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image UIImage(RectTransform rt, Color color, Sprite sprite = null, bool sliced = true)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : RoundedSprite;
            img.type = sliced && img.sprite == RoundedSprite ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI UIText(RectTransform rt, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = UIFont;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 sizeDelta,
            TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
        {
            return UIText(UIRect(name, parent, anchorMin, anchorMax, pivot, pos, sizeDelta), text, size, color, align, style);
        }

        public static Button UIButton(string name, Transform parent, string label, Vector2 pos, Vector2 size,
            Color normal, Color highlight, Color textColor, UnityAction onClick, out TextMeshProUGUI labelText)
        {
            RectTransform rt = UIRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            Image img = UIImage(rt, Color.white);
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlight;
            colors.selectedColor = highlight; // teclado: o selecionado fica destacado
            colors.pressedColor = highlight * 0.85f;
            colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.4f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            labelText = UIText(Stretch("Label", rt), label, 34, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
            if (onClick != null) UnityEventTools.AddPersistentListener(btn.onClick, onClick);
            return btn;
        }

        /// <summary>Liga a navegação vertical por teclado entre botões (cima/baixo).</summary>
        public static void ChainVertical(params Button[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = buttons[(i - 1 + buttons.Length) % buttons.Length],
                    selectOnDown = buttons[(i + 1) % buttons.Length]
                };
                buttons[i].navigation = nav;
            }
        }

        public static void ChainHorizontal(params Button[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = buttons[(i - 1 + buttons.Length) % buttons.Length],
                    selectOnRight = buttons[(i + 1) % buttons.Length]
                };
                buttons[i].navigation = nav;
            }
        }
    }
}
