using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Poeirinha.EditorTools
{
    /// <summary>
    /// Tira prints das cenas sem dar Play (útil para a página do itch.io e para conferir o visual).
    /// Menu: Tools > Poeirinha > Capturar screenshots → pasta Screenshots/ na raiz do projeto.
    /// </summary>
    public static class ScreenshotTool
    {
        [MenuItem("Tools/Poeirinha/Capturar screenshots", priority = 30)]
        public static void Capture()
        {
            Directory.CreateDirectory("Screenshots");
            EditorSceneManager.OpenScene(PoeirinhaBuilder.LevelScenePath);
            var cam = Camera.main;
            var robot = UnityEngine.Object.FindFirstObjectByType<RobotController>().transform;
            var follow = cam.GetComponent<CameraFollow>();
            Physics.SyncTransforms();

            (string name, Vector3 pos, float yaw)[] views =
            {
                ("01_inicio_base", new Vector3(4.6f, 0.005f, 4.05f), 90f),
                ("02_embaixo_sofa", new Vector3(3.0f, 0.005f, 2.95f), 0f),
                ("03_cozinha", new Vector3(7.5f, 0.005f, 4.2f), 0f),
                ("04_escada", new Vector3(6.9f, 0.005f, 1.0f), 90f),
                ("05_quarto", new Vector3(2.2f, 0.005f, 4.9f), 10f),
                ("06_mesa_cadeiras", new Vector3(7.5f, 0.005f, 6.0f), 200f),
            };
            foreach (var v in views)
            {
                robot.SetPositionAndRotation(v.pos, Quaternion.Euler(0, v.yaw, 0));
                Physics.SyncTransforms();
                follow.SnapToTarget();
                Render(cam, v.name);
            }

            // Vista de cima (planta baixa)
            cam.transform.SetPositionAndRotation(new Vector3(5f, 13f, 3.2f), Quaternion.Euler(75f, 0, 0));
            cam.fieldOfView = 50f;
            Render(cam, "00_vista_geral");

            EditorSceneManager.OpenScene(PoeirinhaBuilder.MenuScenePath);
            Render(Camera.main, "07_menu");
            Debug.Log("[Poeirinha] Screenshots salvos em " + Path.GetFullPath("Screenshots"));
        }

        static void Render(Camera cam, string name)
        {
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"Screenshots/{name}.png", tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
        }

        public static void CI_Capture()
        {
            try { Capture(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
