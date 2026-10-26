using UnityEngine;

/// <summary>
/// Spins the crystal mesh only. Keep this on the child so the parent stays fixed above the track.
/// </summary>
[DisallowMultipleComponent]
public sealed class CrystalSpinner : MonoBehaviour
{
    [SerializeField, Min(0f)] float degreesPerSecond = 28f;

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            return;
        transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.World);
    }
}
