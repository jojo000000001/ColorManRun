using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    Ready,
    Playing,
    Paused,
    Dead,
    Clear
}

/// <summary>
/// 每个场景各自持有玩家和管理器；死亡重开本关，通关加载下一关。
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Fast Enter Play Mode skips static initialization. Clear the previous session explicitly.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }

    public GameState State { get; private set; } = GameState.Ready;
    public float LevelTime { get; private set; }
    public event System.Action<GameState> StateChanged;

    [Tooltip("通关后加载的场景名称；场景必须已加入 Build Settings。")]
    public string nextSceneName;

    [Min(0f), Tooltip("死亡后自动重开本关的等待秒数（不受 Time.timeScale 影响）。")]
    public float restartDelay = 0.5f;

    Coroutine restartRoutine;
    FinishTrigger pendingFinish;
    PlayerController pendingPlayer;

    void SetState(GameState state)
    {
        State = state;
        pendingFinish = null;
        pendingPlayer = null;
        StateChanged?.Invoke(state);
    }

    void LateUpdate()
    {
        // 所有 Update 的移动/掉落/颜色死亡先结算；终点仅提交候选。
        FinishTrigger finish = pendingFinish;
        PlayerController player = pendingPlayer;
        pendingFinish = null;
        pendingPlayer = null;
        if (State == GameState.Playing && finish != null && finish.CanFinish(player))
            CompleteLevelClear();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        if (State == GameState.Ready)
            StartLevel(0);
    }

    void Update()
    {
        if (State == GameState.Playing)
            LevelTime += Time.deltaTime;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void StartLevel(int levelIndex)
    {
        if (restartRoutine != null)
        {
            StopCoroutine(restartRoutine);
            restartRoutine = null;
        }
        SetState(GameState.Playing);
        LevelTime = 0f;
    }

    public void GameOver(string reason)
    {
        if (State != GameState.Playing)
            return;

        SetState(GameState.Dead);
        Debug.Log("GameOver: " + reason);
        restartRoutine = StartCoroutine(RestartCurrentLevel());
    }

    IEnumerator RestartCurrentLevel()
    {
        // 至少等到下一帧，避免在角色当前帧的移动积分中途重置。
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, restartDelay));
        restartRoutine = null;

        if (State != GameState.Dead)
            yield break;

        // 使用管理器所属场景，而不是下一关或固定的第一关。
        string scenePath = gameObject.scene.path;
        if (string.IsNullOrEmpty(scenePath) || !Application.CanStreamedLevelBeLoaded(scenePath))
        {
            Debug.LogError("无法重开本关：请保存当前场景并将其加入 Build Settings。", this);
            yield break;
        }

        try
        {
            // 新场景的 Start 自动开跑；角色、动画、相机和计时全部从初始状态开始。
            SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    public void RequestLevelClear(FinishTrigger finish, PlayerController player)
    {
        if (State != GameState.Playing || finish == null || !finish.CanFinish(player))
            return;
        pendingFinish = finish;
        pendingPlayer = player;
    }

    void CompleteLevelClear()
    {
        if (State != GameState.Playing)
            return;

        if (string.IsNullOrWhiteSpace(nextSceneName) || !Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError("无法切换关卡：请配置 Next Scene Name，并将目标场景加入 Build Settings。", this);
            return;
        }

        // 先锁定状态，多个玩家碰撞体或连续触发不能重复请求加载。
        SetState(GameState.Clear);
        Debug.Log("LevelClear: " + gameObject.scene.name + " -> " + nextSceneName);
        try
        {
            SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
        }
        catch (System.Exception exception)
        {
            SetState(GameState.Playing);
            Debug.LogException(exception, this);
        }
    }

    public void TogglePause()
    {
        if (State == GameState.Playing)
            SetState(GameState.Paused);
        else if (State == GameState.Paused)
            SetState(GameState.Playing);
    }
}
