using UnityEngine;

namespace Poeirinha
{
    /// <summary>
    /// Câmera em 3ª pessoa atrás e acima do robô (GDD seção 4).
    ///
    /// 1. Gira junto com o robô, mas com um leve atraso (Slerp).
    /// 2. Posição suavizada com SmoothDamp.
    /// 3. SphereCast do robô até a posição desejada: se bater numa parede, a câmera
    ///    se aproxima. Assim ela nunca entra em paredes (layer "Wall").
    /// Roda em LateUpdate: depois que a física e a interpolação já moveram o robô.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;

        [Header("Enquadramento")]
        [SerializeField] Vector3 offset = new Vector3(0f, 1.0f, -1.3f);
        [SerializeField] float lookAhead = 0.5f;
        [SerializeField] float lookHeight = 0.05f;

        [Header("Suavização")]
        [SerializeField] float positionSmoothTime = 0.15f;
        [Tooltip("Quanto maior, mais rápido a câmera acompanha o giro do robô.")]
        [SerializeField] float rotationFollowSpeed = 4f;

        [Header("Colisão com paredes")]
        [SerializeField] LayerMask collisionMask;
        [SerializeField] float collisionRadius = 0.12f;
        [SerializeField] float pivotHeight = 0.25f;
        [SerializeField] float minDistance = 0.15f;

        public Transform Target => target;

        Vector3 velocity;
        Quaternion followYaw;

        void Start()
        {
            SnapToTarget();
        }

        /// <summary>Coloca a câmera direto na posição final (sem suavizar).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            followYaw = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            transform.position = ResolveCollision(target.position + followYaw * offset);
            velocity = Vector3.zero;
            transform.rotation = Quaternion.LookRotation(LookPoint() - transform.position);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            // Só o yaw (giro em Y) do robô importa para a câmera.
            Quaternion targetYaw = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            // 1 - e^(-k·dt): suavização que não depende do frame rate.
            followYaw = Quaternion.Slerp(followYaw, targetYaw, 1f - Mathf.Exp(-rotationFollowSpeed * dt));

            Vector3 desired = target.position + followYaw * offset;
            Vector3 smoothed = Vector3.SmoothDamp(transform.position, desired, ref velocity, positionSmoothTime);

            // A colisão vem DEPOIS da suavização: mesmo atrasada, a câmera nunca entra na parede.
            transform.position = ResolveCollision(smoothed);
            transform.rotation = Quaternion.LookRotation(LookPoint() - transform.position);
        }

        Vector3 LookPoint() => target.position + followYaw * Vector3.forward * lookAhead + Vector3.up * lookHeight;

        Vector3 ResolveCollision(Vector3 wanted)
        {
            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            Vector3 dir = wanted - pivot;
            float dist = dir.magnitude;
            if (dist < 0.0001f) return wanted;
            dir /= dist;

            if (Physics.SphereCast(pivot, collisionRadius, dir, out RaycastHit hit, dist, collisionMask, QueryTriggerInteraction.Ignore))
            {
                // Para logo antes do ponto de impacto (o raio da esfera já dá a folga).
                return pivot + dir * Mathf.Max(hit.distance, minDistance);
            }
            return wanted;
        }
    }
}
