# 角色 Animator 配置清单

适用工程：Unity 2022.3，`Assets/Prefabs/Fbx/player1.FBX`，现有 `PlayerController` 手动积分运动。动画不驱动物体位移，不修改重力、跳跃、输入和颜色判定。

已创建 `Assets/Animations/Player.controller`，并在 `SampleScene`、`Level2` 的 **Player** 上挂好 `PlayerAnimator`、指定 Animator Controller。不挂在旧 Runner 或 Cube 上。变色现按更新需求使用全身转身动作，不再使用上半身遮罩；旧 `PlayerUpperBody.mask` 保留在工程中，但控制器不再引用它。

## 1. FBX 导入检查

Project 选中 `player1.FBX`，Inspector：

- Rig → Animation Type：**Generic**，保留素材现有骨架，不改 Humanoid。
- Avatar Definition：**Create From This Model**，使用生成的 `player1Avatar`。
- Optimize Game Objects：关闭，保留 Transform 层级以便检查 Generic 遮罩。
- Animation → Import Animation：开启；不要修改已有剪辑名称和切片边界。
- 按下面清单设置四个剪辑后 Apply。其他剪辑不参与本次状态机。

| Clip | FBX 帧范围（30fps） | 实际长度 | Loop Time | Loop Pose |
| --- | --- | --- | --- | --- |
| run | 0–16 | 0.533s | 开 | 开 |
| jump_start | 20–40 | 0.667s | 关 | 关 |
| jump_holding | 40–41 | 0.033s | 开 | 开 |
| change_color | 46–55 | 0.300s | 关 | 关 |

`jump_holding` 只有一帧间隔，基本是保持姿态，不是较长的动态循环。

Root Transform 设置原则（四个剪辑相同）：

- Rotation / Position (Y) / Position (XZ)：**Bake Into Pose 开启**。
- Based Upon：保留 **Original**，Offset 为 0；不把起跳 Y 位移交给根运动。
- 当前 Generic FBX 的 Motion/Root Motion Node 保持现状，不额外改选移动节点；不同 Rig 下 Inspector 不一定显示全部 Humanoid Root Transform 折叠项，不需要为显示这些项切换 Rig。
- 工具已配置对应导入属性 `lockRootRotation / lockRootHeightY / lockRootPositionXZ = true`，`keepOriginalOrientation / keepOriginalPositionY / keepOriginalPositionXZ = true`。
- Player → Animator → **Apply Root Motion 关闭**，这是禁止 Animator 驱动角色根物体的必要设置。Bake Into Pose 不等于删除骨骼姿态内部的上下起伏。
- Animator → Update Mode：Normal；Culling Mode：Always Animate。逻辑暂停未修改 timeScale，因此脚本另外控制 Animator.speed。

## 2. Animator 参数表

Animator 窗口 → Parameters（名称区分大小写）：

| 参数 | 类型 | 默认值 | 用途 |
| --- | --- | --- | --- |
| InAir | Bool | false | 是否离地；落地改为 false，立即回 Run |
| Hold | Bool | false | 起跳视觉窗口结束，或者直接掉下平台，进入滞空姿态 |
| Dead | Bool | false | 死亡优先，禁止继续响应动作 |
| Jump | Trigger | 未触发 | 成功起跳时触发一次，不对应原始按键 |
| ChangeColor | Trigger | 未触发 | 实际变色成功时播放一次全身转身，冷却拒绝的输入不触发 |

## 3. 状态与 Layer

所有状态 **Write Defaults = Off**。不要添加 Animation Event 来调用跳跃或变色逻辑。

### Base Layer

Weight = 1，无 Mask，负责完整的跑步、跳跃、死亡占位。

| State | Motion | Speed | 备注 |
| --- | --- | --- | --- |
| Run | run | 1 | 默认状态（橙色） |
| JumpStart | jump_start | 3 | 视觉起跳只保留约 0.12s，由参数切往 Hold |
| JumpHold | jump_holding | 1 | 空中保持，不等待完整循环才落地 |
| Dead | jump_holding | 0 | 没有死亡素材，静止姿态占位，不是第五段死亡动画 |

### ColorTurn（全身变色转身）

- 新建第二层，名称必须为 **ColorTurn**。
- Blending：**Override**；Mask：**None**。必须包含 Bip001 的旋转，才能看到完整转身。
- 默认 Weight：**0**；变色时脚本设为 1，0.30s 后用 0.04s 淡出至 0。
- Sync 关闭；不需要第三层，也不在 Base Layer 添加全身 ChangeColor 状态。
- Empty：默认状态，Motion = None，Speed = 1。
- ChangeColor：Motion = change_color，Speed = 1。

素材 `change_color` 本身就是约 0.30s 的完整 360° 转身：Bip001 的 Y 旋转依次经过约 0°、65°、127°、179°、242°、313°，最后回到正面。旧 Neck 分支遮罩会丢弃这个关键旋转，所以现在移除遮罩，直接使用原剪辑，不额外用代码旋转 Player。

