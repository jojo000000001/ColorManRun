using UnityEngine;

/// <summary>右上角暂停按钮。Esc 与点击都会在进行中和暂停之间切换。</summary>
public sealed class PauseHud : MonoBehaviour
{
    [SerializeField] GameObject pausedOverlay;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Toggle();
    }

    public void Toggle()
    {
        if (GameManager.Instance == null)
            return;
        GameManager.Instance.TogglePause();
        ApplyOverlay();
    }

    void ApplyOverlay()
    {
        if (pausedOverlay == null || GameManager.Instance == null)
            return;
        pausedOverlay.SetActive(GameManager.Instance.State == GameState.Paused);
    }
}
