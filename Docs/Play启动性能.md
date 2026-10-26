# Unity Editor Play 启动性能

## 原因与处理

2026-10-26 在 Unity 2022.3.62f3c1 的当前编辑器会话中测量：

- 原配置每次进入 Play 都进行脚本域重载。Editor.log 中域重载约 1.6 秒，同次场景加载约 36 毫秒。
- CPU Profiler 还记录到进入 Play 后约 2 秒的编辑器 UI Toolkit 调度耗时（`RetainedMode.UpdateSchedulers`），不是角色移动逻辑耗时。
- 启用 Enter Play Mode Options，仅禁用 Domain Reload，**保留 Scene Reload**。避免每次 Play 重建脚本域及关联编辑器初始化，同时仍从场景初始状态开始游戏。
- `GameManager.Instance` 在 `SubsystemRegistration` 阶段显式清空，防止跳过域重载后持有上一局静态引用。新增可变静态字段或静态事件时，也必须检查并处理跨 Play 会话残留。

## 实测

无 CPU 录制的原配置：请求进入 Play 至首个完成帧约 4587 毫秒。

修改后连续三次约 449、80、80 毫秒；首次包含较多初始化，后续为同编辑器会话的热启动。结果取决于机器、窗口布局、资源导入和编译状态，不保证固定耗时。

测量由编辑器更新回调观察 `Time.frameCount` 推进，不是屏幕实际呈现时间或独立构建启动时间。脚本修改后的编译、资源导入仍会正常发生，不在此次优化中跳过。

## 复测与验证

退出 Play 并等待编译完成后，使用菜单：

`Tools > ColorManRun > Measure Play Startup (Stops After First Frame)`

该工具仅手动触发，在观察到第一帧后自动停止，并向 Console 输出总耗时、Play 切换耗时和进入 Play 后耗时。正常游戏不会自动测量或停止。

修改后在两个独立 Play 会话分别通过 75 项角色回归检查；再次启动确认只有一个 GameManager、初始颜色为 0、游戏正常进入 Playing、计时重新开始；真实死亡重开确认创建新的管理器并恢复 Playing。场景、摄像机位置、跳跃参数及死亡/通关流程均未因此次优化修改。

如需对照测试，在 Project Settings > Editor 中临时恢复 Reload Domain；不要同时关闭 Reload Scene，否则本项目的实例状态初始化假设不成立。
