using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static AvatarToolbox.MenuHandler;
using static AvatarToolbox.ParameterHandler;

/*
    Changes:
    Replaced most selections with VRCAvatarDescriptor to improve UX.
    Made some minor changes to improve prettiness of the code.
    Fixed an issue caused when Submenu Name is null (now prevents creating layer when it is null, and set a default name.)
    Added Undo support.
*/

public class AddBoolLayer : EditorWindow
{
    // Avatar components
    private VRCAvatarDescriptor avatarDescriptor;
    private AnimatorController controller;
    private bool addToVRCMenu = false;
    private VRCExpressionsMenu vrcMenu;
    private VRCExpressionParameters vrcParameters;
    private string submenuName = "Bools";

    // Selecting animations
    [SerializeField]
    private List<AnimationClip> animationClipsList = new List<AnimationClip>();

    private Vector2 scrollPosition;

    [MenuItem("Toolbox/Create Bool Layers")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow<AddBoolLayer>("Create Bool Layers");
    }

    private SerializedObject serializedObject;
    private void OnEnable()
    {
        serializedObject = new SerializedObject(this);
    }

    private void OnGUI()
    {
        avatarDescriptor = EditorGUILayout.ObjectField("Avatar Descriptor", avatarDescriptor, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;

        if (avatarDescriptor != null)
            if (avatarDescriptor.baseAnimationLayers[4].animatorController == null)
                EditorGUILayout.LabelField("No animator found on FX Playable Layer.");
            else
                controller = (AnimatorController)avatarDescriptor.baseAnimationLayers[4].animatorController;

        GUI.enabled = avatarDescriptor != null;
        addToVRCMenu = EditorGUILayout.Toggle("Add to VRChat Menu", addToVRCMenu);
        if (addToVRCMenu)
        {
            if (avatarDescriptor.expressionsMenu != null)
                vrcMenu = avatarDescriptor.expressionsMenu;
            else
                EditorGUILayout.LabelField("No Expressions Menu found.");
            if (avatarDescriptor.expressionParameters != null)
                vrcParameters = avatarDescriptor.expressionParameters;
            else
                EditorGUILayout.LabelField("No Expression Parameters found.");
            submenuName = EditorGUILayout.TextField("Submenu Name", submenuName);
        }

        EditorGUILayout.Space();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("animationClipsList"), true);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.EndScrollView();

        GUI.enabled =
            controller != null &&
            (!addToVRCMenu || (vrcMenu != null && vrcParameters != null && submenuName != null));

        if (GUILayout.Button("Add Layers"))
        {
            Undo.RecordObject(controller, "Add Bool Layers");

            if (addToVRCMenu)
            {
                Undo.RecordObject(vrcMenu, "Add Bool Layers");
                Undo.RecordObject(vrcParameters, "Add Bool Layers");
            }

            AddNewBoolLayers();
            EditorUtility.SetDirty(controller);

            if (addToVRCMenu)
            {
                EditorUtility.SetDirty(vrcMenu);
                EditorUtility.SetDirty(vrcParameters);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private void AddNewBoolLayers()
    {
        foreach (var animationClip in animationClipsList)
        {
            if (animationClip == null)
                continue;

            // Create layer and set up state machine
            AnimatorControllerLayer newLayer = controller.layers.FirstOrDefault(layer => layer.name == animationClip.name);
            if (newLayer == null)
            {
                newLayer = new AnimatorControllerLayer
                {
                    name = animationClip.name,
                    stateMachine = new AnimatorStateMachine
                    {
                        name = animationClip.name,
                        hideFlags = HideFlags.HideInHierarchy
                    }
                };
                if (AssetDatabase.GetAssetPath(controller) != "")
                    AssetDatabase.AddObjectToAsset(newLayer.stateMachine, AssetDatabase.GetAssetPath(controller));
                newLayer.defaultWeight = 1f;

                controller.AddLayer(newLayer);
            }
            else
            {   // If the state exists, clear the layer.
                foreach (ChildAnimatorState state in newLayer.stateMachine.states)
                    newLayer.stateMachine.RemoveState(state.state);

            }

            newLayer.stateMachine.entryPosition = new Vector3(490, 0);
            newLayer.stateMachine.exitPosition = new Vector3(490, 50);
            newLayer.stateMachine.anyStatePosition = new Vector3(50, 0);

            newLayer.stateMachine.AddState("Default", new Vector3(250, 0));

            // add animations to layer
            string animationName = animationClip.name;
            string stateName = animationName;
            AnimatorState newState = newLayer.stateMachine.AddState(stateName, new Vector3(250, 50));

            newState.motion = animationClip;

            CreateAnimatorTransitions(newLayer.stateMachine, newLayer.name);

            if (addToVRCMenu)
            {
                AddExpressionParameter(vrcParameters, animationClip.name, "bool");
                AddControl(vrcMenu, submenuName, animationClip.name, animationClip.name, 0.0F);
            }

            Debug.Log($"Successfully added layer <color=#54e354>" + newLayer.name + $"</color>");
        }
    }

    private void CreateAnimatorTransitions(AnimatorStateMachine layerStateMachine, string layerName)
    {
        AddControllerParameter(controller, layerName, "bool");

        ChildAnimatorState nonDefaultState = layerStateMachine.states.FirstOrDefault(state => state.state != layerStateMachine.defaultState);

        // Default state > Animation
        AnimatorStateTransition transitionToAnimation = nonDefaultState.state.AddTransition(layerStateMachine.defaultState);
        transitionToAnimation.AddCondition(AnimatorConditionMode.IfNot, 0, layerName);
        transitionToAnimation.hasExitTime = false;
        transitionToAnimation.duration = 0.0F;
        // Animation > Default State
        AnimatorStateTransition transitionToLayerDefault = layerStateMachine.defaultState.AddTransition(nonDefaultState.state);
        transitionToLayerDefault.AddCondition(AnimatorConditionMode.If, 0, layerName);
        transitionToLayerDefault.hasExitTime = false;
        transitionToLayerDefault.duration = 0.0F;
    }
}