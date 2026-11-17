using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poeirinha
{
    /// <summary>
    /// Ponto único para trocar de cena. Sempre devolve o tempo e o áudio ao normal
    /// antes de carregar, para nunca começar uma cena "congelada" (Time.timeScale = 0).
    /// </summary>
    public static class SceneLoader
    {
        public const string MainMenu = "MainMenu";
        public const string Level01 = "Level01";

        public static void Load(string sceneName)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(sceneName);
        }

        public static void ReloadCurrent() => Load(SceneManager.GetActiveScene().name);

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
