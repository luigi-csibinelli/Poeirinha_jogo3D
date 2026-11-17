using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static Poeirinha.EditorTools.BuilderUtils;
using Object = UnityEngine.Object;

namespace Poeirinha.EditorTools
{
    /// <summary>
    /// Monta o jogo inteiro a partir de código: materiais, texturas, prefabs, as cenas
    /// MainMenu e Level01 (greybox caprichado com primitivas), NavMesh, UI e Build Settings.
    ///
    /// Menu: Tools > Poeirinha > ...
    /// ATENÇÃO: "Montar tudo" SOBRESCREVE as cenas e prefabs gerados. Ajustes finos feitos
    /// à mão nas cenas devem ser feitos depois (ou passados para este script).
    /// </summary>
    public static class PoeirinhaBuilder
    {
        // Layers (criados em "Configurar projeto")
        public const int LayerIgnoreRaycast = 2;
        public const int LayerUI = 5;
        public const int LayerPlayer = 6;
        public const int LayerWall = 7;
        public const int LayerOccluder = 8;
        public const int LayerCat = 9;

        public const string MenuScenePath = SceneDir + "/MainMenu.unity";
        public const string LevelScenePath = SceneDir + "/Level01.unity";

        const string TmpFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        // Paleta da UI
        static readonly Color PanelBg = new Color(0.09f, 0.11f, 0.17f, 0.86f);
        static readonly Color Mint = new Color(0.36f, 0.88f, 0.72f);
        static readonly Color Yellow = new Color(1f, 0.82f, 0.3f);
        static readonly Color ButtonNormal = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color ButtonHighlight = new Color(0.36f, 0.88f, 0.72f, 1f);

        // Geometria da fase (metros). O apartamento vai de (0,0) a (10,8) em X/Z.
        static readonly Rect StairHole = new Rect(7.8f, 0f, 2.2f, 1.6f);
        static readonly Vector3 DockPos = new Vector3(4.6f, 0f, 4.2f);
        static readonly Vector3 RobotStart = new Vector3(4.6f, 0.005f, 4.05f);
        const float RobotStartYaw = 90f;

        static Dictionary<string, Material> M = new Dictionary<string, Material>();
        static Material fadeTemplate;
        static Material particleMat;
        static Sprite starSprite;

        // =====================================================================
        // Menus
        // =====================================================================

        [MenuItem("Tools/Poeirinha/1. Configurar projeto (layers, TMP, NavMesh)", priority = 1)]
        public static void SetupProject()
        {
            ImportTmpEssentials();
            SetupLayers();
            SetupNavMeshAgent();
            SetupQualityAndPlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[Poeirinha] Projeto configurado.");
        }

        [MenuItem("Tools/Poeirinha/2. Montar tudo (materiais, prefabs, cenas)", priority = 2)]
        public static void BuildAll()
        {
            if (!Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Poeirinha", "Isto recria as cenas MainMenu e Level01 e os prefabs gerados. Continuar?", "Montar", "Cancelar"))
                return;

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            LoadCommonAssets();
            CreateTexturesAndMaterials();
            Dictionary<string, GameObject> prefabs = CreatePrefabs();
            BuildLevelScene(prefabs);
            BuildMenuScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(LevelScenePath, true)
            };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(MenuScenePath);
            Debug.Log("[Poeirinha] Tudo montado. Abra Level01 ou MainMenu e aperte Play.");
        }

        [MenuItem("Tools/Poeirinha/Espalhar sujeira na cena aberta", priority = 20)]
        public static void ScatterDirtMenu()
        {
            LoadCommonAssets();
            GameObject dust = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/DirtSpot_Dust.prefab");
            GameObject crumbs = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/DirtSpot_Crumbs.prefab");
            GameObject rootGo = GameObject.Find("Dirt");
            if (dust == null || crumbs == null || rootGo == null)
            {
                EditorUtility.DisplayDialog("Poeirinha", "Abra a cena Level01 (com o objeto 'Dirt') e rode 'Montar tudo' antes.", "OK");
                return;
            }
            ScatterDirt(rootGo.transform, dust, crumbs, Environment.TickCount);
            EditorSceneManager.MarkSceneDirty(rootGo.scene);
        }

