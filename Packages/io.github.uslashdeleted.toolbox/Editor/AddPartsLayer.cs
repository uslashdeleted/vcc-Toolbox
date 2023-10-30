using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using VRC.SDK3.Avatars.ScriptableObjects;
using static AvatarToolbox.ParameterHandler;
using static AvatarToolbox.MenuHandler;
public class CreatePartsLayer : EditorWindow
{
    private AnimatorController controller;
    private string layerName = "Layer Name";
    private string[] intParameterNames = { "Int" };
    private int selectedIntParameterIndex = 0;

    private float transitionDuration = 0.0F;

    private DefaultAsset animationFolder;
    private Dictionary<string, List<string>> animationRootFolder = new Dictionary<string, List<string>>();
    private List<bool> animationCheckboxes = new List<bool>();

    private VRCExpressionsMenu vrcMenu;
    private VRCExpressionParameters vrcParameters;
    private bool addToVRCMenu = false;
    private string submenuName;

    private Vector2 scrollPosition;
    private bool addingLayer = false;
    private bool scanComplete = false;

    [MenuItem("Toolbox/Create Parts Layer")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow<CreatePartsLayer>("Create Parts Layer");
    }

    private void OnGUI()
    {
        controller = EditorGUILayout.ObjectField("Animator Controller", controller, typeof(AnimatorController), false) as AnimatorController;
        layerName = EditorGUILayout.TextField("Layer Name", layerName);

        intParameterNames = GetIntParameterNames(controller);
        selectedIntParameterIndex = Mathf.Clamp(selectedIntParameterIndex, 0, intParameterNames.Length - 1);
        selectedIntParameterIndex = EditorGUILayout.Popup("Int Parameter:", selectedIntParameterIndex, intParameterNames);

        transitionDuration = EditorGUILayout.FloatField("Transition Duration", transitionDuration);
        animationFolder = EditorGUILayout.ObjectField("Animation Folder", animationFolder, typeof(DefaultAsset), false) as DefaultAsset;

        addToVRCMenu = EditorGUILayout.Toggle("Add to VRChat Menu", addToVRCMenu);
        if (addToVRCMenu)
        {
            vrcMenu = EditorGUILayout.ObjectField("Main Menu", vrcMenu, typeof(VRCExpressionsMenu), false) as VRCExpressionsMenu;
            vrcParameters = EditorGUILayout.ObjectField("Parameters", vrcParameters, typeof(VRCExpressionParameters), false) as VRCExpressionParameters;
            submenuName = EditorGUILayout.TextField("Submenu Name", submenuName);
        }

        GUI.enabled = !addingLayer && (animationFolder != null);

        if (GUILayout.Button("Scan folder"))
            ScanRootFolder();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DisplayAnimationsList();
        EditorGUILayout.EndScrollView();

        GUI.enabled = !addingLayer && scanComplete && controller != null && !string.IsNullOrEmpty(layerName) && (!addToVRCMenu || (vrcMenu != null && vrcParameters != null));

        if (GUILayout.Button("Add Layer"))
        {
            addingLayer = true;
            AddNewPartsLayer();
        }
    }

    private string[] GetIntParameterNames(AnimatorController controller)
    {
        if (controller != null)
        {
            return controller.parameters
                .Where(p => p.type == AnimatorControllerParameterType.Int)
                .Select(p => p.name)
                .ToArray();
        }
        return new string[1] { "Select Controller" };
    }

    private void ScanRootFolder()
    {
        animationRootFolder.Clear();
        animationCheckboxes.Clear();

        string folderPath = AssetDatabase.GetAssetPath(animationFolder);
        string[] allSubfolderPaths = AssetDatabase.GetSubFolders(folderPath);

        int totalAnimationCount = 0;

        foreach (var subfolderPath in allSubfolderPaths)
        {
            if (Regex.IsMatch(Path.GetFileName(subfolderPath), @"^\d+\..*"))
            {
                string[] animationGUIDs = AssetDatabase.FindAssets("t:AnimationClip", new[] { subfolderPath });

                if (!animationRootFolder.ContainsKey(subfolderPath))
                    animationRootFolder[subfolderPath] = new List<string>();

                foreach (var animationGUID in animationGUIDs)
                {
                    string animationPath = AssetDatabase.GUIDToAssetPath(animationGUID);
                    animationRootFolder[subfolderPath].Add(animationPath);
                    totalAnimationCount++;
                }
            }
        }

        animationCheckboxes = new List<bool>(totalAnimationCount);
        for (int i = 0; i < totalAnimationCount; i++)
        {
            animationCheckboxes.Add(false);
        }

        scanComplete = true;
    }