转身期间短暂覆盖全身姿态，包括腿部；Base Layer 的跑跳状态仍在后台更新。结束后恢复当前 Run/JumpStart/JumpHold，不强行回跑步。Player 的移动根节点、射线、碰撞体和 +Z 前进方向均不跟随骨骼转向。空中也可转身变色。

## 4. 每一条 Transition

公共设置（下面所有连线）：

- Fixed Duration：**开**，Duration 单位是 **秒**，不是归一化比例。
- Transition Offset：0；Can Transition To Self：关。
- Interruption Source：**Current State Then Next State**（API：`SourceThenDestination`）。
- Ordered Interruption：**开**。持续为真的 Bool 过渡不应反复中断自己，导致永远停在混合中。
- 同一行的多个条件是 **AND**；未注明 Exit Time 的都关闭 Has Exit Time。

### Base Layer 连线

| 连线 | Conditions | Has Exit Time | Duration | Interruption Source |
| --- | --- | --- | --- | --- |
| Any State → Dead | Dead = true | 关 | 0 | Current State Then Next State |
| Any State → Run | InAir = false；Dead = false | 关 | 0 | 同上 |
| Any State → JumpStart | Jump；InAir = true；Dead = false | 关 | 0.02 | 同上 |
| Run → JumpHold | InAir = true；Hold = true；Dead = false | 关 | 0.03 | 同上 |
| JumpStart → Run | InAir = false；Dead = false | 关 | 0 | 同上 |
| JumpStart → JumpHold | InAir = true；Hold = true；Dead = false | 关 | 0.03 | 同上 |
| JumpHold → Run | InAir = false；Dead = false | 关 | 0 | 同上 |
| Dead → Run | Dead = false | 关 | 0 | 同上 |

Any State 的 Transition 顺序必须是 **Dead → Run → JumpStart**。JumpStart 自己的出口顺序为 **Run → JumpHold**。

Any State → Run 是高优先级的落地保险：即便还在 Run→JumpStart 的 0.02s 混合里，也能在 `InAir=false` 后下一次 Animator 求值立即回 Run。状态自身的落地出口保留，便于查看；不要给任何落地出口开启 Exit Time。

### ColorTurn 连线

| 连线 | Conditions | Has Exit Time / Exit Time | Duration | Interruption Source |
| --- | --- | --- | --- | --- |
| Empty → ChangeColor | ChangeColor；Dead = false | 关 | 0.02 | Current State Then Next State |
| ChangeColor → Empty（死亡取消，排第一） | Dead = true | 关 | 0 | 同上 |
| ChangeColor → Empty（正常结束） | 无 | 开 / 1 | 0.04 | 同上 |

Empty 没有 Motion 且 Write Defaults 关闭时，不能只依赖回到 Empty 来恢复姿态。脚本会在转身播放完后淡出该层权重，确保恢复 Base Layer 的姿态。

## 5. 脚本接线与职责

完整实现：`Assets/Scripts/Player/PlayerAnimator.cs`。挂在带 `PlayerController` 的 Player 上，Animator 字段指向 Player 的现有 Animator。

| 接口 | 调用来源 / 行为 |
| --- | --- |
| PlayRun() | 每帧确认落地时设置 InAir=false、Hold=false；不重复 Play 或 CrossFade |
| PlayJumpStart() | PlayerController.JumpStarted；成功消费跳跃缓冲后才发事件，设置 Jump trigger |
| PlayJumpHold() | 视觉起跳计时达 0.12s，或直接从平台掉下时，设置 InAir=true、Hold=true |
| PlayChangeColor() | PlayerController.ColorChanged；实际完成材质变色后发事件，设置 ChangeColor trigger，播放全身转身 |
| PlayDead() | GameManager.State == Dead；幂等处理，进入静态占位并取消变色 |

- PlayerController 只新增上述两个成功动作事件，不改变运动和输入时序。
- PlayerAnimator 不读取 Input，不改 Transform、Rigidbody、碰撞、材质和颜色索引。
- Jump / ChangeColor 各自记录 `Time.frameCount`，同一帧重复调用只触发一次；Run / Hold 为可重复设置的 Bool，死亡为幂等状态。
- 键盘 **LeftArrow** 的成功变色继续沿用现有逻辑，不使用鼠标左键。空格、上箭头长短按、变色冷却也保持不变。
- 提前按键被缓存时，不提前播起跳；冷却期间变色请求被丢弃时，不播放变色。
- 从边缘走出去没有成功起跳事件，直接进 JumpHold，不错误重播 JumpStart。
- Paused/Ready/Clear：Animator.speed=0，视觉计时也暂停；Playing 恢复为 1。
- DefaultExecutionOrder=50，晚于现有 PlayerController.Update，同帧读取最新落地结果。

可调视觉参数：jumpStartDuration=0.12、colorAnimationDuration=0.30、colorFadeOut=0.04。更换变色剪辑长度时同步调整 colorAnimationDuration。

