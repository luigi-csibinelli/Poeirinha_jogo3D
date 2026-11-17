using System;
using UnityEngine;
using UnityEngine.AI;

namespace Poeirinha
{
    /// <summary>
    /// Gato (GDD seção 9): patrulha entre waypoints com NavMeshAgent, para de vez em
    /// quando (às vezes deita e dorme), olha para o robô quando ele chega perto e,
    /// se o robô encostar, dá um empurrão nele e tira bateria.
    ///
    /// Máquina de estados simples, atualizada em Update:
    ///   Walking → (chegou) → Waiting ou Sleeping → Walking ...
    ///   Qualquer estado (menos Sleeping) → Reacting (robô perto) → Walking
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CatAI : MonoBehaviour
    {
        enum Mode { Walking, Waiting, Sleeping, Reacting }

        [Header("Patrulha")]
        [SerializeField] Transform[] waypoints;
        [SerializeField] float walkSpeed = 1.2f;
        [SerializeField] Vector2 pauseRange = new Vector2(1f, 3f);
        [SerializeField, Range(0f, 1f)] float sleepChance = 0.25f;
        [SerializeField] float sleepDuration = 5f;

        [Header("Reação ao robô")]
        [SerializeField] Transform robot;
        [SerializeField] float reactDistance = 1.5f;
        [SerializeField] float reactDuration = 1f;
        [SerializeField] float reactCooldown = 4f;

        [Header("Encostar no gato")]
        [SerializeField] float pushImpulse = 5f;      // N·s (massa do robô = 2 → 2.5 m/s)
        [SerializeField] float batteryDamage = 10f;
        [SerializeField] float hitCooldown = 1f;       // invulnerabilidade do robô

        [Header("Visual")]
        [SerializeField] Transform model;
        [SerializeField] Transform tail;
        [SerializeField] GameObject sleepIndicator;

        /// <summary>Miado (o AudioManager escuta).</summary>
        public event Action OnMeow;
        /// <summary>O gato acertou o robô.</summary>
        public event Action OnHitRobot;

        public bool IsSleeping => mode == Mode.Sleeping;

        NavMeshAgent agent;
        Mode mode = Mode.Waiting;
        float modeTimer;
        float nextReactTime;
        float nextHitTime;
        int currentWaypoint = -1;
        Vector3 modelRestPos;
        Vector3 modelRestScale;
        Quaternion tailRestRot;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.speed = walkSpeed;
            if (model != null)
            {
                modelRestPos = model.localPosition;
                modelRestScale = model.localScale;
            }
            if (tail != null) tailRestRot = tail.localRotation;
        }

