using UnityEngine;

public enum GameState
{
    Ready,
    Playing,
    Paused,
    Dead,
    Clear
}

/// <summary>
/// 玩法状态入口。这里只切换状态和单关计时，关卡加载、重置和 UI 留到 M3。
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState State { get; private set; } = GameState.Ready;
    public float LevelTime { get; private set; }

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
        State = GameState.Playing;
        LevelTime = 0f;
    }

    public void GameOver(string reason)
    {
        if (State != GameState.Playing)
            return;

        State = GameState.Dead;
        Debug.Log("GameOver: " + reason);
    }

    public void LevelClear()
    {
        if (State != GameState.Playing)
            return;

        State = GameState.Clear;
        Debug.Log("LevelClear");
    }

    public void TogglePause()
    {
        if (State == GameState.Playing)
            State = GameState.Paused;
        else if (State == GameState.Paused)
            State = GameState.Playing;
    }
}