        [MenuItem("Tools/Poeirinha/Build Windows", priority = 40)]
        public static void BuildWindows()
        {
            string outPath = Path.GetFullPath("Build/Windows/Poeirinha.exe");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MenuScenePath, LevelScenePath },
                locationPathName = outPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Poeirinha] Build: {report.summary.result} ({report.summary.totalSize / (1024 * 1024)} MB) em {outPath}");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new Exception("Build falhou: " + report.summary.result);
        }

        // Pontos de entrada para linha de comando (-executeMethod). Saem com código de erro.
        public static void CI_Setup() => RunCI(SetupProject);
        public static void CI_BuildAll() => RunCI(BuildAll);
        public static void CI_BuildWindows() => RunCI(BuildWindows);

        static void RunCI(Action action)
        {
            try
            {
                action();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        // =====================================================================
        // Configuração do projeto
        // =====================================================================

        static void ImportTmpEssentials()
        {
            if (File.Exists(TmpFontPath)) return;
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui/package.json");
            string path = Path.Combine(pkg.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            AssetDatabase.ImportPackage(path, false);
            AssetDatabase.Refresh();
        }

        static void SetupLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            void SetLayer(int index, string name) => layers.GetArrayElementAtIndex(index).stringValue = name;
            SetLayer(LayerPlayer, "Player");
            SetLayer(LayerWall, "Wall");
            SetLayer(LayerOccluder, "Occluder");
            SetLayer(LayerCat, "Cat");
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>O agente padrão do Unity tem 0.5 m de raio e 2 m de altura (um humano). O gato é bem menor.</summary>
        static void SetupNavMeshAgent()
        {
            Object[] objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
            if (objs.Length == 0) { Debug.LogWarning("NavMeshAreas.asset não encontrado"); return; }
            var so = new SerializedObject(objs[0]);
            SerializedProperty settings = so.FindProperty("m_Settings");
            if (settings == null || settings.arraySize == 0) { Debug.LogWarning("m_Settings não encontrado"); return; }
            SerializedProperty s0 = settings.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("agentRadius").floatValue = 0.18f;
            s0.FindPropertyRelative("agentHeight").floatValue = 0.3f;
            s0.FindPropertyRelative("agentClimb").floatValue = 0.08f;
            s0.FindPropertyRelative("agentSlope").floatValue = 40f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetupQualityAndPlayer()
        {
            PlayerSettings.productName = "Poeirinha";
            PlayerSettings.companyName = "Grupo Poeirinha";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;

            // Sombras: distância curta = sombras mais nítidas (a casa tem só 10 m).
            foreach (string path in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null) continue;
                asset.shadowDistance = 18f;
                asset.shadowCascadeCount = 2;
                asset.msaaSampleCount = 4;
                EditorUtility.SetDirty(asset);
            }
        }

        // =====================================================================
        // Texturas e materiais
        // =====================================================================

        static void LoadCommonAssets()
        {
            UIFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
            if (UIFont == null) throw new Exception("Fonte do TextMeshPro não encontrada. Rode 'Tools/Poeirinha/1. Configurar projeto' antes.");
            RoundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        static void CreateTexturesAndMaterials()
        {
            Directory.CreateDirectory(MatDir);
            Texture2D wood = SaveTexture(TextureGen.WoodPlanks(256), "T_WoodFloor");
            Texture2D tiles = SaveTexture(TextureGen.Tiles(256), "T_KitchenTiles");
            Texture2D rug = SaveTexture(TextureGen.Rug(256), "T_Rug");
            Texture2D dust = SaveTexture(TextureGen.Dust(128, 7), "T_Dust", clamp: true, alpha: true);
            Texture2D crumbs = SaveTexture(TextureGen.Crumbs(128, 11), "T_Crumbs", clamp: true, alpha: true);
            Texture2D soft = SaveTexture(TextureGen.SoftCircle(64), "T_SoftCircle", clamp: true, alpha: true);
            SaveTexture(TextureGen.Star(128), "T_Star", sprite: true, clamp: true, alpha: true);
            starSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexDir + "/T_Star.png");

            M.Clear();
            // Casa
            M["Wood"] = Mat("M_WoodFloor", new Color(1f, 0.97f, 0.93f), 0.35f, wood);
            M["WoodBedroom"] = Mat("M_WoodFloorBedroom", new Color(0.92f, 0.82f, 0.78f), 0.3f, wood);
            M["Tiles"] = Mat("M_KitchenTiles", Color.white, 0.55f, tiles);
            M["Wall"] = Mat("M_Wall", new Color(0.93f, 0.9f, 0.84f), 0.1f);
            M["WallDark"] = Mat("M_WallPit", new Color(0.55f, 0.52f, 0.5f), 0.1f);
            M["Trim"] = Mat("M_Trim", new Color(0.98f, 0.97f, 0.95f), 0.3f);
            M["Stair"] = Mat("M_StairWood", new Color(0.62f, 0.45f, 0.32f), 0.3f);
            M["Void"] = Mat("M_Void", new Color(0.08f, 0.08f, 0.1f), 0f);
            M["Window"] = Emissive("M_Window", new Color(0.72f, 0.86f, 1f), 1.4f);
            // Móveis
            M["Sofa"] = Mat("M_Sofa", new Color(0.24f, 0.52f, 0.56f), 0.15f);
            M["SofaCushion"] = Mat("M_SofaCushion", new Color(0.31f, 0.61f, 0.64f), 0.15f);
            M["Pillow"] = Mat("M_Pillow", new Color(0.97f, 0.74f, 0.3f), 0.1f);
            M["WoodDark"] = Mat("M_WoodDark", new Color(0.42f, 0.29f, 0.2f), 0.35f);
            M["WoodLight"] = Mat("M_WoodLight", new Color(0.8f, 0.63f, 0.45f), 0.35f);
            M["Metal"] = Mat("M_Metal", new Color(0.72f, 0.74f, 0.77f), 0.7f, null, 0.7f);
            M["Black"] = Mat("M_Black", new Color(0.06f, 0.06f, 0.07f), 0.6f);
            M["Sheet"] = Mat("M_Sheet", new Color(0.93f, 0.94f, 0.97f), 0.1f);
            M["Duvet"] = Mat("M_Duvet", new Color(0.93f, 0.52f, 0.47f), 0.1f);
            M["Rug"] = Mat("M_Rug", new Color(0.95f, 0.85f, 0.7f), 0.05f, rug);
            M["RugBlue"] = Mat("M_RugBlue", new Color(0.6f, 0.72f, 0.95f), 0.05f, rug);
            M["Counter"] = Mat("M_Counter", new Color(0.95f, 0.95f, 0.94f), 0.4f);
            M["CounterTop"] = Mat("M_CounterTop", new Color(0.28f, 0.29f, 0.32f), 0.6f);
            M["Fridge"] = Mat("M_Fridge", new Color(0.86f, 0.9f, 0.93f), 0.6f);
            M["Screen"] = Emissive("M_TVScreen", new Color(0.2f, 0.32f, 0.5f), 1.2f);
            M["Pot"] = Mat("M_Pot", new Color(0.8f, 0.45f, 0.3f), 0.2f);
            M["Leaves"] = Mat("M_Leaves", new Color(0.33f, 0.62f, 0.35f), 0.2f);
            M["BookA"] = Mat("M_BookA", new Color(0.85f, 0.35f, 0.3f), 0.2f);
            M["BookB"] = Mat("M_BookB", new Color(0.3f, 0.45f, 0.75f), 0.2f);
            M["BookC"] = Mat("M_BookC", new Color(0.95f, 0.8f, 0.35f), 0.2f);
            M["Shade"] = Emissive("M_LampShade", new Color(1f, 0.88f, 0.65f), 1.6f);
            // Robô e base
            M["RobotBody"] = Mat("M_RobotBody", new Color(0.17f, 0.18f, 0.21f), 0.65f);
            M["RobotTop"] = Mat("M_RobotTop", new Color(0.86f, 0.87f, 0.9f), 0.75f);
            M["Bumper"] = Mat("M_RobotBumper", new Color(0.07f, 0.07f, 0.08f), 0.3f);
            M["Accent"] = Emissive("M_RobotAccent", new Color(0.36f, 0.88f, 0.72f), 1.5f);
            M["LED"] = Emissive("M_RobotLED", new Color(0.25f, 1f, 0.45f), 3f);
            M["Brush"] = Mat("M_Brush", new Color(0.25f, 0.25f, 0.28f), 0.2f);
            M["Dock"] = Mat("M_Dock", new Color(0.92f, 0.93f, 0.95f), 0.6f);
            M["DockStatus"] = Emissive("M_DockStatus", new Color(0.25f, 0.6f, 1f), 2f);
            // Gato e meia
            M["Cat"] = Mat("M_Cat", new Color(0.95f, 0.58f, 0.24f), 0.15f);
            M["CatDark"] = Mat("M_CatStripes", new Color(0.72f, 0.36f, 0.13f), 0.15f);
            M["CatWhite"] = Mat("M_CatWhite", new Color(0.98f, 0.95f, 0.9f), 0.15f);
            M["CatPink"] = Mat("M_CatPink", new Color(0.98f, 0.62f, 0.68f), 0.2f);
            M["Sock"] = Mat("M_Sock", new Color(0.96f, 0.96f, 0.98f), 0.05f);
            M["SockStripe"] = Mat("M_SockStripe", new Color(0.9f, 0.3f, 0.3f), 0.05f);

            // Sujeira: transparente com textura
            Material dustMat = new Material(LitShader) { name = "M_Dust" };
            dustMat.SetTexture("_BaseMap", dust);
            dustMat.SetColor("_BaseColor", new Color(0.55f, 0.5f, 0.45f, 0.95f));
            dustMat.SetFloat("_Smoothness", 0f);
            MakeTransparent(dustMat);
            M["Dust"] = SaveAsset(dustMat, MatDir + "/M_Dust.mat");

            Material crumbsMat = new Material(LitShader) { name = "M_Crumbs" };
            crumbsMat.SetTexture("_BaseMap", crumbs);
            crumbsMat.SetColor("_BaseColor", new Color(0.62f, 0.45f, 0.3f, 1f));
            crumbsMat.SetFloat("_Smoothness", 0.1f);
            MakeTransparent(crumbsMat);
            M["Crumbs"] = SaveAsset(crumbsMat, MatDir + "/M_Crumbs.mat");

            // Modelo de material transparente usado pelo FadeableObject (oclusão).
            // Ter este material salvo também garante que a variante transparente do shader entre na build.
            fadeTemplate = new Material(LitShader) { name = "M_FadeTemplate" };
            fadeTemplate.SetFloat("_Smoothness", 0.2f);
            MakeTransparent(fadeTemplate);
            fadeTemplate = SaveAsset(fadeTemplate, MatDir + "/M_FadeTemplate.mat");

            // Partículas
            var urpParticles = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
            particleMat = urpParticles != null ? new Material(urpParticles) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            particleMat.name = "M_Particle";
            particleMat.SetTexture("_BaseMap", soft);
            particleMat.SetColor("_BaseColor", Color.white);
            MakeTransparent(particleMat);
            particleMat = SaveAsset(particleMat, MatDir + "/M_Particle.mat");
            AssetDatabase.SaveAssets();
        }

        // =====================================================================
        // Prefabs
        // =====================================================================

        static Dictionary<string, GameObject> CreatePrefabs()
        {
            Directory.CreateDirectory(PrefabDir);
            var result = new Dictionary<string, GameObject>();
            var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            result["Robot"] = SavePrefab(BuildRobot(), "Robot");
            result["Cat"] = SavePrefab(BuildCat(), "Cat");
            result["Dock"] = SavePrefab(BuildDock(), "ChargingDock");
            result["Sock"] = SavePrefab(BuildSock(), "Sock");
            result["Dust"] = SavePrefab(BuildDirt("DirtSpot_Dust", M["Dust"]), "DirtSpot_Dust");
            result["Crumbs"] = SavePrefab(BuildDirt("DirtSpot_Crumbs", M["Crumbs"]), "DirtSpot_Crumbs");
            return result;
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>Visual do robô feito de primitivas (sem colliders). Usado no jogo e no menu.</summary>
        static Transform BuildRobotModel(Transform root, out Renderer led, out Light ledLight, out Transform[] brushes)
        {
            Transform body = Empty("Body", root).transform;
            Cyl("Shell", body, new Vector3(0, 0.004f, 0), 0.35f, 0.082f, M["RobotBody"]);
            Cyl("Bumper", body, new Vector3(0, 0.012f, 0), 0.356f, 0.04f, M["Bumper"]);
            Cyl("TopCover", body, new Vector3(0, 0.086f, 0), 0.31f, 0.005f, M["RobotTop"]);
            Cyl("AccentRing", body, new Vector3(0, 0.0862f, 0), 0.245f, 0.0052f, M["Accent"]);
            Cyl("TopCenter", body, new Vector3(0, 0.0865f, 0), 0.235f, 0.0055f, M["RobotTop"]);
            Cyl("Lidar", body, new Vector3(0, 0.091f, -0.06f), 0.075f, 0.018f, M["RobotBody"]);
            Cyl("LidarTop", body, new Vector3(0, 0.109f, -0.06f), 0.07f, 0.004f, M["Bumper"]);
            GameObject ledGo = Cyl("LED", body, new Vector3(0, 0.091f, 0.075f), 0.032f, 0.006f, M["LED"]);
            led = ledGo.GetComponent<Renderer>();
            // "Olhinhos" na frente: deixam claro para onde o robô está virado.
            BoxC("EyeL", body, new Vector3(-0.045f, 0.058f, 0.176f), new Vector3(0.034f, 0.012f, 0.008f), M["Accent"], false);
            BoxC("EyeR", body, new Vector3(0.045f, 0.058f, 0.176f), new Vector3(0.034f, 0.012f, 0.008f), M["Accent"], false);

            var lightGo = Empty("LEDLight", body, new Vector3(0, 0.13f, 0.075f));
            ledLight = lightGo.AddComponent<Light>();
            ledLight.type = LightType.Point;
            ledLight.range = 0.3f;
            ledLight.intensity = 0.08f;
            ledLight.shadows = LightShadows.None;

            brushes = new Transform[2];
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -0.115f : 0.115f;
                Transform pivot = Empty(side == 0 ? "BrushL" : "BrushR", body, new Vector3(x, 0.005f, 0.115f)).transform;
                Cyl("Hub", pivot, Vector3.zero, 0.02f, 0.006f, M["Brush"]);
                for (int k = 0; k < 3; k++)
                {
                    Transform arm = Empty("Arm" + k, pivot).transform;
                    arm.localEulerAngles = new Vector3(0, k * 120f, 0);
                    BoxC("Bristle", arm, new Vector3(0.04f, 0.002f, 0), new Vector3(0.08f, 0.003f, 0.007f), M["Brush"], false);
                }
                brushes[side] = pivot;
            }
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            return body;
        }

        static GameObject BuildRobot()
        {
            var root = new GameObject("Robot");
            root.tag = "Player";

            Transform body = BuildRobotModel(root.transform, out Renderer led, out Light ledLight, out Transform[] brushes);

            // Collider: cilindro convexo (0.35 × 0.09 m). Ver GDD seção 19: esfera/cápsula não
            // cabem embaixo do sofá com 0.09 m de altura, e um disco redondo também desliza nas quinas.
            GameObject hull = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hull.name = "Hull";
            hull.transform.SetParent(root.transform, false);
            hull.transform.localPosition = new Vector3(0, 0.045f, 0);
            hull.transform.localScale = new Vector3(0.35f, 0.045f, 0.35f);
            Mesh cylMesh = hull.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(hull.GetComponent<Collider>());
            Object.DestroyImmediate(hull.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(hull.GetComponent<MeshFilter>());
            var mc = hull.AddComponent<MeshCollider>();
            mc.sharedMesh = cylMesh;
            mc.convex = true;

            var physMat = new PhysicsMaterial("PM_RobotNoFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            mc.sharedMaterial = SaveAsset(physMat, MatDir + "/PM_RobotNoFriction.physicMaterial");

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;

            var controller = root.AddComponent<RobotController>();
            var battery = root.AddComponent<BatterySystem>();
            var cliff = root.AddComponent<CliffSensor>();
            var visuals = root.AddComponent<RobotVisuals>();

            Set(battery, "robot", controller);
            int groundMask = ~((1 << LayerPlayer) | (1 << LayerCat) | (1 << LayerIgnoreRaycast) | (1 << LayerUI));
            SetMask(cliff, "groundMask", groundMask);
            Set(visuals, "robot", controller);
            Set(visuals, "battery", battery);
            Set(visuals, "ledRenderer", led);
            Set(visuals, "ledLight", ledLight);
            Set(visuals, "bodyRoot", body);
            SetArray(visuals, "brushes", brushes);

            SetLayerRecursive(root, LayerPlayer);
            return root;
        }

        static Transform BuildCatModel(Transform root, out Transform tail, out GameObject zzz)
        {
            Transform model = Empty("Model", root).transform;
            Capsule("Body", model, new Vector3(0, 0.17f, 0), new Vector3(90, 0, 0), 0.2f, 0.46f, M["Cat"]);
            Capsule("Belly", model, new Vector3(0, 0.135f, 0.02f), new Vector3(90, 0, 0), 0.15f, 0.34f, M["CatWhite"]);
            for (int i = 0; i < 3; i++)
                BoxC("Stripe" + i, model, new Vector3(0, 0.262f, -0.13f + i * 0.08f), new Vector3(0.1f, 0.008f, 0.03f), M["CatDark"], false);

            Sphere("Head", model, new Vector3(0, 0.27f, 0.25f), new Vector3(0.18f, 0.16f, 0.16f), M["Cat"]);
            Sphere("Muzzle", model, new Vector3(0, 0.245f, 0.315f), new Vector3(0.085f, 0.06f, 0.05f), M["CatWhite"]);
            Sphere("Nose", model, new Vector3(0, 0.265f, 0.338f), new Vector3(0.022f, 0.016f, 0.016f), M["CatPink"]);
            Sphere("EyeL", model, new Vector3(-0.042f, 0.292f, 0.318f), new Vector3(0.026f, 0.03f, 0.02f), M["Black"]);
            Sphere("EyeR", model, new Vector3(0.042f, 0.292f, 0.318f), new Vector3(0.026f, 0.03f, 0.02f), M["Black"]);
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject ear = BoxC(s < 0 ? "EarL" : "EarR", model, new Vector3(0.05f * s, 0.345f, 0.24f), new Vector3(0.055f, 0.055f, 0.018f), M["Cat"], false);
                ear.transform.localEulerAngles = new Vector3(0, 0, 45);
                GameObject inner = BoxC("Inner", model, new Vector3(0.05f * s, 0.342f, 0.25f), new Vector3(0.032f, 0.032f, 0.004f), M["CatPink"], false);
                inner.transform.localEulerAngles = new Vector3(0, 0, 45);
            }
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Cyl("Leg", model, new Vector3(0.055f * sx, 0f, 0.14f * sz), 0.05f, 0.13f, M["Cat"]);
                    Sphere("Paw", model, new Vector3(0.055f * sx, 0.012f, 0.14f * sz + 0.01f), new Vector3(0.055f, 0.025f, 0.065f), M["CatWhite"]);
                }

            tail = Empty("TailPivot", model, new Vector3(0, 0.2f, -0.22f)).transform;
            Capsule("Tail", tail, new Vector3(0, 0.1f, -0.05f), new Vector3(-25, 0, 0), 0.04f, 0.28f, M["Cat"]);
            Sphere("TailTip", tail, new Vector3(0, 0.225f, -0.105f), new Vector3(0.042f, 0.042f, 0.042f), M["CatDark"]);

            // "zZ" que aparece quando o gato dorme.
            zzz = new GameObject("Zzz");
            zzz.transform.SetParent(root, false);
            zzz.transform.localPosition = new Vector3(0.05f, 0.48f, 0.15f);
            var tmp = zzz.AddComponent<TextMeshPro>();
            tmp.font = UIFont;
            tmp.text = "z<size=140%>Z</size>";
            tmp.fontSize = 1.4f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.85f, 0.92f, 1f);
            tmp.fontStyle = FontStyles.Bold;
            tmp.rectTransform.sizeDelta = new Vector2(0.4f, 0.2f);
            zzz.AddComponent<Billboard>();
            zzz.SetActive(false);
            return model;
        }

        static GameObject BuildCat()
        {
            var root = new GameObject("Cat");
            Transform model = BuildCatModel(root.transform, out Transform tail, out GameObject zzz);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.18f;
            agent.height = 0.3f;
            agent.speed = 1.2f;
            agent.angularSpeed = 300f;
            agent.acceleration = 4f;
            agent.stoppingDistance = 0.1f;
            agent.baseOffset = 0f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            // Rigidbody cinemático: o NavMesh move o gato, mas a física ainda detecta os triggers.
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Collider sólido simples (o robô não atravessa o gato)
            var solid = root.AddComponent<CapsuleCollider>();
            solid.direction = 2; // eixo Z
            solid.center = new Vector3(0, 0.17f, 0.04f);
            solid.radius = 0.1f;
            solid.height = 0.58f;

            // Trigger um pouco maior para detectar o encostão antes do choque sólido
            var hit = Empty("HitTrigger", root.transform);
            var trig = hit.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.center = new Vector3(0, 0.15f, 0.04f);
            trig.size = new Vector3(0.36f, 0.3f, 0.72f);

            var ai = root.AddComponent<CatAI>();
            Set(ai, "model", model);
            Set(ai, "tail", tail);
            Set(ai, "sleepIndicator", zzz);

            SetLayerRecursive(root, LayerCat);
            return root;
        }

        static GameObject BuildDock()
        {
            var root = new GameObject("ChargingDock");
            // Torre (encostada na parede, lado +Z)
            Box("Tower", root.transform, new Vector3(-0.2f, 0, 0.11f), new Vector3(0.2f, 0.11f, 0.23f), M["Dock"], true);
            Box("TowerTop", root.transform, new Vector3(-0.19f, 0.11f, 0.12f), new Vector3(0.19f, 0.118f, 0.22f), M["RobotBody"], false);
            GameObject status = BoxC("StatusLight", root.transform, new Vector3(0, 0.075f, 0.105f), new Vector3(0.22f, 0.022f, 0.012f), M["DockStatus"], false);
            // Placa no chão: SÓ visual (um degrau de verdade travaria o robô)
            Box("Pad", root.transform, new Vector3(-0.25f, 0, -0.32f), new Vector3(0.25f, 0.003f, 0.11f), M["Dock"], false);
            BoxC("ContactL", root.transform, new Vector3(-0.08f, 0.0035f, -0.02f), new Vector3(0.03f, 0.002f, 0.22f), M["Metal"], false);
            BoxC("ContactR", root.transform, new Vector3(0.08f, 0.0035f, -0.02f), new Vector3(0.03f, 0.002f, 0.22f), M["Metal"], false);

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0, 0.1f, -0.08f);
            trigger.size = new Vector3(0.46f, 0.2f, 0.42f);

            var lightGo = Empty("ChargeLight", root.transform, new Vector3(0, 0.3f, -0.05f));
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.35f, 0.6f, 1f);
            light.range = 1.4f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;

            ParticleSystem ps = CreateParticles("ChargeParticles", root.transform, new Vector3(0, 0.01f, -0.08f), true);
            var main = ps.main;
            main.startColor = new Color(0.45f, 0.75f, 1f, 0.9f);
            main.startLifetime = 0.9f;
            main.startSpeed = 0.18f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.025f);
            main.gravityModifier = -0.02f;
            var emission = ps.emission;
            emission.rateOverTime = 22f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.16f;
            shape.rotation = new Vector3(-90, 0, 0); // emite para cima

            var dock = root.AddComponent<ChargingDock>();
            Set(dock, "chargeParticles", ps);
            Set(dock, "dockLight", light);
            Set(dock, "statusRenderer", status.GetComponent<Renderer>());
            root.layer = LayerIgnoreRaycast; // só o trigger fica neste layer
            return root;
        }

        static GameObject BuildSock()
        {
            var root = new GameObject("Sock");
            Transform vis = Empty("Visual", root.transform).transform;
            GameObject leg = Capsule("Leg", vis, new Vector3(0, 0.012f, -0.04f), new Vector3(90, 0, 0), 1f, 1f, M["Sock"]);
            leg.transform.localScale = new Vector3(0.085f, 0.11f, 0.024f);
            GameObject foot = Capsule("Foot", vis, new Vector3(0.045f, 0.011f, 0.085f), new Vector3(90, 55, 0), 1f, 1f, M["Sock"]);
            foot.transform.localScale = new Vector3(0.08f, 0.065f, 0.022f);
            BoxC("Stripe1", vis, new Vector3(0, 0.022f, -0.13f), new Vector3(0.075f, 0.006f, 0.014f), M["SockStripe"], false);
            BoxC("Stripe2", vis, new Vector3(0, 0.022f, -0.105f), new Vector3(0.078f, 0.006f, 0.012f), M["SockStripe"], false);
            vis.localEulerAngles = new Vector3(0, 25, 0);

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0, 0.05f, 0);
            trigger.size = new Vector3(0.24f, 0.1f, 0.26f);

            var sock = root.AddComponent<SockTrap>();
            Set(sock, "visual", vis);
            SetLayerRecursive(root, LayerIgnoreRaycast);
            return root;
        }

        static GameObject BuildDirt(string name, Material mat)
        {
            var root = new GameObject(name);
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Decal";
            quad.transform.SetParent(root.transform, false);
            quad.transform.localPosition = new Vector3(0, 0.003f, 0);
            quad.transform.localEulerAngles = new Vector3(90, 0, 0); // deitado, virado para cima
            quad.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0, 0.05f, 0);
            trigger.size = new Vector3(0.24f, 0.1f, 0.24f);
            root.AddComponent<DirtSpot>();
            SetLayerRecursive(root, LayerIgnoreRaycast);
            return root;
        }

        static ParticleSystem CreateParticles(string name, Transform parent, Vector3 pos, bool loop)
        {
            var go = Empty(name, parent, pos);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = loop;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        // =====================================================================
        // Fase Level01
        // =====================================================================

        static void BuildLevelScene(Dictionary<string, GameObject> prefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupLighting();
            Transform house = Empty("House", null).transform;
            BuildFloorAndWalls(house);
            BuildStairs(house);
            Transform furniture = Empty("Furniture", house).transform;
            BuildLivingRoom(furniture);
            BuildBedroom(furniture);
            BuildKitchen(furniture);

            // Base de recarga
            var dock = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["Dock"]);
            dock.transform.position = DockPos;

            // Robô
            var robot = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["Robot"]);
            robot.transform.SetPositionAndRotation(RobotStart, Quaternion.Euler(0, RobotStartYaw, 0));
            var controller = robot.GetComponent<RobotController>();
            var battery = robot.GetComponent<BatterySystem>();
            var cliff = robot.GetComponent<CliffSensor>();

            // Meias (extra)
            Transform hazards = Empty("Socks", null).transform;
            foreach (Vector3 p in SockPositions)
            {
                var sock = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["Sock"], hazards);
                sock.transform.position = p;
                sock.transform.rotation = Quaternion.Euler(0, p.x * 37f % 360f, 0);
            }

            // Sistemas
            var systems = new GameObject("Systems");
            var gm = systems.AddComponent<GameManager>();
            var cleaning = systems.AddComponent<CleaningManager>();
            var timer = systems.AddComponent<LevelTimer>();
            Set(gm, "battery", battery);
            Set(gm, "cleaning", cleaning);
            Set(gm, "timer", timer);

            ParticleSystem dust = CreateParticles("DustBurst", systems.transform, Vector3.zero, false);
            var dm = dust.main;
            dm.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            dm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            dm.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            dm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.66f, 0.6f, 0.8f), new Color(0.9f, 0.88f, 0.85f, 0.6f));
            dm.gravityModifier = 0.25f;
            var de = dust.emission;
            de.rateOverTime = 0f;
            var ds = dust.shape;
            ds.shapeType = ParticleSystemShapeType.Hemisphere;
            ds.radius = 0.08f;
            ds.rotation = new Vector3(-90, 0, 0);
            var col = dust.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = grad;
            Set(cleaning, "dustBurst", dust);

            // Sujeira
            Transform dirtRoot = Empty("Dirt", null).transform;
            Physics.SyncTransforms();
            ScatterDirt(dirtRoot, prefabs["Dust"], prefabs["Crumbs"], 42);

            // Gato + waypoints
            Transform wpRoot = Empty("CatWaypoints", null).transform;
            var waypoints = new List<Transform>();
            for (int i = 0; i < CatWaypoints.Length; i++)
                waypoints.Add(Empty("WP" + i, wpRoot, CatWaypoints[i]).transform);
            var cat = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["Cat"]);
            cat.transform.SetPositionAndRotation(CatWaypoints[3], Quaternion.Euler(0, 200, 0));
            var catAI = cat.GetComponent<CatAI>();
            SetArray(catAI, "waypoints", waypoints.ToArray());
            Set(catAI, "robot", robot.transform);

            // NavMesh (o gato anda nela)
            BakeNavMesh();

            // Câmera
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.11f, 0.15f);
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var follow = camGo.AddComponent<CameraFollow>();
            Set(follow, "target", robot.transform);
            SetMask(follow, "collisionMask", 1 << LayerWall);
            var fader = camGo.AddComponent<OcclusionFader>();
            Set(fader, "target", robot.transform);
            SetMask(fader, "occluderMask", 1 << LayerOccluder);
            camGo.transform.SetPositionAndRotation(RobotStart + Quaternion.Euler(0, RobotStartYaw, 0) * new Vector3(0, 1f, -1.3f),
                Quaternion.Euler(30, RobotStartYaw, 0));

            CreatePostVolume();

            // Áudio
            var audioGo = new GameObject("AudioManager");
            var audio = audioGo.AddComponent<AudioManager>();
            Set(audio, "robot", controller);
            Set(audio, "battery", battery);
            Set(audio, "cliffSensor", cliff);
            Set(audio, "cleaning", cleaning);
            Set(audio, "cat", catAI);
            Set(audio, "timer", timer);

            // UI
            BuildGameUI(battery, cleaning, timer, cliff, controller);

            EditorSceneManager.SaveScene(scene, LevelScenePath);
        }

        static readonly Vector3[] SockPositions =
        {
            new Vector3(0.95f, 0f, 0.75f),  // sala, canto sudoeste
            new Vector3(1.1f, 0f, 6.25f),   // quarto, perto do guarda-roupa
            new Vector3(9.35f, 0f, 5.75f),  // cozinha, perto da geladeira
        };

        static readonly Vector3[] CatWaypoints =
        {
            new Vector3(6.4f, 0f, 2.7f),
            new Vector3(1.1f, 0f, 2.8f),
            new Vector3(8.7f, 0f, 3.4f),
            new Vector3(4.3f, 0f, 5.3f),
            new Vector3(1.4f, 0f, 5.1f),
            new Vector3(6.2f, 0f, 5.2f),
            new Vector3(9.0f, 0f, 5.0f),
        };

        static void SetupLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.62f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.45f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.23f, 0.21f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.shadowBias = 0.02f;
            sun.shadowNormalBias = 0.2f;
            sunGo.transform.rotation = Quaternion.Euler(62f, -35f, 0f);
            RenderSettings.sun = sun;
        }

        static Light PointLight(string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        static void CreatePostVolume()
        {
            string path = SceneDir + "/PostProcessing.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
                var tone = profile.Add<Tonemapping>(true);
                tone.mode.Override(TonemappingMode.Neutral);
                var bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(1.05f);
                bloom.intensity.Override(0.6f);
                bloom.scatter.Override(0.6f);
                var vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.22f);
                vignette.smoothness.Override(0.5f);
                var color = profile.Add<ColorAdjustments>(true);
                color.postExposure.Override(0.15f);
                color.saturation.Override(10f);
                color.contrast.Override(6f);
                foreach (VolumeComponent c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
            var go = new GameObject("Global Volume");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
        }

        // ---------------------------------------------------------- chão e paredes

        static void BuildFloorAndWalls(Transform house)
        {
            Transform floors = Empty("Floors", house).transform;
            // Visual: planos com UV em metros (a textura não estica)
            MeshObject("Floor_Sala_A", floors, FloorQuad("Floor_Sala_A", new Vector2(0, 0), new Vector2(StairHole.xMin, StairHole.yMax)), M["Wood"], Vector3.zero);
            MeshObject("Floor_Sala_B", floors, FloorQuad("Floor_Sala_B", new Vector2(0, StairHole.yMax), new Vector2(10, 4.5f)), M["Wood"], Vector3.zero);
            MeshObject("Floor_Quarto", floors, FloorQuad("Floor_Quarto", new Vector2(0, 4.5f), new Vector2(5, 8)), M["WoodBedroom"], Vector3.zero);
            MeshObject("Floor_Cozinha", floors, FloorQuad("Floor_Cozinha", new Vector2(5, 4.5f), new Vector2(10, 8)), M["Tiles"], Vector3.zero);

            // Física: UM único MeshCollider em "L" (sem emendas entre caixas, que poderiam
            // criar "colisões fantasmas" e travar o robô). O buraco da escada fica de fora.
            var mesh = new Mesh { name = "FloorCollider" };
            mesh.vertices = new[]
            {
                new Vector3(0, 0, 0), new Vector3(0, 0, StairHole.yMax), new Vector3(StairHole.xMin, 0, StairHole.yMax), new Vector3(StairHole.xMin, 0, 0),
                new Vector3(0, 0, StairHole.yMax), new Vector3(0, 0, 8), new Vector3(10, 0, 8), new Vector3(10, 0, StairHole.yMax),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveAsset(mesh, GenDir + "/FloorCollider.asset");
            var floorCol = Empty("FloorCollider", floors);
            floorCol.AddComponent<MeshCollider>().sharedMesh = mesh;

            // Paredes: caixas simples, sobrepostas nos cantos (sem frestas). Layer "Wall" (a câmera colide).
            Transform walls = Empty("Walls", house).transform;
            const float T = 0.15f, H = 2.6f;
            Material w = M["Wall"];
            // Externas (descem até -2 m por causa do poço da escada)
            Box("Wall_S", walls, new Vector3(-T, -2f, -T), new Vector3(10 + T, H, 0), w, true, LayerWall);
            Box("Wall_N", walls, new Vector3(-T, -2f, 8), new Vector3(10 + T, H, 8 + T), w, true, LayerWall);
            Box("Wall_W", walls, new Vector3(-T, -2f, 0), new Vector3(0, H, 8), w, true, LayerWall);
            Box("Wall_E", walls, new Vector3(10, -2f, 0), new Vector3(10 + T, H, 8), w, true, LayerWall);
            // Interna horizontal (z = 4.5) com porta do quarto (x 1.8–2.7) e vão da cozinha (x 6–8)
            float z0 = 4.5f - 0.07f, z1 = 4.5f + 0.07f;
            Box("Wall_Mid_1", walls, new Vector3(0, -0.1f, z0), new Vector3(1.8f, H, z1), w, true, LayerWall);
            Box("Wall_Mid_DoorTop", walls, new Vector3(1.8f, 2.1f, z0), new Vector3(2.7f, H, z1), w, true, LayerWall);
            Box("Wall_Mid_2", walls, new Vector3(2.7f, -0.1f, z0), new Vector3(6f, H, z1), w, true, LayerWall);
            Box("Wall_Mid_ArchTop", walls, new Vector3(6f, 2.25f, z0), new Vector3(8f, H, z1), w, true, LayerWall);
            Box("Wall_Mid_3", walls, new Vector3(8f, -0.1f, z0), new Vector3(10f, H, z1), w, true, LayerWall);
            // Interna vertical (x = 5) entre quarto e cozinha
            Box("Wall_Quarto_Cozinha", walls, new Vector3(4.93f, -0.1f, z1), new Vector3(5.07f, H, 8f), w, true, LayerWall);

            // Batentes (só visual)
            Material trim = M["Trim"];
            Box("DoorFrame_L", walls, new Vector3(1.76f, 0, z0 - 0.01f), new Vector3(1.82f, 2.12f, z1 + 0.01f), trim, false);
            Box("DoorFrame_R", walls, new Vector3(2.68f, 0, z0 - 0.01f), new Vector3(2.74f, 2.12f, z1 + 0.01f), trim, false);
            Box("DoorFrame_T", walls, new Vector3(1.76f, 2.08f, z0 - 0.01f), new Vector3(2.74f, 2.14f, z1 + 0.01f), trim, false);

            // Janelas (só visual, "luz do dia")
            Window(walls, "Window_Sala", new Vector3(0.006f, 1.5f, 3.0f), new Vector3(0.012f, 1.0f, 1.2f));
            Window(walls, "Window_Sala_S", new Vector3(5.9f, 1.5f, 0.006f), new Vector3(1.4f, 1.0f, 0.012f));
            Window(walls, "Window_Cozinha", new Vector3(7.15f, 1.65f, 7.994f), new Vector3(1.3f, 0.8f, 0.012f));
            Window(walls, "Window_Quarto", new Vector3(4.4f, 1.5f, 7.994f), new Vector3(0.8f, 1.0f, 0.012f));
        }

        static void Window(Transform parent, string name, Vector3 center, Vector3 size)
        {
            BoxC(name, parent, center, size, M["Window"], false);
            Vector3 frame = size + new Vector3(size.x < 0.05f ? 0.01f : 0.1f, 0.1f, size.z < 0.05f ? 0.01f : 0.1f);
            Vector3 frameCenter = center;
            // moldura levemente atrás do vidro
            if (size.x < 0.05f) frameCenter.x -= Mathf.Sign(center.x - 5f) * 0.004f;
            else frameCenter.z -= Mathf.Sign(center.z - 4f) * 0.004f;
            BoxC(name + "_Frame", parent, frameCenter, frame, M["Trim"], false);
        }

        static void BuildStairs(Transform house)
        {
            Transform stairs = Empty("Stairs", house).transform;
            float x0 = StairHole.xMin, x1 = StairHole.xMax, zMax = StairHole.yMax;
            // Paredes do poço, logo abaixo do piso
            Box("PitWall_W", stairs, new Vector3(x0 - 0.15f, -2f, 0), new Vector3(x0, -0.001f, zMax), M["WallDark"], true);
            Box("PitWall_N", stairs, new Vector3(x0 - 0.15f, -2f, zMax), new Vector3(x1, -0.001f, zMax + 0.15f), M["WallDark"], true);
            // Degraus descendo para o leste (espelho de 0.25 m: o sensor de 0.2 m detecta a borda)
            const float riser = 0.25f, tread = 0.3f;
            for (int i = 0; i < 7; i++)
            {
                float top = -riser * (i + 1);
                Box("Step" + i, stairs, new Vector3(x0 + tread * i, -2f, 0), new Vector3(x1, top, zMax), i % 2 == 0 ? M["Stair"] : M["WoodDark"], true);
                // Focinho claro na beirada de cada degrau: dá leitura de "escada" vista de cima.
                Box("Nosing" + i, stairs, new Vector3(x0 + tread * i, top, 0), new Vector3(x0 + tread * i + 0.04f, top + 0.004f, zMax), M["Trim"], false);
            }
            Box("LowerFloor", stairs, new Vector3(x0, -2.1f, 0), new Vector3(x1, -2f, zMax), M["Void"], true);
            PointLight("StairLight", new Vector3(8.9f, -1.0f, 0.8f), new Color(0.55f, 0.65f, 0.95f), 0.8f, 2.5f).transform.SetParent(stairs, true);

            // Zona de queda: trigger logo abaixo da borda superior.
            var fall = Empty("FallZone", stairs);
            fall.layer = LayerIgnoreRaycast;
            var bc = fall.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = new Vector3((x0 + x1) / 2f, (-0.12f + -1.9f) / 2f, zMax / 2f);
            bc.size = new Vector3(x1 - x0, 1.78f, zMax);
            fall.AddComponent<FallZone>();

            // Faixa de aviso amarela pintada no chão (só visual) perto da borda
            Box("WarnStripe_W", stairs, new Vector3(x0 - 0.06f, 0.0005f, 0), new Vector3(x0, 0.0015f, zMax + 0.06f), M["Pillow"], false);
            Box("WarnStripe_N", stairs, new Vector3(x0, 0.0005f, zMax), new Vector3(x1, 0.0015f, zMax + 0.06f), M["Pillow"], false);
        }

        // ---------------------------------------------------------- móveis

        /// <summary>Raiz de um móvel "com vão": layer Occluder + fica transparente quando tapa o robô.</summary>
        static Transform Furniture(string name, Transform parent, Vector3 pos, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = new Vector3(0, yaw, 0);
            return go.transform;
        }

        static void FinishFadeable(Transform root)
        {
            SetLayerRecursive(root.gameObject, LayerOccluder);
            var f = root.gameObject.AddComponent<FadeableObject>();
            Set(f, "transparentTemplate", fadeTemplate);
        }

        static void Legs(Transform parent, float halfX, float halfZ, float height, float diameter, Material mat)
        {
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Cyl("Leg", parent, new Vector3(halfX * sx, 0, halfZ * sz), diameter, height, mat, true);
        }

        static Transform BuildSofa(Transform parent, Vector3 pos, float yaw)
        {
            Transform s = Furniture("Sofa", parent, pos, yaw);
            const float gap = 0.12f; // vão embaixo do sofá (robô tem 0.09)
            Legs(s, 1.0f, 0.36f, gap, 0.05f, M["WoodDark"]);
            Box("Base", s, new Vector3(-1.1f, gap, -0.45f), new Vector3(1.1f, 0.37f, 0.45f), M["Sofa"], true);
            Box("Back", s, new Vector3(-1.1f, 0.37f, 0.22f), new Vector3(1.1f, 0.86f, 0.45f), M["Sofa"], true);
            Box("ArmL", s, new Vector3(-1.1f, 0.37f, -0.45f), new Vector3(-0.92f, 0.62f, 0.22f), M["Sofa"], true);
            Box("ArmR", s, new Vector3(0.92f, 0.37f, -0.45f), new Vector3(1.1f, 0.62f, 0.22f), M["Sofa"], true);
            Box("CushionL", s, new Vector3(-0.91f, 0.37f, -0.43f), new Vector3(-0.01f, 0.48f, 0.21f), M["SofaCushion"], false);
            Box("CushionR", s, new Vector3(0.01f, 0.37f, -0.43f), new Vector3(0.91f, 0.48f, 0.21f), M["SofaCushion"], false);
            GameObject p1 = BoxC("Pillow1", s, new Vector3(-0.65f, 0.6f, 0.13f), new Vector3(0.38f, 0.3f, 0.1f), M["Pillow"], false);
            p1.transform.localEulerAngles = new Vector3(-15, 10, 8);
            GameObject p2 = BoxC("Pillow2", s, new Vector3(0.62f, 0.6f, 0.13f), new Vector3(0.36f, 0.3f, 0.1f), M["Duvet"], false);
            p2.transform.localEulerAngles = new Vector3(-15, -8, -6);
            FinishFadeable(s);
            return s;
        }

        static void BuildLivingRoom(Transform parent)
        {
            BuildSofa(parent, new Vector3(3.0f, 0, 3.05f), 180f); // de costas para a parede do quarto, virado para a TV

            // Mesa de centro (robô passa embaixo)
            Transform ct = Furniture("CoffeeTable", parent, new Vector3(3.0f, 0, 1.75f));
            Box("Top", ct, new Vector3(-0.55f, 0.36f, -0.28f), new Vector3(0.55f, 0.4f, 0.28f), M["WoodLight"], true);
            Legs(ct, 0.5f, 0.23f, 0.36f, 0.04f, M["WoodDark"]);
            BoxC("Book", ct, new Vector3(0.2f, 0.415f, 0.05f), new Vector3(0.22f, 0.03f, 0.16f), M["BookB"], false);
            Cyl("Mug", ct, new Vector3(-0.25f, 0.4f, -0.05f), 0.08f, 0.09f, M["Pillow"]);
            FinishFadeable(ct);

            // Tapete (só visual: um degrau de 4 mm já travaria o robô)
            BoxC("Rug_Sala", parent, new Vector3(3.0f, 0.002f, 1.95f), new Vector3(2.7f, 0.004f, 1.9f), M["Rug"], false);

            // Rack + TV (sólidos, layer Wall: a câmera desvia deles)
            Transform rack = Furniture("TVRack", parent, new Vector3(3.0f, 0, 0.23f));
            Box("Rack", rack, new Vector3(-0.9f, 0, -0.21f), new Vector3(0.9f, 0.48f, 0.21f), M["WoodDark"], true);
            Box("TV", rack, new Vector3(-0.62f, 0.55f, -0.04f), new Vector3(0.62f, 1.25f, 0.02f), M["Black"], true);
            Box("Screen", rack, new Vector3(-0.59f, 0.58f, 0.02f), new Vector3(0.59f, 1.22f, 0.025f), M["Screen"], false);
            Box("Foot", rack, new Vector3(-0.15f, 0.48f, -0.1f), new Vector3(0.15f, 0.55f, 0.06f), M["Black"], false);
            SetLayerRecursive(rack.gameObject, LayerWall);

            // Estante de livros (parede oeste)
            Transform shelf = Furniture("Bookshelf", parent, new Vector3(0.18f, 0, 1.6f));
            Box("Case", shelf, new Vector3(-0.18f, 0, -0.6f), new Vector3(0.18f, 1.8f, 0.6f), M["WoodLight"], true);
            Material[] books = { M["BookA"], M["BookB"], M["BookC"] };
            var rnd = new System.Random(3);
            for (int row = 0; row < 4; row++)
            {
                float z = -0.55f;
                while (z < 0.5f)
                {
                    float wBook = 0.04f + (float)rnd.NextDouble() * 0.05f;
                    float hBook = 0.2f + (float)rnd.NextDouble() * 0.12f;
                    Box("Book", shelf, new Vector3(0.18f, 0.12f + row * 0.42f, z), new Vector3(0.195f, 0.12f + row * 0.42f + hBook, z + wBook),
                        books[rnd.Next(books.Length)], false);
                    z += wBook + 0.008f;
                }
            }
            SetLayerRecursive(shelf.gameObject, LayerWall);

            // Abajur de chão (luz de ambiente)
            Transform lamp = Furniture("FloorLamp", parent, new Vector3(0.45f, 0, 4.05f));
            Cyl("Base", lamp, Vector3.zero, 0.3f, 0.03f, M["Black"], true);
            Cyl("Pole", lamp, new Vector3(0, 0.03f, 0), 0.03f, 1.45f, M["Metal"], true);
            Cyl("Shade", lamp, new Vector3(0, 1.42f, 0), 0.42f, 0.3f, M["Shade"]);
            SetLayerRecursive(lamp.gameObject, LayerWall);
            PointLight("Light_FloorLamp", new Vector3(0.5f, 1.35f, 3.95f), new Color(1f, 0.82f, 0.6f), 2.2f, 5f);

            // Vaso de planta (canto leste)
            Transform plant = Furniture("Plant", parent, new Vector3(9.6f, 0, 4.05f));
            Cyl("Pot", plant, Vector3.zero, 0.4f, 0.38f, M["Pot"], true);
            Sphere("Leaves1", plant, new Vector3(0, 0.62f, 0), new Vector3(0.5f, 0.45f, 0.5f), M["Leaves"]);
            Sphere("Leaves2", plant, new Vector3(0.1f, 0.85f, -0.05f), new Vector3(0.35f, 0.35f, 0.35f), M["Leaves"]);
            Sphere("Leaves3", plant, new Vector3(-0.1f, 0.78f, 0.1f), new Vector3(0.3f, 0.3f, 0.3f), M["Leaves"]);
            SetLayerRecursive(plant.gameObject, LayerWall);

            // Poltrona pequena perto da janela sul? (deixamos o centro livre para circular)
        }

        static void BuildBedroom(Transform parent)
        {
            // Cama de casal com vão de 0.15 m
            Transform bed = Furniture("Bed", parent, new Vector3(2.6f, 0, 7.0f));
            const float gap = 0.15f;
            Legs(bed, 0.75f, 0.94f, gap, 0.06f, M["WoodDark"]);
            Box("Frame", bed, new Vector3(-0.8f, gap, -1f), new Vector3(0.8f, 0.38f, 1f), M["WoodLight"], true);
            Box("Mattress", bed, new Vector3(-0.77f, 0.38f, -0.97f), new Vector3(0.77f, 0.58f, 0.95f), M["Sheet"], true);
            Box("Duvet", bed, new Vector3(-0.8f, 0.5f, -1f), new Vector3(0.8f, 0.62f, 0.35f), M["Duvet"], false);
            BoxC("PillowL", bed, new Vector3(-0.38f, 0.65f, 0.7f), new Vector3(0.6f, 0.12f, 0.34f), M["Sheet"], false);
            BoxC("PillowR", bed, new Vector3(0.38f, 0.65f, 0.7f), new Vector3(0.6f, 0.12f, 0.34f), M["Sheet"], false);
            Box("Headboard", bed, new Vector3(-0.85f, gap, 0.96f), new Vector3(0.85f, 1.05f, 1f), M["WoodDark"], true);
            FinishFadeable(bed);

            // Criados-mudos (sólidos)
            foreach (float x in new[] { 1.5f, 3.7f })
            {
                Transform ns = Furniture("Nightstand", parent, new Vector3(x, 0, 7.78f));
                Box("Body", ns, new Vector3(-0.22f, 0, -0.2f), new Vector3(0.22f, 0.5f, 0.2f), M["WoodLight"], true);
                Box("Drawer", ns, new Vector3(-0.19f, 0.28f, -0.205f), new Vector3(0.19f, 0.44f, -0.2f), M["WoodDark"], false);
            }
            Transform lamp = Furniture("BedLamp", parent, new Vector3(1.5f, 0.5f, 7.8f));
            Cyl("Base", lamp, Vector3.zero, 0.1f, 0.18f, M["Metal"]);
            Cyl("Shade", lamp, new Vector3(0, 0.16f, 0), 0.24f, 0.18f, M["Shade"]);
            PointLight("Light_BedLamp", new Vector3(1.55f, 0.85f, 7.6f), new Color(1f, 0.8f, 0.55f), 1.4f, 3.5f);

            // Guarda-roupa (alto, layer Wall)
            Transform wardrobe = Furniture("Wardrobe", parent, new Vector3(0.3f, 0, 5.9f));
            Box("Body", wardrobe, new Vector3(-0.3f, 0, -0.9f), new Vector3(0.3f, 2.0f, 0.9f), M["WoodLight"], true);
            Box("Seam", wardrobe, new Vector3(0.3f, 0.05f, -0.005f), new Vector3(0.305f, 1.95f, 0.005f), M["WoodDark"], false);
            Box("HandleL", wardrobe, new Vector3(0.3f, 0.95f, -0.08f), new Vector3(0.32f, 1.15f, -0.06f), M["Metal"], false);
            Box("HandleR", wardrobe, new Vector3(0.3f, 0.95f, 0.06f), new Vector3(0.32f, 1.15f, 0.08f), M["Metal"], false);
            SetLayerRecursive(wardrobe.gameObject, LayerWall);

            // Escrivaninha (robô passa embaixo)
            Transform desk = Furniture("Desk", parent, new Vector3(4.45f, 0, 7.62f));
            Box("Top", desk, new Vector3(-0.45f, 0.71f, -0.3f), new Vector3(0.45f, 0.75f, 0.3f), M["WoodLight"], true);
            Legs(desk, 0.41f, 0.26f, 0.71f, 0.04f, M["Metal"]);
            BoxC("Laptop", desk, new Vector3(0, 0.76f, 0), new Vector3(0.32f, 0.015f, 0.22f), M["Black"], false);
            FinishFadeable(desk);

            BoxC("Rug_Quarto", parent, new Vector3(2.6f, 0.002f, 5.55f), new Vector3(2.2f, 0.004f, 1.0f), M["RugBlue"], false);
        }

        static void BuildKitchen(Transform parent)
        {
            Transform counters = Furniture("Counters", parent, Vector3.zero);
            Box("Base", counters, new Vector3(5.07f, 0, 7.4f), new Vector3(9.3f, 0.86f, 8f), M["Counter"], true);
            Box("Top", counters, new Vector3(5.07f, 0.86f, 7.36f), new Vector3(9.3f, 0.9f, 8f), M["CounterTop"], true);
            Box("Toe", counters, new Vector3(5.07f, 0, 7.39f), new Vector3(9.3f, 0.08f, 7.41f), M["CounterTop"], false);
            for (float x = 5.6f; x < 9.3f; x += 0.6f)
                Box("DoorLine", counters, new Vector3(x - 0.004f, 0.1f, 7.392f), new Vector3(x + 0.004f, 0.82f, 7.4f), M["CounterTop"], false);
            BoxC("Sink", counters, new Vector3(7.15f, 0.902f, 7.7f), new Vector3(0.55f, 0.006f, 0.38f), M["Metal"], false);
            BoxC("Stove", counters, new Vector3(8.6f, 0.902f, 7.7f), new Vector3(0.58f, 0.006f, 0.5f), M["Black"], false);
            Box("Upper1", counters, new Vector3(5.07f, 1.5f, 7.65f), new Vector3(6.3f, 2.2f, 8f), M["Counter"], true);
            Box("Upper2", counters, new Vector3(8.0f, 1.5f, 7.65f), new Vector3(9.3f, 2.2f, 8f), M["Counter"], true);
            SetLayerRecursive(counters.gameObject, LayerWall);

            Transform fridge = Furniture("Fridge", parent, Vector3.zero);
            Box("Body", fridge, new Vector3(9.3f, 0, 7.25f), new Vector3(10f, 1.85f, 8f), M["Fridge"], true);
            Box("Line", fridge, new Vector3(9.3f, 1.2f, 7.245f), new Vector3(10f, 1.21f, 7.25f), M["Metal"], false);
            Box("Handle", fridge, new Vector3(9.38f, 0.7f, 7.2f), new Vector3(9.41f, 1.1f, 7.25f), M["Metal"], false);
            SetLayerRecursive(fridge.gameObject, LayerWall);

            // Mesa de jantar + 4 cadeiras (robô passa entre os pés)
            Transform table = Furniture("DiningTable", parent, new Vector3(7.5f, 0, 6.0f));
            Box("Top", table, new Vector3(-0.7f, 0.73f, -0.4f), new Vector3(0.7f, 0.77f, 0.4f), M["WoodLight"], true);
            Legs(table, 0.65f, 0.35f, 0.73f, 0.05f, M["WoodDark"]);
            Cyl("FruitBowl", table, new Vector3(0, 0.77f, 0), 0.26f, 0.06f, M["Pot"]);
            Sphere("Orange", table, new Vector3(0.04f, 0.84f, 0.03f), Vector3.one * 0.08f, M["Cat"]);
            Sphere("Apple", table, new Vector3(-0.05f, 0.84f, -0.03f), Vector3.one * 0.075f, M["BookA"]);
            FinishFadeable(table);

            float[] xs = { 7.15f, 7.85f };
            foreach (float x in xs)
            {
                BuildChair(parent, new Vector3(x, 0, 5.25f), 0f);    // sul, virada para a mesa (norte)
                BuildChair(parent, new Vector3(x, 0, 6.75f), 180f);  // norte, virada para a mesa (sul)
            }

            Transform bin = Furniture("TrashBin", parent, new Vector3(9.75f, 0, 4.85f));
            Cyl("Bin", bin, Vector3.zero, 0.3f, 0.45f, M["Metal"], true);
            Cyl("Lid", bin, new Vector3(0, 0.45f, 0), 0.31f, 0.02f, M["Black"]);

            // Luminária pendente sobre a mesa
            Transform pendant = Furniture("Pendant", parent, new Vector3(7.5f, 0, 6.0f));
            Cyl("Cord", pendant, new Vector3(0, 1.75f, 0), 0.01f, 0.85f, M["Black"]);
            Cyl("Shade", pendant, new Vector3(0, 1.6f, 0), 0.35f, 0.16f, M["Shade"]);
            PointLight("Light_Kitchen", new Vector3(7.5f, 1.5f, 6.0f), new Color(1f, 0.88f, 0.7f), 2.2f, 5f);
        }

        static void BuildChair(Transform parent, Vector3 pos, float yaw)
        {
            Transform c = Furniture("Chair", parent, pos, yaw);
            // Pés afastados 0.44 m: sobra ~3.5 cm de cada lado para o robô (0.35 m) passar.
            Legs(c, 0.22f, 0.2f, 0.45f, 0.035f, M["WoodDark"]);
            Box("Seat", c, new Vector3(-0.25f, 0.45f, -0.23f), new Vector3(0.25f, 0.49f, 0.23f), M["WoodLight"], true);
            Box("Back", c, new Vector3(-0.24f, 0.49f, -0.24f), new Vector3(0.24f, 0.92f, -0.2f), M["WoodLight"], true);
            FinishFadeable(c);
        }

        // ---------------------------------------------------------- sujeira

        struct DirtRegion
        {
            public string name; public Rect rect; public int count; public bool under;
            public DirtRegion(string n, float x0, float z0, float x1, float z1, int c, bool u)
            { name = n; rect = Rect.MinMaxRect(x0, z0, x1, z1); count = c; under = u; }
        }

        // ~60 manchas, 15 embaixo de móveis (GDD seção 7)
        static readonly DirtRegion[] DirtRegions =
        {
            new DirtRegion("Sofa", 2.1f, 2.75f, 3.9f, 3.35f, 5, true),
            new DirtRegion("Bed", 1.95f, 6.2f, 3.25f, 7.65f, 5, true),
            new DirtRegion("CoffeeTable", 2.62f, 1.6f, 3.38f, 1.9f, 2, true),
            new DirtRegion("DiningTable", 7.0f, 5.8f, 8.0f, 6.2f, 2, true),
            new DirtRegion("Desk", 4.2f, 7.45f, 4.7f, 7.7f, 1, true),
            new DirtRegion("Sala", 0.4f, 0.4f, 7.45f, 4.2f, 18, false),
            new DirtRegion("SalaLeste", 7.9f, 2.0f, 9.75f, 4.2f, 4, false),
            new DirtRegion("Quarto", 0.75f, 4.75f, 4.75f, 7.8f, 11, false),
            new DirtRegion("Cozinha", 5.3f, 4.75f, 9.75f, 7.15f, 10, false),
        };

        // Duas manchas "tentadoras" perto da borda da escada (risco x recompensa)
        static readonly Vector3[] StairDirt = { new Vector3(7.45f, 0, 0.8f), new Vector3(8.7f, 0, 1.95f) };

        public static void ScatterDirt(Transform root, GameObject dustPrefab, GameObject crumbsPrefab, int seed)
        {
            for (int i = root.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.GetChild(i).gameObject);
            Physics.SyncTransforms();
            var rnd = new System.Random(seed);
            var placed = new List<Vector3>();

            int solidMask = ~((1 << LayerIgnoreRaycast) | (1 << LayerPlayer) | (1 << LayerCat) | (1 << LayerUI));

            void Place(Vector3 p, bool under)
            {
                GameObject prefab = rnd.NextDouble() < 0.6 ? dustPrefab : crumbsPrefab;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                go.name = (under ? "Dirt_Under_" : "Dirt_") + placed.Count.ToString("00");
                go.transform.position = new Vector3(p.x, 0.0005f * (placed.Count % 4), p.z);
                go.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                float s = 0.85f + (float)rnd.NextDouble() * 0.45f;
                go.transform.localScale = new Vector3(s, 1f, s);
                SetBool(go.GetComponent<DirtSpot>(), "underFurniture", under);
                placed.Add(p);
            }

            foreach (Vector3 p in StairDirt) Place(p, false);

            foreach (DirtRegion region in DirtRegions)
            {
                int done = 0, attempts = 0;
                float minDist = region.under ? 0.32f : 0.5f;
                while (done < region.count && attempts++ < 3000)
                {
                    var p = new Vector3(
                        Mathf.Lerp(region.rect.xMin, region.rect.xMax, (float)rnd.NextDouble()), 0,
                        Mathf.Lerp(region.rect.yMin, region.rect.yMax, (float)rnd.NextDouble()));

                    if (placed.Any(q => (q - p).sqrMagnitude < minDist * minDist)) continue;
                    // Longe do buraco da escada, da base e das meias
                    Rect hole = StairHole;
                    hole.xMin -= 0.4f; hole.yMax += 0.4f;
                    if (hole.Contains(new Vector2(p.x, p.z))) continue;
                    if (Vector3.Distance(p, new Vector3(DockPos.x, 0, DockPos.z - 0.15f)) < 0.65f) continue;
                    if (SockPositions.Any(s => Vector3.Distance(s, p) < 0.45f)) continue;
                    // O robô cabe aqui? (caixa do tamanho do robô, um pouco acima do chão)
                    if (Physics.CheckBox(new Vector3(p.x, 0.06f, p.z), new Vector3(0.19f, 0.035f, 0.19f), Quaternion.identity, solidMask, QueryTriggerInteraction.Ignore)) continue;
                    // Embaixo de móvel? (raio para cima bate em algo)
                    bool covered = Physics.Raycast(new Vector3(p.x, 0.1f, p.z), Vector3.up, 2.4f, solidMask, QueryTriggerInteraction.Ignore);
                    if (covered != region.under) continue;
                    Place(p, region.under);
                    done++;
                }
                if (done < region.count) Debug.LogWarning($"[Poeirinha] Região {region.name}: só coube {done}/{region.count} manchas.");
            }
            Debug.Log($"[Poeirinha] {placed.Count} manchas de sujeira espalhadas.");
        }

        // ---------------------------------------------------------- NavMesh

        static void BakeNavMesh()
        {
            var go = new GameObject("NavMesh");
            var surface = go.AddComponent<NavMeshSurface>();
            surface.agentTypeID = 0;
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~((1 << LayerPlayer) | (1 << LayerCat) | (1 << LayerIgnoreRaycast) | (1 << LayerUI));
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            string path = SceneDir + "/Level01_NavMesh.asset";
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path);
        }

        // =====================================================================
        // UI do jogo
        // =====================================================================

        static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerUI;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        static void CreateEventSystem()
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        static void BuildGameUI(BatterySystem battery, CleaningManager cleaning, LevelTimer timer, CliffSensor cliff, RobotController robot)
        {
            Canvas canvas = CreateCanvas("Canvas");
            Transform c = canvas.transform;
            CreateEventSystem();

            // ---------------- HUD
            RectTransform hud = Stretch("HUD", c);
            var hudGroup = hud.gameObject.AddComponent<CanvasGroup>();
            hudGroup.interactable = false;
            hudGroup.blocksRaycasts = false;
            var hudCtrl = hud.gameObject.AddComponent<HUDController>();

            // Limpeza (topo-esquerda)
            RectTransform cleanPanel = UIRect("CleanPanel", hud, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -36), new Vector2(470, 112));
            UIImage(cleanPanel, PanelBg);
            Label("Title", cleanPanel, "LIMPEZA", 26, Mint, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -12), new Vector2(260, 40), TextAlignmentOptions.Left, FontStyles.Bold);
            Label("Goal", cleanPanel, "meta 90%", 20, new Color(1, 1, 1, 0.55f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(150, -16), new Vector2(160, 34), TextAlignmentOptions.Left);
            TextMeshProUGUI cleanText = Label("Percent", cleanPanel, "0%", 44, Color.white, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -6), new Vector2(170, 52), TextAlignmentOptions.Right, FontStyles.Bold);
            RectTransform cleanBar = UIRect("Bar", cleanPanel, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 20), new Vector2(422, 26));
            UIImage(cleanBar, new Color(1, 1, 1, 0.12f));
            RectTransform cleanFill = Stretch("Fill", cleanBar);
            cleanFill.anchorMax = new Vector2(0, 1);
            UIImage(cleanFill, Mint);
            RectTransform goalMark = UIRect("GoalMark", cleanBar, new Vector2(0.9f, 0), new Vector2(0.9f, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4, 12));
            UIImage(goalMark, Yellow, null, false);

            // Timer (topo-direita)
            RectTransform timerPanel = UIRect("TimerPanel", hud, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -36), new Vector2(270, 126));
            UIImage(timerPanel, PanelBg);
            Label("Title", timerPanel, "O DONO VOLTA EM", 20, new Color(1, 1, 1, 0.65f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(260, 30), TextAlignmentOptions.Center, FontStyles.Bold);
            TextMeshProUGUI timerText = Label("Time", timerPanel, "3:00", 64, Color.white, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(260, 80), TextAlignmentOptions.Center, FontStyles.Bold);

            // Bateria (base-esquerda)
            RectTransform batPanel = UIRect("BatteryPanel", hud, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 36), new Vector2(470, 126));
            UIImage(batPanel, PanelBg);
            Label("Title", batPanel, "BATERIA", 24, Mint, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -10), new Vector2(200, 36), TextAlignmentOptions.Left, FontStyles.Bold);
            TextMeshProUGUI chargingLabel = Label("Charging", batPanel, "CARREGANDO...", 22, new Color(0.45f, 0.72f, 1f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -12), new Vector2(240, 34), TextAlignmentOptions.Right, FontStyles.Bold);
            RectTransform batBody = UIRect("Body", batPanel, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 20), new Vector2(310, 58));
            UIImage(batBody, new Color(1, 1, 1, 0.85f));
            RectTransform batInner = Stretch("Inner", batBody, 5);
            UIImage(batInner, new Color(0.08f, 0.09f, 0.13f, 1f));
            RectTransform batFillArea = Stretch("FillArea", batInner, 4);
            RectTransform batFill = Stretch("Fill", batFillArea);
            Image batFillImg = UIImage(batFill, new Color(0.35f, 0.9f, 0.55f));
            RectTransform nub = UIRect("Nub", batPanel, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0.5f), new Vector2(334, 49), new Vector2(12, 26));
            UIImage(nub, new Color(1, 1, 1, 0.85f), null, false);
            TextMeshProUGUI batText = Label("Percent", batPanel, "100%", 34, Color.white, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(352, 20), new Vector2(100, 58), TextAlignmentOptions.Right, FontStyles.Bold);

            // Avisos no centro
            RectTransform warn = UIRect("Warning", hud, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(820, 86));
            UIImage(warn, new Color(0.78f, 0.16f, 0.13f, 0.88f));
            TextMeshProUGUI warnText = UIText(Stretch("Text", warn, 8), "Aviso", 38, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            warn.gameObject.SetActive(false);

            TextMeshProUGUI toast = Label("Toast", hud, "Metade!", 64, Yellow, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(1000, 90), TextAlignmentOptions.Center, FontStyles.Bold);
            toast.outlineWidth = 0.15f;
            toast.outlineColor = new Color(0.1f, 0.08f, 0.05f, 1f);

            Label("Hint", hud, "WASD / setas: mover   |   Shift: turbo   |   E: soltar meia   |   Esc: pausa", 22, new Color(1, 1, 1, 0.55f),
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 30), new Vector2(1000, 34), TextAlignmentOptions.Right);

            Set(hudCtrl, "battery", battery);
            Set(hudCtrl, "cleaning", cleaning);
            Set(hudCtrl, "timer", timer);
            Set(hudCtrl, "cliffSensor", cliff);
            Set(hudCtrl, "robot", robot);
            Set(hudCtrl, "cleanFill", cleanFill);
            Set(hudCtrl, "cleanText", cleanText);
            Set(hudCtrl, "timerText", timerText);
            Set(hudCtrl, "batteryFill", batFill);
            Set(hudCtrl, "batteryFillImage", batFillImg);
            Set(hudCtrl, "batteryText", batText);
            Set(hudCtrl, "chargingLabel", chargingLabel.gameObject);
            Set(hudCtrl, "warningRoot", warn.gameObject);
            Set(hudCtrl, "warningText", warnText);
            Set(hudCtrl, "toastText", toast);
            Set(hudCtrl, "hudGroup", hudGroup);

            // ---------------- Pausa
            var pauseGo = new GameObject("PauseMenu", typeof(RectTransform));
            pauseGo.transform.SetParent(c, false);
            var pauseMenu = pauseGo.AddComponent<PauseMenu>();
            RectTransform pausePanel = Stretch("Panel", pauseGo.transform);
            ((RectTransform)pauseGo.transform).anchorMin = Vector2.zero;
            ((RectTransform)pauseGo.transform).anchorMax = Vector2.one;
            ((RectTransform)pauseGo.transform).sizeDelta = Vector2.zero;
            Image dim = UIImage(pausePanel, new Color(0, 0, 0, 0.55f), null, false);
            dim.raycastTarget = true;
            RectTransform card = UIRect("Card", pausePanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 540));
            UIImage(card, PanelBg);
            Label("Title", card, "Pausado", 70, Color.white, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(500, 90), TextAlignmentOptions.Center, FontStyles.Bold);
            Button bContinue = UIButton("Continue", card, "Continuar", new Vector2(0, 50), new Vector2(380, 90), ButtonNormal, ButtonHighlight, Color.white, pauseMenu.OnContinueClicked, out _);
            Button bRestart = UIButton("Restart", card, "Reiniciar", new Vector2(0, -60), new Vector2(380, 90), ButtonNormal, ButtonHighlight, Color.white, pauseMenu.OnRestartClicked, out _);
            Button bMenu = UIButton("Menu", card, "Menu", new Vector2(0, -170), new Vector2(380, 90), ButtonNormal, ButtonHighlight, Color.white, pauseMenu.OnMenuClicked, out _);
            ChainVertical(bContinue, bRestart, bMenu);
            Set(pauseMenu, "panel", pausePanel.gameObject);
            Set(pauseMenu, "firstSelected", bContinue.gameObject);

            // ---------------- Fim de jogo
            var endGo = new GameObject("EndScreen", typeof(RectTransform));
            endGo.transform.SetParent(c, false);
            ((RectTransform)endGo.transform).anchorMin = Vector2.zero;
            ((RectTransform)endGo.transform).anchorMax = Vector2.one;
            ((RectTransform)endGo.transform).sizeDelta = Vector2.zero;
            var end = endGo.AddComponent<EndScreen>();
            RectTransform endPanel = Stretch("Panel", endGo.transform);
            Image endDim = UIImage(endPanel, new Color(0, 0, 0, 0.6f), null, false);
            endDim.raycastTarget = true;
            RectTransform endCard = UIRect("Card", endPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 640));
            UIImage(endCard, PanelBg);
            TextMeshProUGUI endTitle = Label("Title", endCard, "Casa limpa!", 84, Mint, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(840, 110), TextAlignmentOptions.Center, FontStyles.Bold);
            TextMeshProUGUI endSub = Label("Subtitle", endCard, "Subtítulo", 38, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(840, 60));
            RectTransform starsRoot = UIRect("Stars", endCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 45), new Vector2(460, 130));
            var stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform st = UIRect("Star" + i, starsRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 145, i == 1 ? 10 : -5), new Vector2(i == 1 ? 130 : 110, i == 1 ? 130 : 110));
                stars[i] = UIImage(st, Yellow, starSprite, false);
                stars[i].preserveAspect = true;
            }
            TextMeshProUGUI endDetail = Label("Detail", endCard, "Detalhes", 30, new Color(1, 1, 1, 0.8f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(840, 50));
            Button bRetry = UIButton("Retry", endCard, "Jogar de novo", new Vector2(-200, -205), new Vector2(370, 92), ButtonNormal, ButtonHighlight, Color.white, end.OnRetryClicked, out TextMeshProUGUI retryLabel);
            Button bEndMenu = UIButton("Menu", endCard, "Menu", new Vector2(200, -205), new Vector2(370, 92), ButtonNormal, ButtonHighlight, Color.white, end.OnMenuClicked, out _);
            ChainHorizontal(bRetry, bEndMenu);
            Set(end, "panel", endPanel.gameObject);
            Set(end, "titleText", endTitle);
            Set(end, "subtitleText", endSub);
            Set(end, "detailText", endDetail);
            Set(end, "starsRoot", starsRoot.gameObject);
            SetArray(end, "stars", stars);
            Set(end, "primaryButtonLabel", retryLabel);
            Set(end, "firstSelected", bRetry.gameObject);
        }

        // =====================================================================
        // Menu principal
        // =====================================================================

        static void BuildMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();

            // Cenário: um cantinho da sala com o robô e o gato dormindo.
            Transform set = Empty("Diorama", null).transform;
            MeshObject("Floor", set, FloorQuad("Menu_Floor", new Vector2(-3, -3), new Vector2(3, 3)), M["Wood"], Vector3.zero);
            Box("WallN", set, new Vector3(-3, 0, 1.6f), new Vector3(3, 2.6f, 1.75f), M["Wall"], false);
            Box("WallW", set, new Vector3(-1.75f, 0, -3), new Vector3(-1.6f, 2.6f, 1.75f), M["Wall"], false);
            BoxC("Rug", set, new Vector3(0.2f, 0.002f, 0.1f), new Vector3(2.2f, 0.004f, 1.6f), M["Rug"], false);
            Transform sofa = BuildSofa(set, new Vector3(-0.1f, 0, 1.1f), 180f);
            Object.DestroyImmediate(sofa.GetComponent<FadeableObject>());
            Transform lamp = Furniture("FloorLamp", set, new Vector3(-1.3f, 0, 1.25f));
            Cyl("Base", lamp, Vector3.zero, 0.3f, 0.03f, M["Black"]);
            Cyl("Pole", lamp, new Vector3(0, 0.03f, 0), 0.03f, 1.45f, M["Metal"]);
            Cyl("Shade", lamp, new Vector3(0, 1.42f, 0), 0.42f, 0.3f, M["Shade"]);
            PointLight("LampLight", new Vector3(-1.25f, 1.35f, 1.15f), new Color(1f, 0.82f, 0.6f), 2.5f, 5f);

            var robot = new GameObject("Robot");
            robot.transform.SetPositionAndRotation(new Vector3(0.1f, 0, -0.1f), Quaternion.Euler(0, 150, 0));
            BuildRobotModel(robot.transform, out Renderer led, out Light ledLight, out Transform[] brushes);
            var visuals = robot.AddComponent<RobotVisuals>();
            Set(visuals, "ledRenderer", led);
            Set(visuals, "ledLight", ledLight);
            SetArray(visuals, "brushes", brushes);

            var cat = new GameObject("Cat");
            cat.transform.SetPositionAndRotation(new Vector3(0.75f, 0, 0.45f), Quaternion.Euler(0, 235, 0));
            Transform model = BuildCatModel(cat.transform, out Transform tail, out GameObject zzz);
            model.localPosition = new Vector3(0, -0.07f, 0);
            model.localScale = new Vector3(1.1f, 0.75f, 1f);
            zzz.SetActive(true);

            var rnd = new System.Random(5);
            for (int i = 0; i < 9; i++)
            {
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = "Dust";
                q.transform.SetParent(set, false);
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = 0.35f + (float)rnd.NextDouble() * 0.9f;
                q.transform.localPosition = new Vector3(Mathf.Cos(a) * r, 0.003f, Mathf.Sin(a) * r * 0.8f - 0.3f);
                q.transform.localEulerAngles = new Vector3(90, (float)rnd.NextDouble() * 360f, 0);
                float s = 0.22f + (float)rnd.NextDouble() * 0.12f;
                q.transform.localScale = new Vector3(s, s, 1);
                q.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? M["Dust"] : M["Crumbs"];
            }

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.03f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.11f, 0.15f);
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var orbit = camGo.AddComponent<MenuCameraOrbit>();
            Set(orbit, "pivot", robot.transform);
            camGo.transform.position = robot.transform.position + Quaternion.Euler(0, -35, 0) * new Vector3(0, 0.6f, -1.4f);
            camGo.transform.LookAt(robot.transform.position + Vector3.up * 0.12f);
            CreatePostVolume();

            new GameObject("AudioManager").AddComponent<AudioManager>();

            // UI
            Canvas canvas = CreateCanvas("Canvas");
            CreateEventSystem();
            var menuGo = new GameObject("MainMenu", typeof(RectTransform));
            menuGo.transform.SetParent(canvas.transform, false);
            var menuRt = (RectTransform)menuGo.transform;
            menuRt.anchorMin = Vector2.zero; menuRt.anchorMax = Vector2.one; menuRt.sizeDelta = Vector2.zero;
            var menu = menuGo.AddComponent<MainMenu>();

            RectTransform main = UIRect("MainPanel", menuRt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(90, 0), new Vector2(660, 860));
            UIImage(main, PanelBg);
            TextMeshProUGUI title = Label("Title", main, "Poeirinha", 120, Mint, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(620, 150), TextAlignmentOptions.Center, FontStyles.Bold);
            title.outlineWidth = 0.12f;
            title.outlineColor = new Color(0.05f, 0.2f, 0.17f);
            Label("Subtitle", main, "Limpe a casa antes do dono voltar!", 32, new Color(1, 1, 1, 0.85f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 195), new Vector2(600, 90));
            Button play = UIButton("Play", main, "Jogar", new Vector2(0, 40), new Vector2(440, 96), ButtonNormal, ButtonHighlight, Color.white, menu.OnPlayClicked, out _);
            Button howTo = UIButton("HowTo", main, "Como jogar", new Vector2(0, -75), new Vector2(440, 96), ButtonNormal, ButtonHighlight, Color.white, menu.OnHowToClicked, out _);
            Button quit = UIButton("Quit", main, "Sair", new Vector2(0, -190), new Vector2(440, 96), ButtonNormal, ButtonHighlight, Color.white, menu.OnQuitClicked, out _);
            ChainVertical(play, howTo, quit);
            Label("Footer", main, "Projeto 3D - Insper 2026.2", 22, new Color(1, 1, 1, 0.45f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(600, 40));

            RectTransform how = UIRect("HowToPanel", menuRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 860));
            UIImage(how, PanelBg);
            Label("Title", how, "Como jogar", 76, Mint, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(1000, 100), TextAlignmentOptions.Center, FontStyles.Bold);
            string body =
                "Você é um <color=#5CE0B8><b>robô aspirador</b></color>. Aspire <b>90%</b> da sujeira antes do dono voltar (3 minutos).\n\n" +
                "<b>W / S</b> ou setas: andar para frente / ré\n" +
                "<b>A / D</b> ou setas: girar\n" +
                "<b>Shift</b> (segurar): turbo - mais rápido, gasta o dobro de bateria\n" +
                "<b>E</b> (várias vezes): soltar-se de uma meia enroscada\n" +
                "<b>Esc</b>: pausar\n\n" +
                "<color=#FFD24D>Bateria:</color> volte à <b>base azul</b> para recarregar.\n" +
                "<color=#FF6B5E>Cuidado:</color> a escada não tem grade - se cair, acabou! O bip avisa quando a borda está perto.\n" +
                "O <b>gato</b> empurra o robô e tira bateria. Tem sujeira escondida <b>embaixo dos móveis</b>!";
            Label("Body", how, body, 31, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1060, 560), TextAlignmentOptions.Left);
            Button back = UIButton("Back", how, "Voltar", new Vector2(0, -350), new Vector2(360, 90), ButtonNormal, ButtonHighlight, Color.white, menu.OnBackClicked, out _);

            Set(menu, "mainPanel", main.gameObject);
            Set(menu, "howToPanel", how.gameObject);
            Set(menu, "firstSelectedMain", play.gameObject);
            Set(menu, "firstSelectedHowTo", back.gameObject);
            how.gameObject.SetActive(false);

            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }
    }
}
