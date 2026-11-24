using System;
using UnityEngine;
using UnityEngine.AI;

namespace Poeirinha
{
    /// <summary>
    /// Gato: patrulha entre waypoints com NavMeshAgent e, quando VÊ o robô por perto,
    /// corre atrás dele. Se encostar, empurra o robô e tira bateria.
    ///
    /// Como fugir: o gato é mais rápido que o robô andando, mas mais lento que o turbo.
    /// E o robô pode se ESCONDER embaixo de móveis baixos (sofá, cama): o gato não cabe
    /// ali (a NavMesh dele não passa por baixo) e perde o interesse.
    ///
    /// Máquina de estados (atualizada em Update):
    ///   Walking → (chegou) → Waiting ou Sleeping → Walking ...
    ///   Walking/Waiting → Noticing (viu o robô: para e olha, 0.6 s) → Chasing
    ///   Chasing → (robô escondeu / fugiu / cansou / acertou) → Walking + pausa da perseguição
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CatAI : MonoBehaviour
    {
        enum Mode { Walking, Waiting, Sleeping, Noticing, Chasing }

        [Header("Patrulha")]
        [SerializeField] Transform[] waypoints;
        [SerializeField] float walkSpeed = 1.0f;
        [SerializeField] Vector2 pauseRange = new Vector2(1f, 3f);
        [SerializeField, Range(0f, 1f)] float sleepChance = 0.2f;
        [SerializeField] float sleepDuration = 5f;

        [Header("Perseguição")]
        [SerializeField] Transform robot;
        [SerializeField] float detectDistance = 3.2f;
        [SerializeField] float loseDistance = 5f;
        [SerializeField] float noticeDuration = 0.6f;
        [Tooltip("Mais rápido que o robô andando (1.3 m/s), mais lento que o turbo (2.1 m/s).")]
        [SerializeField] float chaseSpeed = 1.6f;
        [SerializeField] float maxChaseTime = 8f;
        [SerializeField] float chaseCooldown = 5f;
        [SerializeField] float repathInterval = 0.2f;
        [Tooltip("Paredes e móveis altos bloqueiam a visão do gato.")]
        [SerializeField] LayerMask sightBlockers;
        [Tooltip("Se houver algo a menos desta altura acima do robô, ele está escondido (o gato não cabe).")]
        [SerializeField] float hideCeiling = 0.3f;
        [SerializeField] LayerMask hideMask = ~0;

        [Header("Encostar no gato")]
        [SerializeField] float pushImpulse = 4f;       // N·s (massa do robô = 2 → 2 m/s)
        [SerializeField] float batteryDamage = 10f;
        [SerializeField] float hitCooldown = 1f;       // invulnerabilidade do robô

        [Header("Visual")]
        [SerializeField] Transform model;
        [SerializeField] Transform tail;
        [SerializeField] GameObject sleepIndicator;
        [SerializeField] GameObject alertIndicator;

        /// <summary>Miado (o AudioManager escuta).</summary>
        public event Action OnMeow;
        /// <summary>O gato acertou o robô.</summary>
        public event Action OnHitRobot;
        /// <summary>Começou (true) ou parou (false) de perseguir.</summary>
        public event Action<bool> OnChaseChanged;

        public bool IsSleeping => mode == Mode.Sleeping;
        public bool IsChasing => mode == Mode.Chasing || mode == Mode.Noticing;

        NavMeshAgent agent;
        Mode mode = Mode.Waiting;
        float modeTimer;
        float chaseTimer;
        float repathTimer;
        float nextChaseTime;
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
            if (alertIndicator != null) alertIndicator.SetActive(false);
        }

