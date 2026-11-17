using System;
using UnityEngine;

namespace Poeirinha
{
    /// <summary>
    /// Sensor de queda: um Raycast para baixo, um pouco à frente do robô.
    /// Se não há chão perto, avisa (bip + ícone no HUD). Ele NÃO freia o robô:
    /// evitar a queda é trabalho do jogador (GDD seção 5).
    /// </summary>
    public class CliffSensor : MonoBehaviour
    {
        [SerializeField] float forwardOffset = 0.25f;  // m à frente do centro do robô
        [SerializeField] float maxDrop = 0.2f;         // "chão a menos de 0.2 m"
        [SerializeField] float rayStartHeight = 0.05f;
        [Tooltip("O que conta como chão. Deixe o layer Player de fora.")]
        [SerializeField] LayerMask groundMask = ~0;

        public bool EdgeDetected { get; private set; }
        public event Action<bool> OnEdgeWarning;

        Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            Vector3 pos = rb != null ? rb.position : transform.position;
            Quaternion rot = rb != null ? rb.rotation : transform.rotation;
            Vector3 origin = pos + rot * Vector3.forward * forwardOffset + Vector3.up * rayStartHeight;

            // QueryTriggerInteraction.Ignore: sujeira, base etc. não contam como chão.
            bool hasGround = Physics.Raycast(origin, Vector3.down, rayStartHeight + maxDrop,
                groundMask, QueryTriggerInteraction.Ignore);

            bool edge = !hasGround && GameManager.Playing;
            if (edge != EdgeDetected)
            {
                EdgeDetected = edge;
                OnEdgeWarning?.Invoke(edge);
            }
        }

        void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position + transform.forward * forwardOffset + Vector3.up * rayStartHeight;
            Gizmos.color = EdgeDetected ? Color.red : Color.green;
            Gizmos.DrawLine(origin, origin + Vector3.down * (rayStartHeight + maxDrop));
        }
    }
}
