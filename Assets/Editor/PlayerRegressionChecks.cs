using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 在已暂停的 Play Mode 中隔离验证输入状态机及真实 CharacterController 碰撞。
/// 不模拟系统键盘，不修改共享 SO 或保存场景。运行后退出 Play Mode 即恢复关卡。
/// </summary>
public static class PlayerRegressionChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static object Call(object target, string name, params object[] args)
    {
        return target.GetType().GetMethod(name, Private).Invoke(target, args);
    }

    static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, Private).SetValue(target, value);
    }

    static T Get<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, Private).GetValue(target);
    }

    [MenuItem("Tools/ColorManRun/Run Player Regression Checks (Paused Play Mode)")]
    public static void RunFromMenu()
    {
        Debug.Log(Run());
    }

    public static string Run()
    {
        if (!EditorApplication.isPlaying || !EditorApplication.isPaused)
            throw new InvalidOperationException("请进入 Play Mode 并暂停，再运行回归检查；完成后退出 Play Mode。");

        GameManager manager = GameManager.Instance;
        if (manager == null)
            throw new InvalidOperationException("当前关卡没有 GameManager。");

        var passed = new List<string>();
        var objects = new List<GameObject>();
        PlayerMovementSettings testSettings = null;
        float oldDelay = manager.restartDelay;
        manager.restartDelay = 100000f;
        Vector3 origin = new Vector3(10000f, 0f, 0f);
        try
        {
            var floor = new GameObject("Regression floor");
            objects.Add(floor);
            floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = origin + new Vector3(0f, -1f, 20f);
            var floorCollider = floor.AddComponent<BoxCollider>();
            floorCollider.size = new Vector3(3f, 2f, 60f);
            var part = floor.AddComponent<TrackPart>();
            part.SetColor(0);

            var actor = new GameObject("Regression player");
            objects.Add(actor);
            actor.SetActive(false);
            actor.tag = "Player";
            var player = actor.AddComponent<PlayerController>();
            player.centerOffset = new Vector3(0f, 0.79f, 0f);
            player.groundMask = LayerMask.GetMask("Ground");
            testSettings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>("Assets/SO/SharedPlayerMovement.asset"));
            player.movementSettings = testSettings; // Parameter variants must never write back to the shared asset.
            // Keep physics assertions deterministic when the shared gameplay tuning is edited.
            testSettings.baseSpeed = 15f;
            testSettings.jumpVelocity = 15f;
            testSettings.longJumpVelocity = 15f;
            testSettings.longJumpHoldAcceleration = 18f;
            testSettings.longJumpMaxUpwardSpeed = 18f;
            actor.SetActive(true);
            var body = actor.GetComponent<CharacterController>();
            int jumpEvents = 0;
            player.JumpStarted += () => jumpEvents++;

            Action<bool> reset = onFloor =>
            {
                manager.StartLevel(0);
                body.enabled = false;
                actor.transform.position = origin + new Vector3(0f, onFloor ? 0.21f : 5.21f, 0f);
                body.enabled = true;
                Physics.SyncTransforms();
                Set(player, "vy", 0f);
                Set(player, "speedMul", 1f);
                Set(player, "coyoteTimer", 0f);
                Set(player, "grounded", false);
                Set(player, "airJumpAvailable", false);
                Set(player, "support", null);
                Set(player, "colorIndex", 0);
                Set(player, "colorCooldown", 0f);
                Set(player, "lastJumpFrame", -1);
                Call(player, "ResetPendingInput");
                Call(player, "ProcessJumpInput", false, false, false, false, false, 0f);
                Call(player, "RefreshSupport");
                jumpEvents = 0;
            };
            Action<string, bool> check = (name, condition) =>
            {
                if (!condition)
                    throw new Exception("FAIL: " + name + " at " + actor.transform.position);
                passed.Add(name);
            };
            Action startLongJump = () =>
            {
                Call(player, "ProcessJumpInput", false, false, true, true, false, 0f);
                Call(player, "ProcessJumpInput", false, false, false, true, false, player.longPressThreshold + 0.01f);
            };
            Action pressSpaceNextFrame = () =>
            {
                // The harness executes synchronously in one paused Editor frame.
                Call(player, "ProcessJumpInput", false, false, false, false, false, 0f);
                Set(player, "lastJumpFrame", -1);
                Call(player, "ProcessJumpInput", true, true, false, false, false, 0f);
            };

            reset(true);
            check("真实平台支撑与脚底偏移", player.IsGrounded && player.Support == part && Mathf.Abs(player.FeetPosition.y) < 0.001f);
            Call(player, "SimulateMovement", 0.1f);
            check("控制器自动前进并保持落地", player.IsGrounded && Mathf.Abs(actor.transform.position.z - 1.5f) < 0.05f && Mathf.Abs(player.FeetPosition.y) < 0.05f);

            reset(true);
            pressSpaceNextFrame();
            check("第一跳保留二段跳次数", jumpEvents == 1 && Get<bool>(player, "airJumpAvailable"));
            Call(player, "ProcessJumpInput", true, true, false, false, false, 0f);
            check("同帧不能连跳或消耗二段跳", jumpEvents == 1 && Get<bool>(player, "airJumpAvailable"));
            Call(player, "SimulateMovement", 0.1f);
            Call(player, "ProcessJumpInput", false, true, false, false, false, 0.1f);
            check("持续按住空格不自动二段跳", jumpEvents == 1 && Get<bool>(player, "airJumpAvailable"));
            Set(player, "jumpBufferTimer", player.jumpBufferTime);
            Set(player, "lastJumpFrame", -1);
            Call(player, "TryBufferedJump");
            check("普通缓冲不能自动消费二段跳", jumpEvents == 1 && Get<bool>(player, "airJumpAvailable"));
            float beforeAirJumpY = actor.transform.position.y;
            pressSpaceNextFrame();
            check("空中重新按空格执行二段跳", jumpEvents == 2 && !Get<bool>(player, "airJumpAvailable") && Mathf.Approximately(Get<float>(player, "vy"), player.jumpVelocity) && Get<float>(player, "jumpBufferTimer") == 0f);
            Call(player, "SimulateMovement", 0.1f);
            check("二段跳通过控制器实际向上移动", actor.transform.position.y > beforeAirJumpY + 1f);
            pressSpaceNextFrame();
            check("空中第三次按键不能三连跳", jumpEvents == 2 && !Get<bool>(player, "airJumpAvailable"));
            Call(player, "SimulateMovement", 1.6f);
            check("真实落地恢复二段跳次数", player.IsGrounded && Get<bool>(player, "airJumpAvailable") && jumpEvents == 2);
            pressSpaceNextFrame();
            Call(player, "SimulateMovement", 0.1f);
            pressSpaceNextFrame();
            check("落地后可再次完成两段跳", jumpEvents == 4 && !Get<bool>(player, "airJumpAvailable"));

            reset(true);
            startLongJump();
            Call(player, "SimulateMovement", 0.1f);
            Set(player, "lastJumpFrame", -1);
            Call(player, "ProcessJumpInput", true, true, false, true, false, 0.4f);
            check("长跳后二段跳取消助推并保留前进倍率", jumpEvents == 2 && !Get<bool>(player, "airJumpAvailable") && !Get<bool>(player, "longJumpBoostActive") && Get<float>(player, "longJumpBoostTimeRemaining") == 0f && Mathf.Approximately(Get<float>(player, "speedMul"), player.longJumpSpeedMul));
            Call(player, "ProcessJumpInput", false, false, false, true, false, 0.6f);
            Call(player, "ProcessJumpInput", false, false, false, false, true, 0.7f);
            check("二段跳后同次上键不补跳或续推", jumpEvents == 2 && !Get<bool>(player, "longJumpBoostActive") && Get<float>(player, "jumpBufferTimer") == 0f);

            reset(true);
            actor.transform.position += Vector3.right * 4f;
            Physics.SyncTransforms();
            Call(player, "RefreshSupport");
            pressSpaceNextFrame();
            check("走出边缘的土狼跳不消耗二段跳", jumpEvents == 1 && Get<bool>(player, "airJumpAvailable"));
            Call(player, "SimulateMovement", 0.1f);
            pressSpaceNextFrame();
            check("土狼跳后仍可二段跳", jumpEvents == 2 && !Get<bool>(player, "airJumpAvailable"));
            reset(true);
            actor.transform.position += Vector3.right * 4f;
            Physics.SyncTransforms();
            Call(player, "RefreshSupport");
            Call(player, "SimulateMovement", 0.1f);
            pressSpaceNextFrame();
            pressSpaceNextFrame();
            check("直接走落平台也仅能空中跳一次", jumpEvents == 1 && !Get<bool>(player, "airJumpAvailable"));

            reset(true);
            pressSpaceNextFrame();
            Call(player, "SimulateMovement", 0.1f);
            manager.TogglePause();
            manager.TogglePause();
            Set(player, "lastJumpFrame", -1);
            Call(player, "ProcessJumpInput", true, true, false, false, false, 0.4f);
            check("暂停恢复按住不误触发二段跳", jumpEvents == 1 && Get<bool>(player, "airJumpAvailable"));
            pressSpaceNextFrame();
            check("暂停恢复松开重按仍可二段跳", jumpEvents == 2 && !Get<bool>(player, "airJumpAvailable"));
            manager.TogglePause();
            manager.TogglePause();
            pressSpaceNextFrame();
            check("暂停不能补充已使用的二段跳", jumpEvents == 2 && !Get<bool>(player, "airJumpAvailable"));
            player.enabled = false;
            player.enabled = true;
            pressSpaceNextFrame();
            check("禁用重启不能补充已使用的二段跳", jumpEvents == 2 && !Get<bool>(player, "airJumpAvailable"));

            reset(true);
            pressSpaceNextFrame();
            Call(player, "SimulateMovement", 0.8f);
            check("普通跳达到下落阶段", Get<float>(player, "vy") < 0f);
            pressSpaceNextFrame();
            check("下落中二段跳重设向上速度而非叠加", jumpEvents == 2 && Mathf.Approximately(Get<float>(player, "vy"), player.jumpVelocity));
            manager.GameOver("Regression air jump death");
            pressSpaceNextFrame();
            check("死亡状态清空并禁止二段跳", jumpEvents == 2 && !Get<bool>(player, "airJumpAvailable"));

            reset(true);
            Call(player, "ProcessJumpInput", true, true, true, true, false, 0f);
            check("同帧 Space 优先且只成功一次", jumpEvents == 1 && Mathf.Approximately(Get<float>(player, "vy"), player.jumpVelocity));
            Set(player, "lastJumpFrame", -1);
            Set(player, "grounded", true);
            Call(player, "ProcessJumpInput", false, false, false, false, true, 0.3f);
            check("Space 消耗同次上键，松开不补跳", jumpEvents == 1 && Get<float>(player, "jumpBufferTimer") == 0f);

            reset(true);
            Call(player, "ProcessJumpInput", false, false, true, true, false, 0f);
            check("上键按下即执行普通跳", jumpEvents == 1 && Mathf.Approximately(Get<float>(player, "vy"), player.jumpVelocity));
            Call(player, "ProcessJumpInput", false, false, false, false, true, 0.1f);
            check("上键短按松开不重复跳", jumpEvents == 1 && Mathf.Approximately(Get<float>(player, "vy"), player.jumpVelocity));

            reset(true);
            Call(player, "ProcessJumpInput", false, false, true, true, false, 0f);
            Call(player, "ProcessJumpInput", false, false, false, true, false, 0.21f);
            check("上键阈值长跳及原助推授权", jumpEvents == 1 && Get<bool>(player, "longJumpBoostActive") && Mathf.Approximately(Get<float>(player, "vy"), player.longJumpVelocity));
            check("助推预算从实际起跳计时", Mathf.Approximately(Get<float>(player, "longJumpBoostTimeRemaining"), 0.25f));
            Call(player, "SimulateMovement", 0.2f);
            check("起跳后0.2秒仍在有限助推", Get<bool>(player, "longJumpBoostActive") && Mathf.Abs(Get<float>(player, "longJumpBoostTimeRemaining") - 0.05f) < 0.00001f);
            Call(player, "SimulateMovement", 0.05f);
            check("0.25秒到期结束且保留向上惯性", !Get<bool>(player, "longJumpBoostActive") && Get<float>(player, "longJumpBoostTimeRemaining") == 0f && Mathf.Abs(Get<float>(player, "vy") - 14.5f) < 0.0001f);
            Call(player, "SimulateMovement", 0.1f);
            check("到期后一直按住也仅受重力", !Get<bool>(player, "longJumpBoostActive") && Mathf.Abs(Get<float>(player, "vy") - 12.5f) < 0.0001f);
            Call(player, "ProcessJumpInput", false, false, false, false, true, 0.4f);
            check("长跳松开不再普通跳", jumpEvents == 1 && Get<float>(player, "jumpBufferTimer") == 0f);

            reset(true);
            startLongJump();
            float takeoffY = actor.transform.position.y;
            float peakHeight = 0f;
            for (int i = 0; i < 120; i++)
            {
                Call(player, "SimulateMovement", 1f / 60f);
                peakHeight = Mathf.Max(peakHeight, actor.transform.position.y - takeoffY);
            }
            check("持续按住两秒正常落地且高度有限", player.IsGrounded && peakHeight > 11.4f && peakHeight < 11.9f && jumpEvents == 1);
            Call(player, "ProcessJumpInput", false, false, false, false, true, 3f);
            Set(player, "lastJumpFrame", -1);
            startLongJump();
            check("落地后新长跳重新获得完整预算", jumpEvents == 2 && Get<bool>(player, "longJumpBoostActive") && Mathf.Approximately(Get<float>(player, "longJumpBoostTimeRemaining"), 0.25f));

            reset(true);
            startLongJump();
            Call(player, "SimulateMovement", 0.05f);
            float releaseVelocity = Get<float>(player, "vy");
            Call(player, "ProcessJumpInput", false, false, false, false, true, 0.3f);
            check("提前松开清空预算但不切断速度", !Get<bool>(player, "longJumpBoostActive") && Get<float>(player, "longJumpBoostTimeRemaining") == 0f && Get<float>(player, "vy") == releaseVelocity);
            Call(player, "ProcessJumpInput", false, false, true, true, false, 0.4f);
            Call(player, "SimulateMovement", 0.01f);
            check("空中重新按住不恢复已结束助推", !Get<bool>(player, "longJumpBoostActive") && Mathf.Abs(Get<float>(player, "vy") - (releaseVelocity + player.gravity * 0.01f)) < 0.0001f);

            reset(true);
            startLongJump();
            Set(player, "longJumpBoostTimeRemaining", 0.003f);
            Call(player, "IntegrateVerticalVelocity", 0.01f);
            check("子步内到期只助推剩余时间", !Get<bool>(player, "longJumpBoostActive") && Mathf.Abs(Get<float>(player, "vy") - (15f + player.gravity * 0.01f + player.longJumpHoldAcceleration * 0.003f)) < 0.0001f);

            reset(true);
            startLongJump();
            Set(player, "vy", -1f);
            Call(player, "IntegrateVerticalVelocity", 0.01f);
            check("下落不助推且永久清空预算", !Get<bool>(player, "longJumpBoostActive") && Get<float>(player, "longJumpBoostTimeRemaining") == 0f && Mathf.Abs(Get<float>(player, "vy") + 1.2f) < 0.0001f);
            reset(true);
            startLongJump();
            Set(player, "vy", 0.01f);
            Call(player, "IntegrateVerticalVelocity", 0.01f);
            check("子步内到达最高点立即停助推", !Get<bool>(player, "longJumpBoostActive") && Mathf.Abs(Get<float>(player, "vy") + 0.1f) < 0.0001f);

            float originalAcceleration = testSettings.longJumpHoldAcceleration;
            float originalSpeedCap = testSettings.longJumpMaxUpwardSpeed;
            testSettings.longJumpHoldAcceleration = 30f;
            testSettings.longJumpMaxUpwardSpeed = 16f;
            reset(true);
            startLongJump();
            Call(player, "SimulateMovement", 0.2f);
            check("速度上限仍可提前结束助推", !Get<bool>(player, "longJumpBoostActive") && Get<float>(player, "vy") < 16f && Get<float>(player, "longJumpBoostTimeRemaining") == 0f);
            testSettings.longJumpMaxUpwardSpeed = 10f;
            reset(true);
            startLongJump();
            Call(player, "IntegrateVerticalVelocity", 0.01f);
            check("初速度超过上限不截断起跳速度", !Get<bool>(player, "longJumpBoostActive") && Mathf.Abs(Get<float>(player, "vy") - 14.8f) < 0.0001f);
            testSettings.longJumpHoldAcceleration = originalAcceleration;
            testSettings.longJumpMaxUpwardSpeed = originalSpeedCap;
            foreach (float duration in new[] { 0f, -0.1f })
            {
                testSettings.longJumpMaxHoldTime = duration;
                reset(true);
                startLongJump();
                check("非正助推时长禁用助推 " + duration, jumpEvents == 1 && !Get<bool>(player, "longJumpBoostActive") && Get<float>(player, "longJumpBoostTimeRemaining") == 0f);
            }
            testSettings.longJumpMaxHoldTime = 0.25f;

            reset(true);
            startLongJump();
            manager.TogglePause();
            manager.TogglePause();
            check("暂停清空已起跳助推预算", !Get<bool>(player, "longJumpBoostActive") && Get<float>(player, "longJumpBoostTimeRemaining") == 0f);
            reset(true);
            startLongJump();
            player.enabled = false;
            player.enabled = true;
            check("禁用清空已起跳助推预算", !Get<bool>(player, "longJumpBoostActive") && Get<float>(player, "longJumpBoostTimeRemaining") == 0f);
            reset(true);
            startLongJump();
            manager.GameOver("Regression boost reset");
            check("死亡清空已起跳助推预算", !Get<bool>(player, "longJumpBoostActive") && Get<float>(player, "longJumpBoostTimeRemaining") == 0f);

            reset(false);
            Call(player, "ProcessJumpInput", false, false, true, true, false, 0f);
            Set(player, "grounded", true);
            Call(player, "ProcessJumpInput", false, false, false, true, false, 0.3f);
            Call(player, "ProcessJumpInput", false, false, false, false, true, 0.4f);
            check("空中上键与空格一样进入普通落地缓冲", jumpEvents == 0 && Get<float>(player, "jumpBufferTimer") > 0f);

            reset(false);
            Call(player, "ProcessJumpInput", false, false, true, true, false, 0f);
            check("空中按上键不立即起跳并设置与空格相同的缓冲", jumpEvents == 0 && Get<float>(player, "jumpBufferTimer") > 0f);
            Set(player, "grounded", false);
            Set(player, "coyoteTimer", 0f);
            Call(player, "ProcessJumpInput", false, false, false, true, false, 0.21f);
            Set(player, "grounded", true);
            Call(player, "TryBufferedJump");
            check("上键落地缓冲触发普通跳且不成长跳", jumpEvents == 1 && !Get<bool>(player, "longJumpBoostActive") && Get<float>(player, "jumpBufferTimer") == 0f);

            reset(false);
            Call(player, "ProcessJumpInput", true, true, false, false, false, 0f);
            Set(player, "grounded", true);
            Call(player, "TryBufferedJump");
            check("普通跳保留落地缓冲", jumpEvents == 1);
            reset(false);
            Call(player, "ProcessJumpInput", true, true, false, false, false, 0f);
            Call(player, "SimulateMovement", 0.11f);
            Set(player, "grounded", true);
            Call(player, "TryBufferedJump");
            check("普通缓冲按原时限过期", jumpEvents == 0);
            reset(false);
            Set(player, "coyoteTimer", 0.04f);
            Call(player, "ProcessJumpInput", true, true, false, false, false, 0f);
            check("土狼时间普通跳", jumpEvents == 1);

            reset(true);
            Call(player, "ProcessJumpInput", false, false, true, true, false, 0f);
            manager.TogglePause();
            manager.TogglePause();
            Call(player, "ProcessJumpInput", true, true, false, true, false, 0.3f);
            check("暂停恢复清空输入且按住不误跳", jumpEvents == 0 && !Get<bool>(player, "arrowTracking"));
            Call(player, "ProcessJumpInput", false, false, false, false, true, 0.4f);
            Call(player, "ProcessJumpInput", true, true, false, false, false, 0.5f);
            check("恢复后松开再按可正常起跳", jumpEvents == 1);

            reset(true);
            Call(player, "ProcessJumpInput", true, true, false, false, false, 0f);
            Call(player, "TrySetColor", 1);
            check("同帧跳跃变色不能逃避真实支撑死亡", manager.State == GameState.Dead);
            reset(true);
            Call(player, "ProcessJumpInput", false, false, true, true, false, 0f);
            player.enabled = false;
            player.enabled = true;
            check("禁用重启清空跟踪缓冲", !Get<bool>(player, "arrowTracking") && Get<float>(player, "jumpBufferTimer") == 0f);

            var wall = new GameObject("Regression wall");
            objects.Add(wall);
            wall.layer = floor.layer;
            wall.transform.position = origin + new Vector3(0f, 2f, 2f);
            var wallCollider = wall.AddComponent<BoxCollider>();
            wallCollider.size = new Vector3(3f, 4f, 0.2f);
            reset(true);
            Call(player, "SimulateMovement", 0.3f);
            check("控制器墙面真实阻挡不穿透", actor.transform.position.z < 1.65f);
            TrackPart coloredWall = wall.AddComponent<TrackPart>();
            coloredWall.SetColor(1);
            reset(true);
            Call(player, "SimulateMovement", 0.3f);
            check("空中身体碰到错误颜色侧面触发死亡", manager.State == GameState.Dead);
            wall.SetActive(false);
            reset(false);
            actor.transform.position = origin + new Vector3(1.85f, -0.5f, 2f);
            Physics.SyncTransforms();
            Call(player, "RefreshSupport");
            check("平台侧面不算地面", !player.IsGrounded);

            wall.SetActive(true);
            wallCollider.size = new Vector3(3f, 0.2f, 10f);
            wall.transform.position = origin + new Vector3(0f, 2.5f, 3f);
            reset(true);
            Call(player, "ProcessJumpInput", true, true, false, false, false, 0f);
            Call(player, "SimulateMovement", 0.12f);
            check("控制器撞顶终止上升", Get<float>(player, "vy") <= 0f && player.FeetPosition.y < 0.5f);
            reset(true);
            startLongJump();
            Call(player, "SimulateMovement", 0.12f);
            check("长跳撞顶立即清空助推预算", Get<float>(player, "vy") <= 0f && !Get<bool>(player, "longJumpBoostActive") && Get<float>(player, "longJumpBoostTimeRemaining") == 0f);
            wall.SetActive(false);

            foreach (float angle in new[] { -43.54f, 43.54f })
            {
                floor.transform.position = origin - Vector3.up;
                floor.transform.rotation = Quaternion.Euler(angle, 0f, 0f);
                reset(true);
                RaycastHit surface;
                if (!Physics.Raycast(origin + Vector3.up * 100f, Vector3.down, out surface, 200f, player.groundMask))
                    throw new Exception("Slope setup failed");
                body.enabled = false;
                actor.transform.position = surface.point + Vector3.up * 1.21f;
                body.enabled = true;
                Physics.SyncTransforms();
                body.Move(Vector3.down * 2f);
                Call(player, "RefreshSupport");
                check(angle < 0 ? "43.54度上坡支撑" : "43.54度下坡支撑", player.IsGrounded);
                float startY = actor.transform.position.y;
                Call(player, "SimulateMovement", 0.25f);
                check(angle < 0 ? "控制器上坡不穿透" : "控制器下坡保持支撑", player.IsGrounded &&
                    (angle < 0 ? actor.transform.position.y > startY + 2f : actor.transform.position.y < startY - 2f));
            }
            floor.transform.position = origin + new Vector3(0f, -1f, 20f);
            floor.transform.rotation = Quaternion.identity;

            var finishObject = new GameObject("Regression finish");
            objects.Add(finishObject);
            finishObject.SetActive(false);
            finishObject.transform.position = origin + new Vector3(0f, 1.25f, 0f);
            finishObject.AddComponent<BoxCollider>().size = new Vector3(3f, 2.5f, 3f);
            var finish = finishObject.AddComponent<FinishTrigger>();
            finish.finalPlatform = floorCollider;
            finish.player = player;
            finishObject.SetActive(true);
            reset(true);
            check("末段平台安全区域有效通关", finish.CanFinish(player));
            actor.transform.position += Vector3.right * 1.3f;
            Physics.SyncTransforms();
            Call(player, "RefreshSupport");
            check("平台侧缘胶囊未完全落稳拒绝通关", !finish.CanFinish(player));
            reset(true);
            actor.transform.position += Vector3.down * 3f;
            Physics.SyncTransforms();
            check("终点下方拒绝通关", !finish.CanFinish(player));
            reset(true);
            Call(player, "ProcessJumpInput", true, true, false, false, false, 0f);
            check("空中位于终点平台上方可通关", finish.CanFinish(player));
            Set(player, "colorIndex", 1);
            check("空中颜色不一致仍可通关", !player.IsGrounded && finish.CanFinish(player));
            reset(true);
            Set(player, "colorIndex", 1);
            check("错误颜色拒绝通关", !finish.CanFinish(player));
            reset(true);
            manager.RequestLevelClear(finish, player);
            check("终点请求仅排队不提前切关", manager.State == GameState.Playing && Get<FinishTrigger>(manager, "pendingFinish") == finish);
            manager.GameOver("Regression death priority");
            Call(manager, "LateUpdate");
            check("同帧死亡优先于已排队通关", manager.State == GameState.Dead && Get<FinishTrigger>(manager, "pendingFinish") == null);

            reset(true);
            part.SetColor(1);
            Set(player, "jumpBufferTimer", player.jumpBufferTime);
            Call(player, "SimulateMovement", 0.01f);
            check("落地颜色死亡优先于普通缓冲跳", manager.State == GameState.Dead && jumpEvents == 0);
            part.SetColor(0);

            reset(true);
            Call(player, "ProcessJumpInput", false, false, true, true, false, 0f);
            Call(player, "ProcessJumpInput", false, false, false, true, false, 0.21f);
            Set(player, "grounded", true);
            Set(player, "coyoteTimer", player.coyoteTime);
            Call(player, "RequestJump", false);
            check("每帧实际跳跃次数上限", jumpEvents == 1);
            Set(player, "jumpBufferTimer", 0f);
            Set(player, "lastJumpFrame", -1);
            Call(player, "ProcessJumpInput", false, false, false, true, false, 1f);
            Call(player, "TryBufferedJump");
            check("长按跨落地不自动起跳", jumpEvents == 1);

            reset(false);
            actor.transform.position = origin + Vector3.down * 41f;
            Physics.SyncTransforms();
            Call(player, "SimulateMovement", 0.01f);
            check("掉落高度仍触发死亡", manager.State == GameState.Dead);

            return "PASS " + passed.Count + " checks\n" + string.Join("\n", passed);
        }
        finally
        {
            manager.StartLevel(0); // 取消测试死亡产生的自动重开协程。
            manager.restartDelay = oldDelay;
            foreach (GameObject item in objects)
                if (item != null)
                    UnityEngine.Object.DestroyImmediate(item);
            if (testSettings != null)
                UnityEngine.Object.DestroyImmediate(testSettings);
        }
    }
}