        void Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += HandleStateChanged;
            EnterWaiting(UnityEngine.Random.Range(pauseRange.x, pauseRange.y));
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleStateChanged;
        }

        void Update()
        {
            AnimateModel();
            if (!GameManager.Playing || !agent.isOnNavMesh) return;

            float dt = Time.deltaTime;
            modeTimer -= dt;

            // Viu o robô? (não acorda o gato dormindo)
            if ((mode == Mode.Walking || mode == Mode.Waiting) && Time.time >= nextChaseTime && CanSeeRobot(detectDistance))
            {
                mode = Mode.Noticing;
                modeTimer = noticeDuration;
                agent.isStopped = true;
                SetAlert(true);
                OnChaseChanged?.Invoke(true);
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

                case Mode.Noticing:
                    // Um instante parado olhando: dá tempo do jogador reagir.
                    LookAtRobot();
                    if (modeTimer <= 0f)
                    {
                        mode = Mode.Chasing;
                        chaseTimer = maxChaseTime;
                        repathTimer = 0f;
                        agent.speed = chaseSpeed;
                        agent.isStopped = false;
                    }
                    break;

                case Mode.Chasing:
                    chaseTimer -= dt;
                    if (IsRobotHidden() || chaseTimer <= 0f || !CanSeeRobot(loseDistance, ignoreHidden: true))
                    {
                        StopChasing();
                        break;
                    }
                    repathTimer -= dt;
                    if (repathTimer <= 0f)
                    {
                        repathTimer = repathInterval;
                        agent.SetDestination(robot.position);
                    }
                    break;
            }
        }

        // ---------- Percepção ----------

        /// <summary>O robô está perto, à vista (sem parede no meio) e não escondido?</summary>
        bool CanSeeRobot(float range, bool ignoreHidden = false)
        {
            if (robot == null) return false;
            Vector3 eye = transform.position + Vector3.up * 0.28f;
            Vector3 target = robot.position + Vector3.up * 0.05f;
            Vector3 diff = target - eye;
            diff.y = 0f;
            if (diff.magnitude > range) return false;
            if (!ignoreHidden && IsRobotHidden()) return false;
            return !Physics.Linecast(eye, target, sightBlockers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Escondido = tem um móvel logo acima do robô, baixo demais para o gato entrar.</summary>
        public bool IsRobotHidden()
        {
            if (robot == null) return false;
            return Physics.Raycast(robot.position + Vector3.up * 0.1f, Vector3.up, hideCeiling - 0.1f, hideMask, QueryTriggerInteraction.Ignore);
        }

        void StopChasing()
        {
            nextChaseTime = Time.time + chaseCooldown;
            agent.speed = walkSpeed;
            SetAlert(false);
            OnChaseChanged?.Invoke(false);
            GoToNextWaypoint();
        }

        // ---------- Patrulha ----------

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
            agent.speed = walkSpeed;
            if (waypoints == null || waypoints.Length == 0) { EnterWaiting(2f); return; }

            // Sorteia um waypoint diferente do atual para o gato não ficar previsível.
            int next = currentWaypoint;
            if (waypoints.Length > 1)
                while (next == currentWaypoint) next = UnityEngine.Random.Range(0, waypoints.Length);
            else next = 0;
            currentWaypoint = next;

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
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        void SetAlert(bool on)
        {
            if (alertIndicator != null) alertIndicator.SetActive(on);
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

            // Depois do ataque o gato se dá por satisfeito e vai embora por um tempo.
            if (IsChasing) StopChasing();
            else
            {
                nextChaseTime = Time.time + chaseCooldown;
                if (mode == Mode.Sleeping || mode == Mode.Waiting) GoToNextWaypoint();
            }
        }

        void HandleStateChanged(GameState state)
        {
            if (agent == null || !agent.isOnNavMesh) return;
            // Pausa não precisa parar o agente (timeScale = 0 já congela). Fim de jogo, sim.
            if (state == GameState.Victory || state == GameState.Defeat)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                SetAlert(false);
            }
        }

        // ---------- Animação procedural simples ----------

        void AnimateModel()
        {
            if (model == null) return;
            float t = Time.time;
            float speed01 = agent != null ? Mathf.Clamp01(agent.velocity.magnitude / Mathf.Max(0.01f, chaseSpeed)) : 0f;
            bool sleeping = mode == Mode.Sleeping;

            // Deitado: o corpo abaixa e achata. Andando/correndo: balança.
            float bobFreq = mode == Mode.Chasing ? 16f : 10f;
            Vector3 targetPos = modelRestPos + (sleeping ? new Vector3(0f, -0.07f, 0f) : new Vector3(0f, Mathf.Abs(Mathf.Sin(t * bobFreq)) * 0.018f * speed01, 0f));
            Vector3 targetScale = sleeping ? Vector3.Scale(modelRestScale, new Vector3(1.1f, 0.75f, 1f)) : modelRestScale;
            float k = 1f - Mathf.Exp(-6f * Time.deltaTime);
            model.localPosition = Vector3.Lerp(model.localPosition, targetPos, k);
            model.localScale = Vector3.Lerp(model.localScale, targetScale, k);

            if (tail != null)
            {
                // Rabo eriçado e balançando rápido quando está caçando.
                float speed = sleeping ? 1.2f : IsChasing ? 9f : 3.5f;
                float amp = sleeping ? 8f : IsChasing ? 15f : 25f;
                float sway = Mathf.Sin(t * speed) * amp;
                tail.localRotation = tailRestRot * Quaternion.Euler(IsChasing ? 20f : 0f, sway, 0f);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, detectDistance);
            if (waypoints == null) return;
            Gizmos.color = Color.yellow;
            foreach (Transform w in waypoints) if (w != null) Gizmos.DrawSphere(w.position, 0.08f);
        }
    }
}
