using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Poeirinha
{
    /// <summary>
    /// Movimento "tanque" do robô: W/S anda para frente/ré (relativo ao robô),
    /// A/D gira no próprio eixo, Shift é turbo.
    ///
    /// Física (GDD seção 5):
    /// - Input lido em Update, física aplicada em FixedUpdate.
    /// - Em vez de AddForce, escrevemos a velocidade desejada direto em
    ///   Rigidbody.linearVelocity. Assim o controle é preciso e a física continua
    ///   resolvendo as colisões (o robô desliza nas paredes em vez de atravessar).
    /// - A aceleração é suavizada com MoveTowards para não arrancar/parar seco.
    /// - Empurrões (gato) entram numa velocidade extra que decai com o tempo.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RobotController : MonoBehaviour
    {
        [Header("Velocidade")]
        [SerializeField] float maxSpeed = 2f;            // m/s
        [SerializeField] float turboMultiplier = 1.6f;   // 2.0 × 1.6 = 3.2 m/s
        [SerializeField] float turnSpeed = 180f;         // graus/s
        [SerializeField] float acceleration = 5f;        // m/s²
        [SerializeField] float deceleration = 7f;        // m/s² (freia um pouco mais rápido que acelera)
        [SerializeField] float turnAcceleration = 900f;  // graus/s²

        [Header("Empurrão")]
        [Tooltip("Quão rápido o empurrão do gato perde força (por segundo).")]
        [SerializeField] float knockbackDamping = 5f;

        Rigidbody rb;
        InputAction moveAction, turboAction, unstickAction;

        // Input do frame atual (lido em Update, usado em FixedUpdate)
        float throttleInput, turnInput;
        bool turboInput;

        // Estado do movimento
        float currentSpeed;      // m/s, positivo = frente
        float currentTurnSpeed;  // graus/s
        Vector3 knockbackVelocity;

        // Meia enroscada
        SockTrap stuckIn;
        int pressesLeft;

        // Input externo (usado pelos testes automáticos)
        bool useExternalInput;
        float extThrottle, extTurn;
        bool extTurbo;

        public float CurrentSpeed => currentSpeed;
        public float MaxSpeed => maxSpeed * turboMultiplier;
        /// <summary>0 a 1 — usado pelo som do motor e pelas escovas.</summary>
        public float Speed01 => Mathf.Clamp01(Mathf.Abs(currentSpeed) / MaxSpeed);
        public bool IsMoving => Mathf.Abs(currentSpeed) > 0.05f || Mathf.Abs(currentTurnSpeed) > 10f;
        public bool IsTurbo => turboInput && IsMoving && !IsStuck;
        public bool IsStuck => stuckIn != null;
        public int PressesLeft => pressesLeft;
        public Rigidbody Body => rb;

        /// <summary>(está preso?, apertos restantes)</summary>
        public event Action<bool, int> OnStuckChanged;
        /// <summary>Cada aperto de E enquanto preso (apertos restantes).</summary>
        public event Action<int> OnUnstickPress;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();

            // Ações criadas por código: fica tudo visível neste arquivo.
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            turboAction = new InputAction("Turbo", InputActionType.Button, "<Keyboard>/leftShift");
            unstickAction = new InputAction("Unstick", InputActionType.Button, "<Keyboard>/e");
        }

        void OnEnable()
        {
            moveAction.Enable();
            turboAction.Enable();
            unstickAction.Enable();
        }

        void OnDisable()
        {
            moveAction.Disable();
            turboAction.Disable();
            unstickAction.Disable();
        }

        void OnDestroy()
        {
            moveAction?.Dispose();
            turboAction?.Dispose();
            unstickAction?.Dispose();
        }

        void Update()
        {
            if (!GameManager.Playing)
            {
                throttleInput = turnInput = 0f;
                turboInput = false;
                return;
            }

            if (useExternalInput)
            {
                throttleInput = extThrottle;
                turnInput = extTurn;
                turboInput = extTurbo;
            }
            else
            {
                Vector2 move = moveAction.ReadValue<Vector2>();
                throttleInput = move.y;
                turnInput = move.x;
                turboInput = turboAction.IsPressed();
            }

            if (IsStuck && unstickAction.WasPressedThisFrame()) PressUnstick();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            GameState state = GameManager.Instance != null ? GameManager.Instance.State : GameState.Playing;
            bool gameOver = state == GameState.Victory || state == GameState.Defeat;

            if (gameOver)
            {
                // Fim de jogo: o robô para na hora. A gravidade continua (queda na escada).
                currentSpeed = 0f;
                currentTurnSpeed = 0f;
                knockbackVelocity = Vector3.zero;
            }
            else
            {
                float targetSpeed = throttleInput * maxSpeed * (turboInput ? turboMultiplier : 1f);
                float targetTurn = turnInput * turnSpeed;
                if (IsStuck) targetSpeed = targetTurn = 0f;

                // Acelera devagar e freia um pouco mais rápido: sensação de robô de verdade.
                float rate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * dt);
                currentTurnSpeed = Mathf.MoveTowards(currentTurnSpeed, targetTurn, turnAcceleration * dt);

                // Empurrão decai exponencialmente (independe do frame rate).
                knockbackVelocity *= Mathf.Exp(-knockbackDamping * dt);
            }

            // Usamos rb.rotation (e não transform.forward) porque com Interpolate o
            // transform mostra uma posição "suavizada", não a real da física.
            Vector3 forward = rb.rotation * Vector3.forward;
            Vector3 planar = forward * currentSpeed + knockbackVelocity;

            // Mantém o Y da física: o robô cai normalmente se sair do chão.
            rb.linearVelocity = new Vector3(planar.x, rb.linearVelocity.y, planar.z);
            // Giro só no eixo Y (X e Z estão congelados no Rigidbody).
            rb.angularVelocity = new Vector3(0f, currentTurnSpeed * Mathf.Deg2Rad, 0f);
        }

        // ---------- Empurrão (gato) ----------

        /// <summary>Equivalente a AddForce(impulse, ForceMode.Impulse), mas que sobrevive ao controle de velocidade.</summary>
        public void ApplyKnockback(Vector3 impulse)
        {
            impulse.y = 0f;
            knockbackVelocity += impulse / rb.mass;
            currentSpeed *= 0.2f; // perde o embalo
        }

        // ---------- Meia enroscada ----------

        public void GetStuck(SockTrap trap, int presses)
        {
            if (IsStuck || !GameManager.Playing) return;
            stuckIn = trap;
            pressesLeft = Mathf.Max(1, presses);
            currentSpeed = 0f;
            currentTurnSpeed = 0f;
            knockbackVelocity = Vector3.zero;
            OnStuckChanged?.Invoke(true, pressesLeft);
        }

        /// <summary>Um aperto de E. Público para os testes conseguirem simular.</summary>
        public void PressUnstick()
        {
            if (!IsStuck) return;
            pressesLeft--;
            OnUnstickPress?.Invoke(pressesLeft);
            if (pressesLeft <= 0)
            {
                SockTrap trap = stuckIn;
                stuckIn = null;
                trap.OnRobotFreed(this);
                OnStuckChanged?.Invoke(false, 0);
            }
        }

        // ---------- Ferramentas de teste / debug ----------

        public void SetExternalInput(float throttle, float turn, bool turbo = false)
        {
            useExternalInput = true;
            extThrottle = Mathf.Clamp(throttle, -1f, 1f);
            extTurn = Mathf.Clamp(turn, -1f, 1f);
            extTurbo = turbo;
        }

        public void ClearExternalInput()
        {
            useExternalInput = false;
            extThrottle = extTurn = 0f;
            extTurbo = false;
        }

        /// <summary>Teletransporta o robô (parado) para uma posição e direção.</summary>
        public void Teleport(Vector3 position, float yawDegrees)
        {
            Quaternion rot = Quaternion.Euler(0f, yawDegrees, 0f);
            rb.position = position;
            rb.rotation = rot;
            transform.SetPositionAndRotation(position, rot);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            currentSpeed = currentTurnSpeed = 0f;
            knockbackVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }
    }
}
