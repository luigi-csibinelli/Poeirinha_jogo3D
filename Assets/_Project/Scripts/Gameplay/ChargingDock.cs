using System;
using UnityEngine;

namespace Poeirinha
{
    /// <summary>
    /// Base de recarga: um trigger no chão. Enquanto o robô está dentro, a bateria sobe.
    /// </summary>
    public class ChargingDock : MonoBehaviour
    {
        [SerializeField] ParticleSystem chargeParticles;
        [SerializeField] Light dockLight;
        [SerializeField] Renderer statusRenderer;
        [SerializeField] Color idleColor = new Color(0.4f, 0.5f, 0.6f);
        [SerializeField] Color chargingColor = new Color(0.25f, 0.6f, 1f);

        public bool RobotInside { get; private set; }
        public event Action<bool> OnDockedChanged;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        Material statusMaterial;

        void Awake()
        {
            if (statusRenderer != null) statusMaterial = statusRenderer.material;
            SetFeedback(false);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!RobotDetector.TryGetRobot(other, out RobotController robot)) return;
            if (robot.TryGetComponent(out BatterySystem battery)) battery.SetCharging(true);
            RobotInside = true;
            SetFeedback(true);
            OnDockedChanged?.Invoke(true);
        }

        void OnTriggerExit(Collider other)
        {
            if (!RobotDetector.TryGetRobot(other, out RobotController robot)) return;
            if (robot.TryGetComponent(out BatterySystem battery)) battery.SetCharging(false);
            RobotInside = false;
            SetFeedback(false);
            OnDockedChanged?.Invoke(false);
        }

        void SetFeedback(bool charging)
        {
            if (dockLight != null) dockLight.enabled = charging;
            if (chargeParticles != null)
            {
                if (charging) chargeParticles.Play();
                else chargeParticles.Stop();
            }
            if (statusMaterial != null)
            {
                Color c = charging ? chargingColor : idleColor;
                statusMaterial.SetColor(BaseColorId, c);
                statusMaterial.SetColor(EmissionColorId, c * (charging ? 2.5f : 0.3f));
            }
        }
    }
}
