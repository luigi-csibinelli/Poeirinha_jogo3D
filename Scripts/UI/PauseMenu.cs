using UnityEngine;
using UnityEngine.EventSystems;

namespace Poeirinha
{
    /// <summary>Tela de pausa: Continuar, Reiniciar, Menu. Os botões chamam o GameManager.</summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] GameObject firstSelected;
        [SerializeField] GameObject card;
        [SerializeField] GameObject optionsButton;
        [SerializeField] OptionsMenu options;

        void Start()
        {
            panel.SetActive(false);
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += HandleState;
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleState;
        }

        void HandleState(GameState state)
        {
            bool paused = state == GameState.Paused;
            panel.SetActive(paused);
            if (card != null) card.SetActive(true);
            if (!paused && options != null) options.HideImmediate(); // Esc com as opções abertas
            // Seleciona um botão para dar para navegar só com o teclado.
            if (paused && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(firstSelected);
        }

        // Ligados aos botões no Inspector (OnClick).
        public void OnContinueClicked() => GameManager.Instance?.Resume();
        public void OnRestartClicked() => GameManager.Instance?.Restart();
        public void OnMenuClicked() => GameManager.Instance?.GoToMenu();
        public void OnOptionsClicked() { if (options != null) options.Open(card, optionsButton); }
    }
}
