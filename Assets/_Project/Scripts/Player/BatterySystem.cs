using System;
using UnityEngine;

namespace Poeirinha
{
    /// <summary>
    /// Bateria do robô (GDD seção 6). Consome andando/parado, gasta o dobro no turbo,
    /// recarrega na base e avisa quando está fraca ou acabou.
    /// </summary>
    public class BatterySystem : MonoBehaviour
    {
        [SerializeField] RobotController robot;

        [Header("Valores (por segundo)")]
        [SerializeField] float maxCharge = 100f;
        [SerializeField] float drainMoving = 1.5f;
        [SerializeField] float drainIdle = 0.5f;
        [SerializeField] float turboDrainMultiplier = 2f;
        [SerializeField] float chargeRate = 25f;

        [Header("Bateria fraca")]
        [SerializeField, Range(0f, 1f)] float lowThreshold = 0.25f;

        public float Charge { get; private set; }
        public float MaxCharge => maxCharge;
        public float Normalized => Charge / maxCharge;
        public bool IsCharging { get; private set; }
        public bool IsLow => Normalized < lowThreshold;
        public bool IsEmpty => Charge <= 0f;

        /// <summary>Valor normalizado de 0 a 1.</summary>
        public event Action<float> OnBatteryChanged;
        public event Action OnBatteryEmpty;
        public event Action<bool> OnLowBatteryChanged;
        public event Action<bool> OnChargingChanged;
        /// <summary>Dano instantâneo recebido (ex.: gato).</summary>
        public event Action<float> OnHit;

        bool wasLow;
        bool emptyFired;

        void Awake()
        {
            Charge = maxCharge;
            if (robot == null) robot = GetComponent<RobotController>();
        }

        void Update()
        {
            if (!GameManager.Playing) return;

            float dt = Time.deltaTime;
            if (IsCharging)
            {
                Charge = Mathf.Min(maxCharge, Charge + chargeRate * dt);
            }
            else
            {
                float drain = robot != null && robot.IsMoving ? drainMoving : drainIdle;
                if (robot != null && robot.IsTurbo) drain *= turboDrainMultiplier;
                Charge = Mathf.Max(0f, Charge - drain * dt);
            }

            Notify();
        }

        /// <summary>Chamado pela base de recarga (ChargingDock).</summary>
        public void SetCharging(bool charging)
        {
            if (IsCharging == charging) return;
            IsCharging = charging;
            OnChargingChanged?.Invoke(charging);
        }

        /// <summary>Perda instantânea (encostar no gato).</summary>
        public void TakeHit(float amount)
        {
            if (!GameManager.Playing) return;
            Charge = Mathf.Max(0f, Charge - amount);
            OnHit?.Invoke(amount);
            Notify();
        }

        /// <summary>Ferramenta de teste/debug.</summary>
        public void SetCharge(float value)
        {
            Charge = Mathf.Clamp(value, 0f, maxCharge);
            Notify();
        }

        void Notify()
        {
            OnBatteryChanged?.Invoke(Normalized);

            bool low = IsLow;
            if (low != wasLow)
            {
                wasLow = low;
                OnLowBatteryChanged?.Invoke(low);
            }

            // A bateria só "acaba" fora da base (dentro dela ela está subindo).
            if (!emptyFired && Charge <= 0f && !IsCharging)
            {
                emptyFired = true;
                OnBatteryEmpty?.Invoke();
            }
        }
    }
}
