using System.Collections;
using UnityEngine;

namespace Poeirinha
{
    /// <summary>
    /// Só visual: LED de bateria (verde → amarelo → vermelho piscando, azul carregando),
    /// escovas laterais girando e um "tremidinho" quando o jogador aperta E preso na meia.
    /// </summary>
    public class RobotVisuals : MonoBehaviour
    {
        [SerializeField] RobotController robot;
        [SerializeField] BatterySystem battery;
        [SerializeField] Renderer ledRenderer;
        [SerializeField] Light ledLight;
        [SerializeField] Transform[] brushes;
        [SerializeField] Transform bodyRoot;

        [Header("Cores do LED")]
        [SerializeField] Color fullColor = new Color(0.25f, 1f, 0.45f);
        [SerializeField] Color midColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] Color lowColor = new Color(1f, 0.2f, 0.15f);
        [SerializeField] Color chargingColor = new Color(0.25f, 0.6f, 1f);
        [SerializeField] float emissionIntensity = 3f;
        [SerializeField] float blinkPerSecond = 3f;

        [Header("Escovas")]
        [SerializeField] float brushIdleSpeed = 240f;
        [SerializeField] float brushMovingSpeed = 900f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        Material ledMaterial;
        Vector3 bodyRestPos;

        void Awake()
        {
            // .material cria uma cópia só deste robô (não altera o asset).
            if (ledRenderer != null) ledMaterial = ledRenderer.material;
            if (bodyRoot != null) bodyRestPos = bodyRoot.localPosition;
        }

        void OnEnable()
        {
            if (robot != null) robot.OnUnstickPress += HandleUnstickPress;
        }

        void OnDisable()
        {
            if (robot != null) robot.OnUnstickPress -= HandleUnstickPress;
        }

        void Update()
        {
            UpdateLed();
            UpdateBrushes();
        }

        void UpdateLed()
        {
            if (ledMaterial == null || battery == null) return;

            Color c;
            float n = battery.Normalized;
            if (battery.IsCharging)
            {
                // Azul "respirando" enquanto carrega.
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 4f);
                c = chargingColor * pulse;
            }
            else if (battery.IsLow)
            {
                // Pisca: liga/desliga usando a parte inteira do tempo.
                bool on = Mathf.FloorToInt(Time.time * blinkPerSecond * 2f) % 2 == 0;
                c = on ? lowColor : lowColor * 0.08f;
            }
            else
            {
                // 25%..60%: amarelo → verde. Acima de 60%: verde.
                float t = Mathf.InverseLerp(0.25f, 0.6f, n);
                c = Color.Lerp(midColor, fullColor, t);
            }

            ledMaterial.SetColor(BaseColorId, c);
            ledMaterial.SetColor(EmissionColorId, c * emissionIntensity);
            if (ledLight != null) ledLight.color = c;
        }

        void UpdateBrushes()
        {
            if (brushes == null) return;
            bool active = GameManager.Playing || GameManager.Instance == null;
            float speed = !active ? 0f
                : Mathf.Lerp(brushIdleSpeed, brushMovingSpeed, robot != null ? robot.Speed01 : 0f);
            for (int i = 0; i < brushes.Length; i++)
            {
                if (brushes[i] == null) continue;
                // As duas escovas giram em sentidos opostos, como num robô de verdade.
                float dir = i % 2 == 0 ? 1f : -1f;
                brushes[i].Rotate(0f, dir * speed * Time.deltaTime, 0f, Space.Self);
            }
        }

        void HandleUnstickPress(int pressesLeft)
        {
            if (bodyRoot != null) StartCoroutine(Shake());
        }

        IEnumerator Shake()
        {
            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                bodyRoot.localPosition = bodyRestPos + (Vector3)Random.insideUnitCircle * 0.012f;
                yield return null;
            }
            bodyRoot.localPosition = bodyRestPos;
        }
    }
}
