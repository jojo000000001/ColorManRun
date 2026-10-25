using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class FinishTrigger : MonoBehaviour
{
    void Reset()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        if (GameManager.Instance != null)
            GameManager.Instance.LevelClear();
    }

    static bool IsPlayer(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        return player != null && player.CompareTag("Player");
    }
}
