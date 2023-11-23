#if UNITY_EDITOR
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
    Fixed script errors on avatar build
*/

public class AddIntLayer : EditorWindow
{
    // Avatar components
    private VRCAvatarDescriptor avatarDescriptor;
    private AnimatorController controller;
    private bool addToVRCMenu = false;
    private VRCExpressionsMenu vrcMenu;
    private VRCExpressionParameters vrcParameters;
    private string submenuName = "Int";

    // Controller components
    private string layerName = "Int";

    // Selecting animations
    [SerializeField]
    private List<AnimationClip> animationClipsList = new List<AnimationClip>();

    private Vector2 scrollPosition;

    [MenuItem("Toolbox/Create Int Layer")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow<AddIntLayer>("Create Int Layer");
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

        layerName = EditorGUILayout.TextField("Layer Name", layerName);

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
            !string.IsNullOrWhiteSpace(layerName) &&
            (!addToVRCMenu || (vrcMenu != null && vrcParameters != null && submenuName != null));

        if (GUILayout.Button("Add Layer"))
        {
            Undo.RecordObject(controller, "Add Int Layer");

            if (addToVRCMenu)
            {
                Undo.RecordObject(vrcMenu, "Add Int Layer");
                Undo.RecordObject(vrcParameters, "Add Int Layer");
            }

            AddNewIntLayer();
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

    private void AddNewIntLayer()
    {
        AnimatorControllerLayer newLayer = controller.layers.FirstOrDefault(layer => layer.name == layerName);
        if (newLayer == null)
        {
            // Create layer and set up state machine
            newLayer = new AnimatorControllerLayer
            {
                name = layerName,
                stateMachine = new AnimatorStateMachine
                {
                    name = layerName,
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
            {
                newLayer.stateMachine.RemoveState(state.state);
            }
        }

        newLayer.stateMachine.entryPosition = new Vector3(490, 0);
        newLayer.stateMachine.exitPosition = new Vector3(490, 50);
        newLayer.stateMachine.anyStatePosition = new Vector3(50, 0);

        newLayer.stateMachine.AddState("0_Default", new Vector3(250, 0));

        // add animations to layer
        int animationIndex = 0;
        int pos = 50;
        foreach (var animationClip in animationClipsList)
        {
            if (animationClip != null)
            {
                string animationName = animationClip.name;
                string stateName = $"{animationIndex + 1}_{animationName}";
                AnimatorState newState = newLayer.stateMachine.AddState(stateName, new Vector3(250, pos));
                pos += 50;
                newState.motion = animationClip;
                if (addToVRCMenu)
                    AddControl(vrcMenu, submenuName, animationName, layerName, animationIndex + 1);
            }
            animationIndex++;

        }

        AddControllerParameter(controller, layerName, "int");
        CreateAnimatorTransitions(newLayer.stateMachine);

        if (addToVRCMenu)
        {
            AddExpressionParameter(vrcParameters, layerName, "int");

        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Successfully added layer <color=#54e354>" + layerName + $"</color>");
    }

    private void CreateAnimatorTransitions(AnimatorStateMachine layerStateMachine)
    {
        // Any state > 0_Default
        AnimatorStateTransition transitionToLayerDefault = layerStateMachine.AddAnyStateTransition(layerStateMachine.defaultState);
        transitionToLayerDefault.AddCondition(AnimatorConditionMode.Equals, 0, layerName);
        transitionToLayerDefault.hasExitTime = false;
        transitionToLayerDefault.duration = 0.0F;

        foreach (ChildAnimatorState state in layerStateMachine.states)
        {
            if (state.state == layerStateMachine.defaultState)
                continue;

            string[] stateNameParts = state.state.name.Split('_');
            if (stateNameParts.Length < 2)
                continue;

            if (int.TryParse(stateNameParts[0], out int intParameterVal))
            {
                // Any state > #_animationName
                AnimatorStateTransition transitionToAnimation = layerStateMachine.AddAnyStateTransition(state.state);
                transitionToAnimation.AddCondition(AnimatorConditionMode.Equals, intParameterVal, layerName);
                transitionToAnimation.hasExitTime = false;
                transitionToAnimation.duration = 0.0F;
            }
        }

    }
}
#endif