## 6. 短滞空时间的具体建议

按计划中的普通跳约 0.49s、长跳约 0.66s：

- Run → JumpStart：0.02s，别用默认的长混合。
- JumpStart：约 **0.10–0.14s** 后就请求 Hold，本实现取 **0.12s**。
- JumpStart → JumpHold：**0.03s**；Run → JumpHold（掉边）：0.03s。
- 任意空中状态 → Run：**0s**，立即接地；不等待 Exit Time。
- 不给起跳出口设置 Exit Time=1。两种跳跃共用相同视觉窗口，无需按长跳另等一个完整剪辑。

实际 `jump_start` 是 0.667s。Speed=3 时完整播放仍需约 0.222s，因此当前 0.12s 方案会提前截断它，保留起跳前段并用 0.03s 混合衔接 Hold，而不是承诺把整个动作放完。

如果保持 Speed=1、再等待 Exit Time=1，普通跳会先落地，起跳剪辑还没播完：可能看见角色已经落地却仍在做起跳动作，或者整次跳跃根本没进入 Hold。如果强制直接截断且完全不混合，四肢/躯干会突然换姿态，出现“抽一下”的跳变。

若后续希望完整播完起跳，优先重新裁出最关键的 3–4 帧或专门做短起跳动画，再把结尾对齐 Hold。若原 0.667s 动作强压到 0.12s，需要约 5.6 倍速，会明显过快；不要仅为播完整而延迟落地判定或修改运动。

**注意当前工程数值与旧计划不同**：两个场景保留了用户已有的 gravity=-10、jumpVelocity=15、longJumpVelocity=11.5。同高度起落的理论滞空约为 3.0s / 2.3s（实际还受台阶高度和射线吸附影响），并不是旧计划的 0.49s / 0.66s。本次没有改动物理参数；动画窗口本身也能适用于短滞空设置。

## 7. 已验证与后续验收

已在 Unity Play Mode 用现有 Player 和实际控制器检查：

- Run 循环不推动 Player 根物体；反复 PlayRun 不重置播放时间。
- 成功起跳事件 → JumpStart → 视觉计时后 JumpHold；混合能完成，不被自身 Bool 条件持续重启。
- 落地当次 Animator 求值回 Run；包括起跳混合尚未完成、Start→Hold 混合中的情况。
- 实际变色成功才进入 ColorTurn.ChangeColor；冷却拒绝第二次变色；同帧重复触发不重播。
- 全身转身测试：半程 Bip001 转向约 179°，Player 移动根节点位置和旋转不变。
- 变色结束后层权重归零；逻辑暂停和恢复控制 Animator.speed。
- 死亡高优先级打断、占位姿态静止。
- 对 Run / JumpStart 做了实际相机截图检查。测试时临时关闭的运动和调整的参数均未保存，已退出 Play Mode。

仍建议手动试玩：空格、上键短按/长按、边缘土狼跳、跳跃中左方向键变色、落差平台落地，确认所选视觉速度符合个人手感。未使用外部按键注入冒充人工试玩。

FBX 重导入对未使用的 `__preview__Take 001` 报负时间关键帧警告；四个已切片动作已能运行，本次未修改源 FBX 或压缩方案来隐藏该警告。旧 AssetCheckTool 的未使用变量警告也未在本次任务中顺带修改。

## 8. 编辑器工具

`Tools/配置角色动画资源` 对应 `Assets/Editor/PlayerAnimatorSetup.cs`，用于缺失时创建带全身 ColorTurn 层的 Controller。发现已有 Player.controller 会保留，不覆盖手动修改；它不是“强制重新生成”按钮，也不会自动绑定新场景的 Player。新场景需按第 5 节手动指定，或复制已经配置好的 Player。

## 9. 动画脚底与落地高度校准

旧 `centerOffset.y=1.32` 配合 `halfHeight=1`，会把落在 y=0 跑道上的 Player Pivot 吸附到 y=-0.32。这个偏移仅适合最初的静止姿态，跑步动画会进一步向下起伏，造成约半个单位的陷入。

当前两个场景统一配置：

- PlayerController.centerOffset：**(0, 0.79, 0)**。
- CapsuleCollider.center：**(0, 0.79, 0)**，与检测中心一致。
- halfHeight：**1**，胶囊高度 **2**，保持原值。
- y=0 的起始跑道上，Player 起始 Pivot：**y=0.21**。
- 对不同高度的平台依旧按 `地面 y + halfHeight - 世界空间 centerOffset.y` 自动落地，不硬编码平台高度。

用实际 Player 的蒙皮网格采样完整 run 周期，校准后最下方顶点约为地面以上 **0.025**，避开动画最低姿态穿地。这里调整的是碰撞/落地中心相对于模型 Pivot 的校准，不是修改跑道高度或跳跃速度；最终胶囊底部仍位于地面，落地射线长度、Ground 层和逻辑不变。
