using TMPro;
using UnityEngine;

namespace Poeirinha
{
    /// <summary>
    /// HUD do jogo. Só mostra dados: lê valores e escuta eventos, nunca muda regra de jogo.
    ///  - Topo-esquerda: % de limpeza
    ///  - Topo-direita: timer
    ///  - Base-esquerda: bateria
    ///  - Centro: avisos (borda, meia, bateria fraca) e mensagens rápidas ("Metade!")
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Sistemas (só leitura)")]
        [SerializeField] BatterySystem battery;
        [SerializeField] CleaningManager cleaning;
        [SerializeField] LevelTimer timer;
        [SerializeField] CliffSensor cliffSensor;
        [SerializeField] RobotController robot;

        [Header("Limpeza")]
        [SerializeField] RectTransform cleanFill;
        [SerializeField] TMP_Text cleanText;

        [Header("Timer")]
        [SerializeField] TMP_Text timerText;
        [SerializeField] Color timerNormal = Color.white;
        [SerializeField] Color timerWarning = new Color(1f, 0.35f, 0.3f);

        [Header("Bateria")]
        [SerializeField] RectTransform batteryFill;
        [SerializeField] UnityEngine.UI.Image batteryFillImage;
        [SerializeField] TMP_Text batteryText;
        [SerializeField] GameObject chargingLabel;
        [SerializeField] Color batteryHigh = new Color(0.35f, 0.9f, 0.55f);
        [SerializeField] Color batteryMid = new Color(1f, 0.82f, 0.3f);
        [SerializeField] Color batteryLow = new Color(1f, 0.3f, 0.25f);

        [Header("Centro")]
        [SerializeField] GameObject warningRoot;
        [SerializeField] TMP_Text warningText;
        [SerializeField] TMP_Text toastText;
        [SerializeField] float toastDuration = 1.8f;

        [Header("Raiz do HUD (escondida no fim de jogo)")]
        [SerializeField] CanvasGroup hudGroup;

        float toastTimer;

        void Start()
        {
            // Lê o estado atual uma vez (os sistemas podem ter disparado eventos antes de nós).
            if (cleaning != null) ShowCleaning(cleaning.Fraction, cleaning.Collected, cleaning.Total);
            if (timer != null) ShowTime(timer.Remaining);
            if (battery != null) ShowBattery(battery.Normalized);
            if (toastText != null) toastText.gameObject.SetActive(false);

            if (cleaning != null)
            {
                cleaning.OnProgressChanged += ShowCleaning;
                cleaning.OnMilestone += ShowToast;
            }
            if (timer != null)
            {
                timer.OnTimeChanged += ShowTime;
                timer.OnFinalCountdownStarted += HandleFinalCountdown;
            }
            if (battery != null) battery.OnBatteryChanged += ShowBattery;
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += HandleState;
        }

        void OnDestroy()
        {
            if (cleaning != null)
            {
                cleaning.OnProgressChanged -= ShowCleaning;
                cleaning.OnMilestone -= ShowToast;
            }
            if (timer != null)
            {
                timer.OnTimeChanged -= ShowTime;
                timer.OnFinalCountdownStarted -= HandleFinalCountdown;
            }
            if (battery != null) battery.OnBatteryChanged -= ShowBattery;
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleState;
        }

        void Update()
        {
            UpdateWarning();

            if (toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
                if (toastTimer <= 0f && toastText != null) toastText.gameObject.SetActive(false);
            }
        }

        void ShowCleaning(float fraction, int collected, int total)
        {
            if (cleanFill != null) cleanFill.anchorMax = new Vector2(fraction, 1f);
            if (cleanText != null) cleanText.text = $"{Mathf.FloorToInt(fraction * 100f)}%";
        }

        void ShowTime(float remaining)
        {
            if (timerText == null) return;
            int secs = Mathf.CeilToInt(remaining);
            timerText.text = $"{secs / 60}:{secs % 60:00}";
            timerText.color = timer != null && timer.InFinalCountdown ? timerWarning : timerNormal;
        }

        void ShowBattery(float normalized)
        {
            if (batteryFill != null) batteryFill.anchorMax = new Vector2(normalized, 1f);
            if (batteryText != null) batteryText.text = $"{Mathf.CeilToInt(normalized * 100f)}%";
            if (batteryFillImage != null)
                batteryFillImage.color = normalized < 0.25f ? batteryLow : normalized < 0.5f ? batteryMid : batteryHigh;
            if (chargingLabel != null) chargingLabel.SetActive(battery != null && battery.IsCharging);
        }

        void ShowToast(string message)
        {
            if (toastText == null) return;
            toastText.text = message;
            toastText.gameObject.SetActive(true);
            toastTimer = toastDuration;
        }

        void HandleFinalCountdown() => ShowToast("O dono está chegando!");

        /// <summary>Um aviso por vez, pelo mais urgente.</summary>
        void UpdateWarning()
        {
            if (warningRoot == null || warningText == null) return;

            string msg = null;
            if (GameManager.Playing)
            {
                if (robot != null && robot.IsStuck) msg = $"Meia enroscada! Aperte <b>E</b> ({robot.PressesLeft})";
                else if (cliffSensor != null && cliffSensor.EdgeDetected) msg = "<b>!</b>  Cuidado: escada!";
                else if (battery != null && battery.IsLow && !battery.IsCharging) msg = "Bateria fraca! Volte à base";
            }

            bool show = msg != null;
            if (warningRoot.activeSelf != show) warningRoot.SetActive(show);
            if (!show) return;

            warningText.text = msg;
            // Pulsa para chamar atenção.
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f);
            warningText.alpha = pulse;
        }

        void HandleState(GameState state)
        {
            if (hudGroup == null) return;
            bool ended = state == GameState.Victory || state == GameState.Defeat;
            hudGroup.alpha = ended ? 0f : 1f;
        }
    }
}
