using UnityEngine;
using UnityEngine.EventSystems;

namespace Poeirinha
{
    /// <summary>Menu principal: Jogar, Como jogar, Sair.</summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] GameObject mainPanel;
        [SerializeField] GameObject howToPanel;
        [SerializeField] GameObject firstSelectedMain;
        [SerializeField] GameObject firstSelectedHowTo;

        void Start()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            ShowMain();
        }

        public void OnPlayClicked() => SceneLoader.Load(SceneLoader.Level01);
        public void OnHowToClicked() => ShowPanel(howToPanel, firstSelectedHowTo);
        public void OnBackClicked() => ShowMain();
        public void OnQuitClicked() => SceneLoader.Quit();

        void ShowMain() => ShowPanel(mainPanel, firstSelectedMain);

        void ShowPanel(GameObject panel, GameObject selected)
        {
            mainPanel.SetActive(panel == mainPanel);
            howToPanel.SetActive(panel == howToPanel);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(selected);
        }
    }
}
