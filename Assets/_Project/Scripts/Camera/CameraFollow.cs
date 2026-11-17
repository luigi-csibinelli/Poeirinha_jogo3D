using UnityEngine;
using UnityEngine.InputSystem;

namespace Poeirinha
{
    /// <summary>
    /// Câmera em 3ª pessoa atrás e acima do robô (GDD seção 4), com controle pelo mouse.
    ///
    /// 1. Por padrão fica atrás do robô e gira junto com ele, com um leve atraso (Slerp).
    /// 2. O MOUSE gira a câmera em volta do robô (horizontal e vertical) e a roda dá zoom.
    ///    Se "Recentrar câmera" estiver ligado nas Opções, ela volta para trás do robô
    ///    sozinha quando o mouse fica parado e o robô está andando.
    /// 3. Posição suavizada com SmoothDamp.
    /// 4. SphereCast do robô até a posição desejada: se bater numa parede, a câmera
    ///    se aproxima. Assim ela nunca entra em paredes (layer "Wall").
    /// Roda em LateUpdate: depois que a física e a interpolação já moveram o robô.
    ///
    /// Obs.: o movimento do robô continua "tanque" (relativo ao robô, não à câmera).
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;

        [Header("Enquadramento (equivale ao offset (0, 1.0, -1.3))")]
        [SerializeField] float defaultDistance = 1.64f;
        [SerializeField] float defaultPitch = 37.5f;       // graus para baixo
        [SerializeField] float lookAhead = 0.5f;
        [SerializeField] float lookHeight = 0.05f;

        [Header("Mouse")]
        [SerializeField] float degreesPerPixel = 0.12f;    // multiplicado pela sensibilidade das Opções
        [SerializeField] float minPitch = 8f;
        [SerializeField] float maxPitch = 70f;
        [SerializeField] float minZoom = 0.8f;
        [SerializeField] float maxZoom = 2.6f;
        [SerializeField] float zoomPerScrollNotch = 0.12f;
        [Tooltip("Segundos sem mexer o mouse antes de recentrar (se a opção estiver ligada).")]
        [SerializeField] float recenterDelay = 1.5f;
        [SerializeField] float recenterSpeed = 2f;

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
        public float YawOffset => yawOffset;

        Vector3 velocity;
        Quaternion followYaw;
        float yawOffset;      // giro extra dado pelo mouse (graus)
        float pitch;          // inclinação atual (graus)
        float distance;       // distância atual (zoom)
        float lastMouseTime = -100f;
        RobotController robot;

        void Start()
        {
            if (target != null) robot = target.GetComponent<RobotController>();
            SnapToTarget();
        }

        /// <summary>Coloca a câmera direto atrás do robô (sem suavizar e sem giro do mouse).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            yawOffset = 0f;
            pitch = defaultPitch;
            distance = defaultDistance;
            followYaw = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            transform.position = ResolveCollision(target.position + Orbit() * Vector3.back * distance);
            velocity = Vector3.zero;
            transform.rotation = Quaternion.LookRotation(LookPoint() - transform.position);
        }

        /// <summary>Usado pelos testes para simular o mouse.</summary>
        public void AddMouseInput(Vector2 pixels, float scrollNotches = 0f) => ApplyMouse(pixels, scrollNotches);

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            ReadMouse();
            Recenter(dt);

            // Só o yaw (giro em Y) do robô importa para a câmera.
            Quaternion targetYaw = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            // 1 - e^(-k·dt): suavização que não depende do frame rate.
            followYaw = Quaternion.Slerp(followYaw, targetYaw, 1f - Mathf.Exp(-rotationFollowSpeed * dt));

            Vector3 desired = target.position + Orbit() * Vector3.back * distance;
            Vector3 smoothed = Vector3.SmoothDamp(transform.position, desired, ref velocity, positionSmoothTime);

            // A colisão vem DEPOIS da suavização: mesmo atrasada, a câmera nunca entra na parede.
            transform.position = ResolveCollision(smoothed);
            transform.rotation = Quaternion.LookRotation(LookPoint() - transform.position);
        }

        void ReadMouse()
        {
            // Só durante o jogo (na pausa/fim o mouse é usado nos botões).
            if (!GameManager.Playing || Mouse.current == null) return;
            Vector2 delta = Mouse.current.delta.ReadValue();
            float scroll = Mouse.current.scroll.ReadValue().y / 120f; // 1 "clique" da roda ≈ 120
            ApplyMouse(delta, scroll);
        }

        void ApplyMouse(Vector2 pixels, float scrollNotches)
        {
            if (pixels.sqrMagnitude < 0.01f && Mathf.Abs(scrollNotches) < 0.01f) return;
            float k = degreesPerPixel * GameSettings.MouseSensitivity;
            float invert = GameSettings.InvertY ? -1f : 1f;
            yawOffset = Mathf.DeltaAngle(0f, yawOffset + pixels.x * k);
            pitch = Mathf.Clamp(pitch - pixels.y * k * invert, minPitch, maxPitch);
            distance = Mathf.Clamp(distance - scrollNotches * zoomPerScrollNotch, minZoom, maxZoom);
            if (pixels.sqrMagnitude >= 0.01f) lastMouseTime = Time.unscaledTime;
        }

        void Recenter(float dt)
        {
            if (!GameSettings.AutoRecenter) return;
            if (Time.unscaledTime - lastMouseTime < recenterDelay) return;
            if (robot != null && !robot.IsMoving) return; // parado: deixa o jogador olhar em volta
            float k = 1f - Mathf.Exp(-recenterSpeed * dt);
            yawOffset = Mathf.Lerp(yawOffset, 0f, k);
            pitch = Mathf.Lerp(pitch, defaultPitch, k);
        }

        /// <summary>Rotação da órbita: segue o robô + giro do mouse + inclinação.</summary>
        Quaternion Orbit() => followYaw * Quaternion.Euler(pitch, yawOffset, 0f);

        Vector3 LookPoint()
        {
            Quaternion yawOnly = followYaw * Quaternion.Euler(0f, yawOffset, 0f);
            return target.position + yawOnly * Vector3.forward * lookAhead + Vector3.up * lookHeight;
        }

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
