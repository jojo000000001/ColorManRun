using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Observes gameplay events only; survives level reloads so death audio is not cut short.</summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class MusicManager : MonoBehaviour
{
    [Serializable]
    public sealed class LevelMusic
    {
        public string sceneName;
        public AudioClip clip;
    }

    public static MusicManager Instance { get; private set; }

    [Header("关卡 BGM（按场景名，不按 Build Index）")]
    [SerializeField] LevelMusic[] levelMusic = new LevelMusic[0];
    [Header("音效")]
    [SerializeField] AudioClip run;
    [SerializeField] AudioClip jump;
    [SerializeField] AudioClip crash;
    [SerializeField] AudioClip landing;
    [Header("独立 2D 音源")]
    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioSource runSource;
    [SerializeField] AudioSource actionSource;
    [SerializeField] AudioSource crashSource;
    [Header("音量")]
    [SerializeField, Range(0f, 1f)] float musicVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] float runVolume = 0.25f;
    [SerializeField, Range(0f, 1f)] float actionVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] float crashVolume = 0.8f;

    GameManager manager;
    PlayerController player;
    GameState lastState = GameState.Ready;
    bool paused;
    bool groundSample;
    bool wasGrounded;
    Vector3 previousPosition;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ConfigureSources();
    }

    void OnEnable()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindScene(SceneManager.GetActiveScene());
    }

    void OnDisable()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindScene();
        StopAll();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void ConfigureSources()
    {
        Configure(musicSource, true, musicVolume);
        Configure(runSource, true, runVolume);
        Configure(actionSource, false, actionVolume);
        Configure(crashSource, false, crashVolume);
        if (runSource != null) runSource.clip = run;
    }

    static void Configure(AudioSource source, bool loop, float volume)
    {
        if (source == null) return;
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.pitch = 1f; // Tutorial slow motion does not detune music or effects.
        source.volume = volume;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => BindScene(scene);

    void BindScene(Scene scene)
    {
        // Ignore additive utility/test scenes without their own gameplay manager.
        GameManager nextManager = null;
        PlayerController nextPlayer = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (nextManager == null) nextManager = root.GetComponentInChildren<GameManager>();
            if (nextPlayer == null) nextPlayer = root.GetComponentInChildren<PlayerController>();
        }
        if (nextManager == null) return;
        UnbindScene();
        SetPaused(false);
        if (runSource != null) runSource.Stop();
        if (actionSource != null) actionSource.Stop();
        // Do NOT stop crashSource: automatic restart is shorter than crash.flac.
        manager = nextManager;
        player = nextPlayer;
        groundSample = false;
        previousPosition = player != null ? player.transform.position : Vector3.zero;
        lastState = GameState.Ready;
        SelectMusic(scene.name);
        manager.StateChanged += OnStateChanged;
        if (player != null) player.JumpStarted += OnJumpStarted;
        OnStateChanged(manager.State);
    }

    void UnbindScene()
    {
        if (manager != null) manager.StateChanged -= OnStateChanged;
        if (player != null) player.JumpStarted -= OnJumpStarted;
        manager = null;
        player = null;
        groundSample = false;
    }

    void SelectMusic(string sceneName)
    {
        AudioClip selected = null;
        foreach (LevelMusic entry in levelMusic)
            if (entry != null && entry.sceneName == sceneName) { selected = entry.clip; break; }
        if (musicSource == null || musicSource.clip == selected) return;
        musicSource.Stop();
        musicSource.clip = selected;
    }

    void OnStateChanged(GameState state)
    {
        GameState previous = lastState;
        lastState = state;
        if (state == GameState.Paused)
        {
            SetPaused(true);
            return;
        }
        SetPaused(false);
        if (state == GameState.Playing)
        {
            if (musicSource != null && musicSource.clip != null)
            {
                musicSource.UnPause();
                if (!musicSource.isPlaying) musicSource.Play();
            }
            previousPosition = player != null ? player.transform.position : Vector3.zero;
            return;
        }
        if (musicSource != null) musicSource.Pause();
        if (runSource != null) runSource.Stop();
        if (actionSource != null) actionSource.Stop();
        groundSample = false;
        if (state == GameState.Dead && previous != GameState.Dead && crashSource != null && crash != null)
            crashSource.PlayOneShot(crash);
    }

    void OnJumpStarted()
    {
        if (manager == null || manager.State != GameState.Playing) return;
        if (runSource != null) runSource.Stop();
        groundSample = true;
        wasGrounded = false;
        if (actionSource != null && jump != null) actionSource.PlayOneShot(jump);
    }

    void LateUpdate()
    {
        if (player == null || manager == null || manager.State != GameState.Playing) return;
        bool moving = (player.transform.position - previousPosition).sqrMagnitude > 0.000001f;
        previousPosition = player.transform.position;
        UpdateGroundAudio(player.IsGrounded, moving);
    }

    void UpdateGroundAudio(bool grounded, bool moving)
    {
        if (groundSample && !wasGrounded && grounded && actionSource != null && landing != null)
            actionSource.PlayOneShot(landing);
        groundSample = true;
        wasGrounded = grounded;
        if (runSource == null) return;
        if (grounded && moving && runSource.clip != null)
        {
            if (!runSource.isPlaying) runSource.Play();
        }
        else if (runSource.isPlaying) runSource.Stop();
    }

    void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value;
        PauseSource(musicSource, value);
        PauseSource(runSource, value);
        PauseSource(actionSource, value);
        PauseSource(crashSource, value);
    }

    static void PauseSource(AudioSource source, bool value)
    {
        if (source == null) return;
        if (value) source.Pause();
        else source.UnPause();
    }

    void StopAll()
    {
        if (musicSource != null) musicSource.Stop();
        if (runSource != null) runSource.Stop();
        if (actionSource != null) actionSource.Stop();
        if (crashSource != null) crashSource.Stop();
        paused = false;
    }
}
