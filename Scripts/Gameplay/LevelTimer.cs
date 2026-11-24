using System;
using UnityEngine;

namespace Poeirinha
{
    /// <summary>Contagem regressiva da fase: "o dono está voltando".</summary>
    public class LevelTimer : MonoBehaviour
    {
        [SerializeField] float duration = 180f;
        [Tooltip("Abaixo deste tempo o timer fica vermelho e toca o som da porta.")]
        [SerializeField] float finalWarningAt = 30f;

        public float Duration => duration;
        public float Remaining { get; private set; }
        public bool InFinalCountdown => Remaining <= finalWarningAt;

        public event Action<float> OnTimeChanged;
        public event Action OnFinalCountdownStarted;
        public event Action OnTimeUp;

        bool warned;
        bool finished;

        void Awake()
        {
            Remaining = duration;
        }

        void Update()
        {
            if (finished || !GameManager.Playing) return;

            // Time.deltaTime já vale 0 durante a pausa (timeScale = 0).
            Remaining = Mathf.Max(0f, Remaining - Time.deltaTime);
            OnTimeChanged?.Invoke(Remaining);

            if (!warned && Remaining <= finalWarningAt)
            {
                warned = true;
                OnFinalCountdownStarted?.Invoke();
            }

            if (Remaining <= 0f)
            {
                finished = true;
                OnTimeUp?.Invoke();
            }
        }

        /// <summary>Ferramenta de teste/debug: muda o tempo restante.</summary>
        public void SetRemaining(float seconds)
        {
            Remaining = Mathf.Clamp(seconds, 0f, duration);
            OnTimeChanged?.Invoke(Remaining);
        }
    }
}
