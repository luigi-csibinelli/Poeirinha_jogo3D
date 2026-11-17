using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Poeirinha
{
    /// <summary>
    /// Telas de vitória e derrota. Aparecem com um pequeno atraso (na queda da escada,
    /// a câmera acompanha o robô caindo antes da tela surgir).
    /// </summary>
    public class EndScreen : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text subtitleText;
        [SerializeField] TMP_Text detailText;
        [SerializeField] GameObject starsRoot;
        [SerializeField] Image[] stars;
        [SerializeField] Color starOn = new Color(1f, 0.82f, 0.25f);
        [SerializeField] Color starOff = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] TMP_Text primaryButtonLabel;
        [SerializeField] GameObject firstSelected;

        [Header("Atrasos (segundos reais)")]
        [SerializeField] float delay = 0.6f;
        [SerializeField] float fallDelay = 1.1f;

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
            if (state != GameState.Victory && state != GameState.Defeat) return;
            GameManager gm = GameManager.Instance;
            bool fell = state == GameState.Defeat && gm.LastDefeatReason == DefeatReason.FellDownStairs;
            StartCoroutine(ShowAfter(fell ? fallDelay : delay, state));
        }

        IEnumerator ShowAfter(float seconds, GameState state)
        {
            yield return new WaitForSecondsRealtime(seconds);
            GameManager gm = GameManager.Instance;
            if (gm == null) yield break;

            float cleaned = gm.Cleaning != null ? gm.Cleaning.Fraction : 0f;
            float remaining = gm.Timer != null ? gm.Timer.Remaining : 0f;

            if (state == GameState.Victory)
            {
                titleText.text = "Casa limpa!";
                subtitleText.text = "Você terminou antes do dono voltar.";
                int secs = Mathf.CeilToInt(remaining);
                detailText.text = $"Tempo restante: {secs / 60}:{secs % 60:00}   ·   Limpeza: {Mathf.FloorToInt(cleaned * 100f)}%";
                starsRoot.SetActive(true);
                int count = gm.CalculateStars();
                for (int i = 0; i < stars.Length; i++) stars[i].color = i < count ? starOn : starOff;
                primaryButtonLabel.text = "Jogar de novo";
            }
            else
            {
                titleText.text = "Ops!";
                subtitleText.text = ReasonText(gm.LastDefeatReason);
                detailText.text = $"Você chegou a limpar {Mathf.FloorToInt(cleaned * 100f)}% da casa.";
                starsRoot.SetActive(false);
                primaryButtonLabel.text = "Tentar de novo";
            }

            panel.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(firstSelected);
        }

        static string ReasonText(DefeatReason reason)
        {
            switch (reason)
            {
                case DefeatReason.BatteryEmpty: return "A bateria acabou!";
                case DefeatReason.FellDownStairs: return "Caiu da escada!";
                case DefeatReason.TimeUp: return "O dono chegou e a casa ainda está suja!";
                default: return "Fim de jogo";
            }
        }

        // Ligados aos botões no Inspector (OnClick).
        public void OnRetryClicked() => GameManager.Instance?.Restart();
        public void OnMenuClicked() => GameManager.Instance?.GoToMenu();
    }
}
