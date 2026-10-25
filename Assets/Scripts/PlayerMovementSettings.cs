using UnityEngine;

/// <summary>
/// Shared tuning only. Velocity, input consumption, color and timers belong to each player.
/// </summary>
[CreateAssetMenu(fileName = "PlayerMovement", menuName = "跑酷/角色运动配置", order = 20)]
public sealed class PlayerMovementSettings : ScriptableObject
{
    [Header("前进与重力")]
    [Min(0f)] public float baseSpeed = 15f;
    [Tooltip("向下为负值，单位/秒²。")]
    public float gravity = -20f;
    public float deathY = -40f;

    [Header("跳跃")]
    [Min(0f)] public float jumpVelocity = 10f;
    [Min(0f)] public float longJumpVelocity = 11.5f;
    [Min(0f)] public float longJumpSpeedMul = 1.45f;
    [Tooltip("长跳成功后继续按住上方向键时，叠加到重力上的向上加速度，单位/秒²。")]
    [Min(0f)] public float longJumpHoldAcceleration = 30f;
    [Tooltip("助推最大上升速度，单位/秒。达到后结束本次助推；0 为禁用助推。不截断起跳初速度。")]
    [Min(0f)] public float longJumpMaxUpwardSpeed = 13f;

    [Header("输入时序（秒）")]
    [Min(0f)] public float coyoteTime = 0.08f;
    [Min(0f)] public float jumpBufferTime = 0.1f;
    [Min(0.001f)] public float longPressThreshold = 0.2f;
    [Min(0f)] public float colorCooldownDuration = 0.35f;
}
