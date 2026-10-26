using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>左上角秒表。显示本关已进行的秒数，小数两位是不足 1 秒的部分，例如 01.25。</summary>
public sealed class LevelTimerHud : MonoBehaviour
{
    [SerializeField] Text label;

    void Update()
    {
        if (label == null)
            return;
        float elapsed = GameManager.Instance != null ? GameManager.Instance.LevelTime : 0f;
        string text = Mathf.Max(0f, elapsed).ToString("00.00", CultureInfo.InvariantCulture);
        if (label.text != text)
            label.text = text;
    }
}
