using System.Collections.Generic;
using UnityEngine;

namespace Poeirinha
{
    /// <summary>
    /// Fica na câmera. A cada frame, lança uma esfera da câmera até o robô e deixa
    /// semitransparentes os móveis (layer "Occluder") que estiverem no meio.
    /// </summary>
    [DefaultExecutionOrder(60)] // depois do CameraFollow (50)
    public class OcclusionFader : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] LayerMask occluderMask;
        [SerializeField] float radius = 0.08f;
        [SerializeField] float targetHeight = 0.05f;

        readonly RaycastHit[] hits = new RaycastHit[16];
        readonly Collider[] overlaps = new Collider[8];
        HashSet<FadeableObject> current = new HashSet<FadeableObject>();
        HashSet<FadeableObject> previous = new HashSet<FadeableObject>();

        void LateUpdate()
        {
            if (target == null) return;

            // Troca os conjuntos sem alocar memória nova a cada frame.
            (previous, current) = (current, previous);
            current.Clear();

            Vector3 from = transform.position;
            Vector3 to = target.position + Vector3.up * targetHeight;
            Vector3 dir = to - from;
            float dist = dir.magnitude;

            if (dist > 0.001f)
            {
                int n = Physics.SphereCastNonAlloc(from, radius, dir / dist, hits, dist, occluderMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++) AddFadeable(hits[i].collider);
            }

            // O SphereCast não detecta colliders que já envolvem o ponto de partida,
            // então checamos também o que está encostado na própria câmera.
            int m = Physics.OverlapSphereNonAlloc(from, radius, overlaps, occluderMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < m; i++) AddFadeable(overlaps[i]);

            foreach (FadeableObject f in previous)
                if (f != null && !current.Contains(f)) f.SetOccluding(false);
            foreach (FadeableObject f in current)
                f.SetOccluding(true);
        }

        void AddFadeable(Collider col)
        {
            FadeableObject f = col.GetComponentInParent<FadeableObject>();
            if (f != null) current.Add(f);
        }
    }
}
