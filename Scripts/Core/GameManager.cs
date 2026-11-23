using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Poeirinha
{
    /// <summary>
    /// Dono do estado do jogo. Recebe os eventos de fim (bateria, tempo, sujeira, queda)
    /// e decide vitória ou derrota. Também cuida da pausa.
    ///
    /// Regra de ouro (GDD seção 10): toda condição de fim só vale se o estado atual for
    /// Playing. Assim é impossível ganhar e perder no mesmo frame ou disparar o fim duas vezes.
    /// </summary>
    [DefaultExecutionOrder(-100)] // roda antes dos outros scripts para o Instance já existir
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        /// <summary>Atalho usado por todos os sistemas: "o jogo está rodando agora?"</summary>
        public static bool Playing => Instance != null && Instance.State == GameState.Playing;

        [Header("Referências (arraste no Inspector)")]
        [SerializeField] BatterySystem battery;
        [SerializeField] CleaningManager cleaning;
        [SerializeField] LevelTimer timer;

        [Header("Estrelas da vitória (segundos restantes)")]
        [SerializeField] float threeStarsAbove = 60f;
        [SerializeField] float twoStarsAbove = 30f;

        public GameState State { get; private set; } = GameState.Playing;
        public DefeatReason LastDefeatReason { get; private set; } = DefeatReason.None;

        public BatterySystem Battery => battery;
        public CleaningManager Cleaning => cleaning;
        public LevelTimer Timer => timer;

        /// <summary>Disparado sempre que o estado muda. A UI e o áudio escutam este evento.</summary>
        public event Action<GameState> OnStateChanged;

        InputAction pauseAction;

        void Awake()
        {
            Instance = this;
            // Garantia extra: uma cena nova sempre começa com o tempo normal.
            Time.timeScale = 1f;
            AudioListener.pause = false;
            pauseAction = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
        }

        void OnEnable()
        {
            pauseAction.Enable();
            if (battery != null) battery.OnBatteryEmpty += HandleBatteryEmpty;
            if (timer != null) timer.OnTimeUp += HandleTimeUp;
            if (cleaning != null) cleaning.OnGoalReached += HandleGoalReached;
        }

        void OnDisable()
        {
            pauseAction.Disable();
            if (battery != null) battery.OnBatteryEmpty -= HandleBatteryEmpty;
            if (timer != null) timer.OnTimeUp -= HandleTimeUp;
            if (cleaning != null) cleaning.OnGoalReached -= HandleGoalReached;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            pauseAction?.Dispose();
        }

        void Start()
        {
            SetState(GameState.Playing);
        }

        void Update()
        {
            // Input em Update (GDD seção 14). Funciona mesmo com timeScale = 0.
            if (pauseAction.WasPressedThisFrame()) TogglePause();
        }

        // ---------- Pausa ----------

        public void TogglePause()
        {
            if (State == GameState.Playing) SetState(GameState.Paused);
            else if (State == GameState.Paused) SetState(GameState.Playing);
        }

        public void Resume()
        {
            if (State == GameState.Paused) SetState(GameState.Playing);
        }

        // ---------- Fim de jogo ----------

        public void Win()
        {
            if (State != GameState.Playing) return; // guarda contra fim duplo
            SetState(GameState.Victory);
        }

        public void Lose(DefeatReason reason)
        {
            if (State != GameState.Playing) return; // guarda contra fim duplo
            LastDefeatReason = reason;
            SetState(GameState.Defeat);
        }

        void HandleBatteryEmpty() => Lose(DefeatReason.BatteryEmpty);
        void HandleTimeUp() => Lose(DefeatReason.TimeUp);
        void HandleGoalReached() => Win();

        /// <summary>Estrelas da vitória: 3 se sobrou mais de 60 s, 2 se mais de 30 s, senão 1.</summary>
        public int CalculateStars()
        {
            float remaining = timer != null ? timer.Remaining : 0f;
            if (remaining > threeStarsAbove) return 3;
            if (remaining > twoStarsAbove) return 2;
            return 1;
        }

        // ---------- Navegação ----------

        /// <summary>Reiniciar = recarregar a cena. Nada de "resetar na mão".</summary>
        public void Restart() => SceneLoader.Load(SceneLoader.Level01);

        public void GoToMenu() => SceneLoader.Load(SceneLoader.MainMenu);

        public void QuitGame() => SceneLoader.Quit();

        void SetState(GameState newState)
        {
            State = newState;

            // Só a pausa congela o tempo. Na vitória/derrota o tempo continua
            // (a câmera ainda acompanha a queda da escada, por exemplo).
            Time.timeScale = newState == GameState.Paused ? 0f : 1f;

            // Jogando: cursor preso e escondido (o mouse gira a câmera).
            // Pausa/fim: cursor livre para clicar nos botões.
            bool playing = newState == GameState.Playing;
            Cursor.visible = !playing;
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;

            OnStateChanged?.Invoke(newState);
        }
    }
}
