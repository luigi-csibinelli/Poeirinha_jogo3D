using UnityEngine;

namespace Poeirinha
{
    /// <summary>
    /// Toca efeitos e música. Só ESCUTA eventos dos outros sistemas: nenhum script de
    /// jogo precisa conhecer o áudio. Funciona também no menu (campos vazios são ignorados).
    ///
    /// Os sons são gerados por código (ProceduralAudio). Para usar arquivos de áudio,
    /// arraste os AudioClips nos campos "Override" do Inspector.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Header("Fontes de eventos (opcionais)")]
        [SerializeField] RobotController robot;
        [SerializeField] BatterySystem battery;
        [SerializeField] CliffSensor cliffSensor;
        [SerializeField] CleaningManager cleaning;
        [SerializeField] CatAI cat;
        [SerializeField] LevelTimer timer;

        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.22f;
        [SerializeField, Range(0f, 1f)] float sfxVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] float motorVolume = 0.25f;

        [Header("Intervalos de bip")]
        [SerializeField] float lowBatteryBeepInterval = 2f;
        [SerializeField] float edgeBeepInterval = 0.45f;

        [Header("Override (opcional): arraste clips reais aqui")]
        [SerializeField] AudioClip musicOverride;
        [SerializeField] AudioClip suckOverride;
        [SerializeField] AudioClip meowOverride;

        AudioSource musicSource, sfxSource, motorSource;
        AudioClip suck, beep, edgeBeep, plug, meow, bonk, rip, victory, defeat, chime, doorKeys;

        float lowBeepTimer, edgeBeepTimer;
        bool lowBattery, edgeWarning;

        void Awake()
        {
            AudioListener.volume = masterVolume * GameSettings.MasterVolume;
            AudioListener.pause = false;

            musicSource = CreateSource("Music", true);
            musicSource.ignoreListenerPause = true; // a música continua tocando na pausa
            sfxSource = CreateSource("SFX", false);
            motorSource = CreateSource("Motor", true);

            suck = suckOverride != null ? suckOverride : ProceduralAudio.Suck();
            meow = meowOverride != null ? meowOverride : ProceduralAudio.Meow();
            beep = ProceduralAudio.Beep(1250f, 0.11f);
            edgeBeep = ProceduralAudio.Beep(1900f, 0.07f);
            plug = ProceduralAudio.Plug();
            bonk = ProceduralAudio.Bonk();
            rip = ProceduralAudio.Rip();
            victory = ProceduralAudio.VictoryJingle();
            defeat = ProceduralAudio.DefeatJingle();
            chime = ProceduralAudio.Chime();
            doorKeys = ProceduralAudio.DoorKeys();

            musicSource.clip = musicOverride != null ? musicOverride : ProceduralAudio.MusicLoop();
            musicSource.volume = musicVolume;
            musicSource.Play();

            if (robot != null)
            {
                motorSource.clip = ProceduralAudio.MotorLoop();
                motorSource.volume = 0f;
                motorSource.Play();
            }
        }

        AudioSource CreateSource(string sourceName, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f; // 2D: o robô está sempre perto da câmera
            return src;
        }

        void ApplyVolume() => AudioListener.volume = masterVolume * GameSettings.MasterVolume;

        void OnEnable()
        {
            GameSettings.OnChanged += ApplyVolume;
            if (battery != null)
            {
                battery.OnChargingChanged += HandleCharging;
                battery.OnLowBatteryChanged += HandleLowBattery;
                battery.OnHit += HandleHit;
            }
            if (cliffSensor != null) cliffSensor.OnEdgeWarning += HandleEdge;
            if (cleaning != null)
            {
                cleaning.OnDirtCollected += HandleDirt;
                cleaning.OnMilestone += HandleMilestone;
            }
            if (cat != null) { cat.OnMeow += HandleMeow; cat.OnChaseChanged += HandleChase; }
            if (timer != null) timer.OnFinalCountdownStarted += HandleFinalCountdown;
            if (robot != null) robot.OnUnstickPress += HandleUnstick;
            if (robot != null) robot.OnStuckChanged += HandleStuck;
        }

        void OnDisable()
        {
            GameSettings.OnChanged -= ApplyVolume;
            if (battery != null)
            {
                battery.OnChargingChanged -= HandleCharging;
                battery.OnLowBatteryChanged -= HandleLowBattery;
                battery.OnHit -= HandleHit;
            }
            if (cliffSensor != null) cliffSensor.OnEdgeWarning -= HandleEdge;
            if (cleaning != null)
            {
                cleaning.OnDirtCollected -= HandleDirt;
                cleaning.OnMilestone -= HandleMilestone;
            }
            if (cat != null) { cat.OnMeow -= HandleMeow; cat.OnChaseChanged -= HandleChase; }
            if (timer != null) timer.OnFinalCountdownStarted -= HandleFinalCountdown;
            if (robot != null) robot.OnUnstickPress -= HandleUnstick;
            if (robot != null) robot.OnStuckChanged -= HandleStuck;
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleState;
        }

        void Start()
        {
            // O GameManager pode ter acordado depois do nosso OnEnable: assina aqui.
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += HandleState;
        }

        void OnDestroy()
        {
            AudioListener.pause = false;
        }

        void Update()
        {
            bool playing = GameManager.Playing;

            // Motor: o pitch acompanha a velocidade.
            if (robot != null)
            {
                float s = playing ? robot.Speed01 : 0f;
                float targetVol = playing ? Mathf.Lerp(0.25f, 1f, s) * motorVolume * sfxVolume : 0f;
                motorSource.volume = Mathf.MoveTowards(motorSource.volume, targetVol, Time.unscaledDeltaTime * 2f);
                motorSource.pitch = Mathf.Lerp(0.8f, 1.5f, s);
            }

            if (!playing) return;

            // Bip de bateria fraca a cada 2 s.
            if (lowBattery && (battery == null || !battery.IsCharging))
            {
                lowBeepTimer -= Time.deltaTime;
                if (lowBeepTimer <= 0f)
                {
                    lowBeepTimer = lowBatteryBeepInterval;
                    Play(beep, 0.8f);
                }
            }

            // Bip rápido enquanto o sensor de borda está ativo.
            if (edgeWarning)
            {
                edgeBeepTimer -= Time.deltaTime;
                if (edgeBeepTimer <= 0f)
                {
                    edgeBeepTimer = edgeBeepInterval;
                    Play(edgeBeep, 0.7f);
                }
            }
        }

        public void Play(AudioClip clip, float volume = 1f)
        {
            if (clip != null) sfxSource.PlayOneShot(clip, volume * sfxVolume);
        }

        void HandleDirt(DirtSpot spot)
        {
            sfxSource.pitch = 1f;
            Play(suck, 0.7f);
        }

        void HandleMilestone(string text) => Play(chime, 0.8f);
        void HandleCharging(bool charging) { if (charging) Play(plug, 0.9f); }
        void HandleLowBattery(bool low) { lowBattery = low; lowBeepTimer = 0f; }
        void HandleEdge(bool edge) { edgeWarning = edge; edgeBeepTimer = 0f; }
        void HandleHit(float amount) => Play(bonk, 0.8f);
        void HandleMeow() => Play(meow, 1f);
        void HandleChase(bool chasing) { if (chasing) Play(meow, 0.6f); }
        void HandleFinalCountdown() => Play(doorKeys, 1f);
        void HandleUnstick(int left) => Play(rip, 0.8f);
        void HandleStuck(bool stuck, int presses) { if (stuck) Play(beep, 0.6f); }

        void HandleState(GameState state)
        {
            // Pausa: congela os efeitos, a música continua (ignoreListenerPause).
            AudioListener.pause = state == GameState.Paused;

            if (state == GameState.Victory || state == GameState.Defeat)
            {
                musicSource.volume = musicVolume * 0.3f;
                lowBattery = edgeWarning = false;
                Play(state == GameState.Victory ? victory : defeat, 1f);
            }
        }
    }
}