    private void DisplayAnimationsList()
    {
        int animationIndex = 0;

        foreach (var subfolderPath in animationRootFolder.Keys)
        {
            GUILayout.Label(Path.GetFileName(subfolderPath), EditorStyles.boldLabel);

            foreach (var animationPath in animationRootFolder[subfolderPath])
            {
                GUILayout.BeginHorizontal();

                if (animationIndex >= 0 && animationIndex < animationCheckboxes.Count)
                {
                    animationCheckboxes[animationIndex] = GUILayout.Toggle(animationCheckboxes[animationIndex], "", GUILayout.Width(20));
                    GUILayout.Label(Path.GetFileNameWithoutExtension(animationPath));
                }

                GUILayout.EndHorizontal();

                animationIndex++;
            }
        }
    }

    private void AddNewPartsLayer()
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

        // Make the layer  pretty
        newLayer.stateMachine.entryPosition = new Vector3(490, 60);
        newLayer.stateMachine.exitPosition = new Vector3(490, 110);
        newLayer.stateMachine.anyStatePosition = new Vector3(50, 0);

        // Add empty Default state
        newLayer.stateMachine.AddState("Default", new Vector3(470, 0));


        // Add each animation to the layer
        // [Any state] -|- [anim1] -|- [Default State]
        //              |- [anim2] -|
        //              |- [anim3] -|
        //              : : : : : : : 

        int animationIndex = 0;
        int pos = 0;
        foreach (var subfolderPath in animationRootFolder.Keys)
        {
            Match match = Regex.Match(subfolderPath, @"(\d+)\.");
            if (match.Success)
            {
                string folderNumber = match.Groups[1].Value;

                foreach (var animationPath in animationRootFolder[subfolderPath])
                {
                    if (animationIndex < animationCheckboxes.Count && animationCheckboxes[animationIndex])
                    {
                        string animationName = Path.GetFileNameWithoutExtension(animationPath);
                        string stateName = $"{folderNumber}_{animationName}";
                        AnimatorState newState = newLayer.stateMachine.AddState(stateName, new Vector3(250, pos));
                        AnimationClip animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animationPath);
                        newState.motion = animationClip;
                        newState.writeDefaultValues = true;
                        pos += 50;
                    }
                    animationIndex++;
                }
            }
        }

        //CreateBoolParameter();
        AddControllerParameter(controller, layerName, "bool");
        CreateAnimatorTransitions(newLayer.stateMachine);

        if (addToVRCMenu)
        {
            AddExpressionParameter(vrcParameters, layerName, "bool");
            AddControl(vrcMenu, submenuName, layerName, layerName, 0F);
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Reset values to default.

        for (int i = 0; i < animationCheckboxes.Count; i++)
        {
            animationCheckboxes[i] = false;
        }

        transitionDuration = 0.0F;

        addingLayer = false;

        Debug.Log($"Successfully added layer <color=#54e354>" + layerName + $"</color>");
    }

    private void CreateAnimatorTransitions(AnimatorStateMachine layerStateMachine)
    {

        string selectedIntParameterName = intParameterNames[selectedIntParameterIndex];
        AnimatorControllerParameter intParameter = controller.parameters.FirstOrDefault(p => p.name == selectedIntParameterName && p.type == AnimatorControllerParameterType.Int);

        if (intParameter == null)
        {
            Debug.LogError($"Integer parameter '{selectedIntParameterName}' does not exist or is not of the correct type.");
            return;
        }

        /*  
        Create transitions for layer
        [Any state] -> [anim1] -> [Default State]
        [Any state] -> [anim2] -> [Default State]
        : : : : : : : : : : : : : : : : : : : : :
        */
        foreach (ChildAnimatorState state in layerStateMachine.states)
        {
            // Check if state contains an animation
            AnimationClip clip = state.state.motion as AnimationClip;
            if (clip != null)
            {
                // Check if state matches expected name format
                string[] stateNameParts = state.state.name.Split('_');
                if (stateNameParts.Length >= 2)
                {
                    // Set intParameterVal to the number the state starts with
                    int intParameterVal;
                    if (int.TryParse(stateNameParts[0], out intParameterVal))
                    {
                        // Create transitions
                        AnimatorStateTransition transitionToAnimation = layerStateMachine.AddAnyStateTransition(state.state);
                        transitionToAnimation.AddCondition(AnimatorConditionMode.Equals, intParameterVal, selectedIntParameterName);
                        transitionToAnimation.AddCondition(AnimatorConditionMode.If, 1, layerStateMachine.name);
                        transitionToAnimation.hasExitTime = false;
                        transitionToAnimation.duration = transitionDuration;

                        AnimatorStateTransition transitionToLayerDefault = state.state.AddTransition(layerStateMachine.defaultState);
                        transitionToLayerDefault.AddCondition(AnimatorConditionMode.IfNot, 0, layerStateMachine.name);
                        transitionToLayerDefault.hasExitTime = false;
                        transitionToLayerDefault.duration = transitionDuration;
                    }
                }
            }
        }
    }
}
