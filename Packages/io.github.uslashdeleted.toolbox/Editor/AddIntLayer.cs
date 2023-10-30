using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Linq;
using VRC.SDK3.Avatars.ScriptableObjects;
using static AvatarToolbox.ParameterHandler;
using static AvatarToolbox.MenuHandler;

public class AddIntLayer : EditorWindow
{
    private AnimatorController controller;
    private string layerName = "Int";
    private bool addingLayer = false;

    private VRCExpressionsMenu vrcMenu;
    private VRCExpressionParameters vrcParameters;
    private bool addToVRCMenu = false;
    private string submenuName;

    private Vector2 scrollPosition;

    [SerializeField]
    private List<AnimationClip> animationClipsList = new List<AnimationClip>();

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
        controller = EditorGUILayout.ObjectField("Animator Controller", controller, typeof(AnimatorController), false) as AnimatorController;
        layerName = EditorGUILayout.TextField("Layer Name", layerName);

        addToVRCMenu = EditorGUILayout.Toggle("Add to VRChat Menu", addToVRCMenu);
        if (addToVRCMenu)
        {
            vrcMenu = EditorGUILayout.ObjectField("Main Menu", vrcMenu, typeof(VRCExpressionsMenu), false) as VRCExpressionsMenu;
            vrcParameters = EditorGUILayout.ObjectField("Parameters", vrcParameters, typeof(VRCExpressionParameters), false) as VRCExpressionParameters;
            submenuName = EditorGUILayout.TextField("Submenu Name", submenuName);
        }
        EditorGUILayout.Space();
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("animationClipsList"), true);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.EndScrollView();


        GUI.enabled = !addingLayer && controller != null && !string.IsNullOrWhiteSpace(layerName) && (!addToVRCMenu || (vrcMenu != null && vrcParameters != null));

        if (GUILayout.Button("Add Layer"))
        {
            addingLayer = true;
            AddNewIntLayer();
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

        addingLayer = false;

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