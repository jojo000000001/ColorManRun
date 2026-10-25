using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Creates the animation assets once; never replaces a hand-edited controller.</summary>
public static class PlayerAnimatorSetup
{
    public const string ControllerPath = "Assets/Animations/Player.controller";
    const string ModelPath = "Assets/Prefabs/Fbx/player1.FBX";

    [MenuItem("Tools/配置角色动画资源")]
    public static void CreateAssets()
    {
        if (Application.isPlaying)
            throw new System.InvalidOperationException("Exit Play Mode before creating animation assets.");
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
        {
            Debug.Log("Player.controller already exists; preserving its configuration.");
            return;
        }

        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        if (importer == null || importer.animationType != ModelImporterAnimationType.Generic)
            throw new System.InvalidOperationException("Expected the existing Generic player1.FBX rig.");
        var clips = importer.clipAnimations;
        string[] required = { "run", "jump_start", "jump_holding", "change_color" };
        foreach (string name in required)
        {
            var clip = clips.Single(c => c.name == name);
            clip.loopTime = name == "run" || name == "jump_holding";
            clip.loopPose = clip.loopTime;
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();

        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");
        var motions = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
            .Where(c => required.Contains(c.name)).ToDictionary(c => c.name);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("InAir", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Hold", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("ChangeColor", AnimatorControllerParameterType.Trigger);

        var baseMachine = controller.layers[0].stateMachine;
        var run = State(baseMachine, "Run", motions["run"], new Vector3(250, 0));
        var start = State(baseMachine, "JumpStart", motions["jump_start"], new Vector3(500, 0));
        var hold = State(baseMachine, "JumpHold", motions["jump_holding"], new Vector3(500, 160));
        var dead = State(baseMachine, "Dead", motions["jump_holding"], new Vector3(250, 300));
        start.speed = 3f;
        dead.speed = 0f;
        baseMachine.defaultState = run;

        var death = Transition(baseMachine.AddAnyStateTransition(dead), 0f);
        Condition(death, "Dead", true);
        // Higher priority than Jump also cancels its initial blend on early landing.
        var grounded = Transition(baseMachine.AddAnyStateTransition(run), 0f);
        Condition(grounded, "InAir", false);
        Condition(grounded, "Dead", false);
        var jump = Transition(baseMachine.AddAnyStateTransition(start), 0.02f);
        Condition(jump, "Jump", true);
        Condition(jump, "InAir", true);
        Condition(jump, "Dead", false);
        var fall = Transition(run.AddTransition(hold), 0.03f);
        Condition(fall, "InAir", true);
        Condition(fall, "Hold", true);
        Condition(fall, "Dead", false);
        var earlyLand = Transition(start.AddTransition(run), 0f);
        Condition(earlyLand, "InAir", false);
        Condition(earlyLand, "Dead", false);
        var airborne = Transition(start.AddTransition(hold), 0.03f);
        Condition(airborne, "InAir", true);
        Condition(airborne, "Hold", true);
        Condition(airborne, "Dead", false);
        var land = Transition(hold.AddTransition(run), 0f);
        Condition(land, "InAir", false);
        Condition(land, "Dead", false);
        var restart = Transition(dead.AddTransition(run), 0f);
        Condition(restart, "Dead", false);

        controller.AddLayer("ColorTurn");
        var layers = controller.layers;
        // change_color is a full-body 360-degree turn, including Bip001 rotation.
        layers[1].avatarMask = null;
        layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
        layers[1].defaultWeight = 0f; // PlayerAnimator enables it for real color changes only.
        controller.layers = layers;
        var upper = layers[1].stateMachine;
        var empty = State(upper, "Empty", null, new Vector3(250, 0));
        var change = State(upper, "ChangeColor", motions["change_color"], new Vector3(500, 0));
        upper.defaultState = empty;
        var changeIn = Transition(empty.AddTransition(change), 0.02f);
        Condition(changeIn, "ChangeColor", true);
        Condition(changeIn, "Dead", false);
        var cancel = Transition(change.AddTransition(empty), 0f);
        Condition(cancel, "Dead", true);
        var changeOut = Transition(change.AddTransition(empty), 0.04f);
        changeOut.hasExitTime = true;
        changeOut.exitTime = 1f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Created Player.controller with a full-body ColorTurn layer. Assign it to each scene's Player.");
    }

    static AnimatorState State(AnimatorStateMachine machine, string name, Motion motion, Vector3 position)
    {
        var state = machine.AddState(name, position);
        state.motion = motion;
        state.writeDefaultValues = false;
        return state;
    }

    static AnimatorStateTransition Transition(AnimatorStateTransition transition, float seconds)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = seconds;
        transition.offset = 0f;
        transition.canTransitionToSelf = false;
        transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
        // Do not let a persistent bool transition continually interrupt itself.
        // Earlier transitions (especially landing/death) can still interrupt.
        transition.orderedInterruption = true;
        return transition;
    }

    static void Condition(AnimatorStateTransition transition, string name, bool value)
    {
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, name);
    }
}