        void OnEnable()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += HandleStateChanged;
        }

        void OnDisable()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleStateChanged;
        }

        void Start()
        {
            // Se o OnEnable rodou antes do GameManager existir, assina agora.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= HandleStateChanged;
                GameManager.Instance.OnStateChanged += HandleStateChanged;
            }
            EnterWaiting(UnityEngine.Random.Range(pauseRange.x, pauseRange.y));
        }

        void Update()
        {
            AnimateModel();
            if (!GameManager.Playing || !agent.isOnNavMesh) return;

            modeTimer -= Time.deltaTime;

            // Reação: o robô chegou perto → o gato para e olha para ele.
            if (robot != null && mode != Mode.Sleeping && mode != Mode.Reacting && Time.time >= nextReactTime)
            {
                Vector3 toRobot = robot.position - transform.position;
                toRobot.y = 0f;
                if (toRobot.magnitude < reactDistance)
                {
                    mode = Mode.Reacting;
                    modeTimer = reactDuration;
                    nextReactTime = Time.time + reactCooldown;
                    agent.isStopped = true;
                }
            }

            switch (mode)
            {
                case Mode.Walking:
                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
                    {
                        if (UnityEngine.Random.value < sleepChance) EnterSleeping();
                        else EnterWaiting(UnityEngine.Random.Range(pauseRange.x, pauseRange.y));
                    }
                    break;

                case Mode.Waiting:
                case Mode.Sleeping:
                    if (modeTimer <= 0f) GoToNextWaypoint();
                    break;

                case Mode.Reacting:
                    LookAtRobot();
                    if (modeTimer <= 0f)
                    {
                        agent.isStopped = false;
                        mode = Mode.Walking; // retoma o caminho de onde parou
                    }
                    break;
            }
        }

        void EnterWaiting(float seconds)
        {
            mode = Mode.Waiting;
            modeTimer = seconds;
            if (sleepIndicator != null) sleepIndicator.SetActive(false);
        }

        void EnterSleeping()
        {
            mode = Mode.Sleeping;
            modeTimer = sleepDuration;
            if (sleepIndicator != null) sleepIndicator.SetActive(true);
        }

        void GoToNextWaypoint()
        {
            if (sleepIndicator != null) sleepIndicator.SetActive(false);
            if (waypoints == null || waypoints.Length == 0) { EnterWaiting(2f); return; }

            // Sorteia um waypoint diferente do atual para o gato não ficar previsível.
            int next = currentWaypoint;
            if (waypoints.Length > 1)
                while (next == currentWaypoint) next = UnityEngine.Random.Range(0, waypoints.Length);
            else next = 0;
            currentWaypoint = next;

            // Garante que o destino está na NavMesh (o waypoint pode estar um pouco fora).
            Vector3 target = waypoints[next].position;
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 1f, NavMesh.AllAreas)) target = hit.position;

            agent.isStopped = false;
            agent.SetDestination(target);
            mode = Mode.Walking;
        }

        void LookAtRobot()
        {
            if (robot == null) return;
            Vector3 dir = robot.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion look = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }

        // ---------- Contato com o robô ----------

        // O trigger filho (maior que o corpo sólido) manda a mensagem para cá porque
        // está no mesmo Rigidbody (cinemático) do gato.
        void OnTriggerStay(Collider other)
        {
            if (!GameManager.Playing || Time.time < nextHitTime) return;
            if (!RobotDetector.TryGetRobot(other, out RobotController robotCtrl)) return;

            nextHitTime = Time.time + hitCooldown; // 1 s de invulnerabilidade

            Vector3 away = robotCtrl.transform.position - transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
            robotCtrl.ApplyKnockback(away.normalized * pushImpulse);

            if (robotCtrl.TryGetComponent(out BatterySystem battery)) battery.TakeHit(batteryDamage);

            OnHitRobot?.Invoke();
            OnMeow?.Invoke();

            // Levou um susto: acorda e sai andando.
            if (mode == Mode.Sleeping || mode == Mode.Waiting) GoToNextWaypoint();
        }

        void HandleStateChanged(GameState state)
        {
            if (agent == null || !agent.isOnNavMesh) return;
            // Pausa não precisa parar o agente (timeScale = 0 já congela). Fim de jogo, sim.
            if (state == GameState.Victory || state == GameState.Defeat)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }

        // ---------- Animação procedural simples ----------

        void AnimateModel()
        {
            if (model == null) return;
            float t = Time.time;
            float speed01 = agent != null ? Mathf.Clamp01(agent.velocity.magnitude / Mathf.Max(0.01f, walkSpeed)) : 0f;
            bool sleeping = mode == Mode.Sleeping;

            // Deitado: o corpo abaixa e achata. Andando: balança de leve.
            Vector3 targetPos = modelRestPos + (sleeping ? new Vector3(0f, -0.07f, 0f) : new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 10f)) * 0.012f * speed01, 0f));
            Vector3 targetScale = sleeping ? Vector3.Scale(modelRestScale, new Vector3(1.1f, 0.75f, 1f)) : modelRestScale;
            float k = 1f - Mathf.Exp(-6f * Time.deltaTime);
            model.localPosition = Vector3.Lerp(model.localPosition, targetPos, k);
            model.localScale = Vector3.Lerp(model.localScale, targetScale, k);

            if (tail != null)
            {
                float sway = Mathf.Sin(t * (sleeping ? 1.2f : 3.5f)) * (sleeping ? 8f : 25f);
                tail.localRotation = tailRestRot * Quaternion.Euler(0f, sway, 0f);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, reactDistance);
            if (waypoints == null) return;
            Gizmos.color = Color.yellow;
            foreach (Transform w in waypoints) if (w != null) Gizmos.DrawSphere(w.position, 0.08f);
        }
    }
}
