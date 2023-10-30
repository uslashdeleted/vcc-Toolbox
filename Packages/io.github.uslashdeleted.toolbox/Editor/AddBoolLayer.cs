using UnityEditor;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using System.Linq;
using VRC.SDK3.Avatars.ScriptableObjects;
using static AvatarToolbox.ParameterHandler;
using static AvatarToolbox.MenuHandler;

public class AddBoolLayer : EditorWindow
{
    private AnimatorController controller;

    private bool addingLayer = false;

    private Vector2 scrollPosition;

    private SerializedObject serializedObject;

    private VRCExpressionsMenu vrcMenu;
    private VRCExpressionParameters vrcParameters;
    private bool addToVRCMenu = false;
    private string submenuName;

    [SerializeField]
    private List<AnimationClip> animationClipsList = new List<AnimationClip>();

    [MenuItem("Toolbox/Create Bool Layers")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow<AddBoolLayer>("Create Bool Layers");
    }

    private void OnEnable()
    {
        serializedObject = new SerializedObject(this);
    }

    private void OnGUI()
    {
        controller = EditorGUILayout.ObjectField("Animator Controller", controller, typeof(AnimatorController), false) as AnimatorController;

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


        GUI.enabled = !addingLayer && controller != null && (!addToVRCMenu || (vrcMenu != null && vrcParameters != null));

        if (GUILayout.Button("Add Layers"))
        {
            addingLayer = true;
            AddNewBoolLayers();
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            addingLayer = false;
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
                {
                    newLayer.stateMachine.RemoveState(state.state);
                }
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