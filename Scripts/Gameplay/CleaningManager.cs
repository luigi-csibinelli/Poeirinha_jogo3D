using System;
using UnityEngine;

namespace Poeirinha
{
    /// <summary>
    /// Conta as manchas da fase e as já limpas. Avisa o progresso (para o HUD),
    /// marcos de 50%/75% e quando a meta de 90% é atingida (vitória).
    /// </summary>
    public class CleaningManager : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 1f)] float goalFraction = 0.9f;
        [Tooltip("Partículas de poeira emitidas onde a mancha foi aspirada.")]
        [SerializeField] ParticleSystem dustBurst;
        [SerializeField] int dustParticles = 14;

        public int Total { get; private set; }
        public int Collected { get; private set; }
        public float Fraction => Total > 0 ? (float)Collected / Total : 0f;
        public float GoalFraction => goalFraction;
        public int Required => Mathf.CeilToInt(Total * goalFraction);
        public DirtSpot[] Spots { get; private set; }

        /// <summary>(fração limpa 0..1, limpas, total)</summary>
        public event Action<float, int, int> OnProgressChanged;
        /// <summary>Texto curto para o HUD: "Metade!", "Quase lá!".</summary>
        public event Action<string> OnMilestone;
        public event Action<DirtSpot> OnDirtCollected;
        public event Action OnGoalReached;

        bool halfAnnounced, threeQuartersAnnounced, goalAnnounced;

        void Awake()
        {
            // Busca única no início da fase (nunca em Update).
            Spots = FindObjectsByType<DirtSpot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Total = Spots.Length;
            foreach (DirtSpot spot in Spots) spot.Bind(this);
        }

        public void NotifyCollected(DirtSpot spot)
        {
            if (!GameManager.Playing) return;

            Collected++;
            EmitDust(spot.transform.position);
            OnDirtCollected?.Invoke(spot);
            OnProgressChanged?.Invoke(Fraction, Collected, Total);

            if (!halfAnnounced && Fraction >= 0.5f)
            {
                halfAnnounced = true;
                OnMilestone?.Invoke("Metade!");
            }
            if (!threeQuartersAnnounced && Fraction >= 0.75f)
            {
                threeQuartersAnnounced = true;
                OnMilestone?.Invoke("Quase lá!");
            }
            if (!goalAnnounced && Collected >= Required)
            {
                goalAnnounced = true;
                OnGoalReached?.Invoke();
            }
        }

        void EmitDust(Vector3 position)
        {
            if (dustBurst == null) return;
            var emit = new ParticleSystem.EmitParams
            {
                position = position + Vector3.up * 0.03f,
                applyShapeToPosition = true
            };
            dustBurst.Emit(emit, dustParticles);
        }
    }
}
