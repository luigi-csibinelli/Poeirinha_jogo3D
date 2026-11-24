using System.Collections;
using UnityEngine;

namespace Poeirinha
{
    /// <summary>
    /// Uma mancha de sujeira no chão. Quando o robô encosta (trigger), ela avisa o
    /// CleaningManager, encolhe rapidinho (0.2 s) e se desativa.
    /// </summary>
    public class DirtSpot : MonoBehaviour
    {
        [SerializeField] float fadeDuration = 0.2f;
        [Tooltip("Só informativo: mancha escondida embaixo de um móvel.")]
        [SerializeField] bool underFurniture;

        public bool IsCollected { get; private set; }
        public bool UnderFurniture => underFurniture;

        CleaningManager manager;

        /// <summary>Chamado pelo CleaningManager quando ele registra a mancha.</summary>
        public void Bind(CleaningManager owner) => manager = owner;

        void OnTriggerEnter(Collider other)
        {
            if (IsCollected || !GameManager.Playing) return;
            if (!RobotDetector.TryGetRobot(other, out _)) return; // o gato não limpa nada
            Collect();
        }

        public void Collect()
        {
            if (IsCollected) return;
            IsCollected = true;
            if (TryGetComponent(out Collider col)) col.enabled = false;
            if (manager != null) manager.NotifyCollected(this);
            StartCoroutine(FadeOut());
        }

        IEnumerator FadeOut()
        {
            Vector3 start = transform.localScale;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(start, Vector3.zero, t / fadeDuration);
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
