using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;
using System.Text.RegularExpressions;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace AvatarToolbox
{
    public class ParameterHandler
    {
        public static void AddExpressionParameter(VRCExpressionParameters vrcParameters, string parameterName, string type)
        {
            if (vrcParameters == null)
            {
                Debug.LogError("Skipping adding parameter because vrcParameters is null");
                return;
            }
            VRCExpressionParameters.ValueType parameterType = VRCExpressionParameters.ValueType.Float;

            switch (type.ToLower())
            {
                case "int":
                    parameterType = VRCExpressionParameters.ValueType.Int;
                    break;
                case "bool":
                    parameterType = VRCExpressionParameters.ValueType.Bool;
                    break;
                case "float":
                    parameterType = VRCExpressionParameters.ValueType.Float;
                    break;
                default:
                    Debug.LogWarning("Invalid parameter type. Defaulting to Float");
                    break;
            }

            VRCExpressionParameters.Parameter existingParameter = vrcParameters.parameters.FirstOrDefault(parameter => parameter.name == parameterName);

            if (existingParameter != null)
            {
                // Debug.Log("Parameter exists.");
                return;
            }
            else
            {
                // Debug.Log("Parameter does not exist, creating new Parameter.");
                VRCExpressionParameters.Parameter[] updatedParameters = vrcParameters.parameters.Append(new VRCExpressionParameters.Parameter()
                {
                    name = parameterName,
                    valueType = parameterType,
                    defaultValue = 0.0F,
                    saved = true,
                    networkSynced = true
                }).ToArray();

                vrcParameters.parameters = updatedParameters;

                EditorUtility.SetDirty(vrcParameters);
            }
        }
        public static void AddControllerParameter(AnimatorController controller, string parameterName, string type)
        {
            if (controller == null)
            {
                Debug.LogError("Controller is null.");
                return;
            }

            AnimatorControllerParameterType parameterType = AnimatorControllerParameterType.Float;

            switch (type.ToLower())
            {
                case "int":
                    parameterType = AnimatorControllerParameterType.Int;
                    break;
                case "bool":
                    parameterType = AnimatorControllerParameterType.Bool;
                    break;
                case "float":
                    parameterType = AnimatorControllerParameterType.Float;
                    break;
                default:
                    Debug.LogWarning("Invalid parameter type. Defaulting to Float");
                    break;
            }

            AnimatorControllerParameter parameter = controller.parameters.FirstOrDefault(p => p.name == parameterName && p.type == parameterType);
            if (parameter == null)
                controller.AddParameter(parameterName, parameterType);
        }

    }

    public class MenuHandler
    {
        public static VRCExpressionsMenu AddSubmenu(VRCExpressionsMenu vrcMenu, string menuName, string subfolderName)
        {
            bool menuExists = false;
            if (vrcMenu.controls.Count() >= 8)
            {
                Debug.LogError("vrcMenu is full");
                return null;
            }

            VRCExpressionsMenu.Control existingControl = vrcMenu.controls.FirstOrDefault(control => control.name == menuName);
            VRCExpressionsMenu vrcSubMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();

            if (existingControl == null)
                AddSubmenuControl(vrcMenu, vrcSubMenu, menuName);

            else if (existingControl.subMenu != null)
            {
                vrcSubMenu = existingControl.subMenu;
                menuExists = true;
            }

            else
                existingControl.subMenu = vrcSubMenu;

            // Save the submenu as an asset
            if (!menuExists)
            {
                if (AssetDatabase.IsValidFolder("Assets/Toolbox") == false)
                    AssetDatabase.CreateFolder("Assets", "Toolbox");
                if (AssetDatabase.IsValidFolder("Assets/Toolbox/Generated Assets") == false)
                    AssetDatabase.CreateFolder("Assets/Toolbox", "Generated Assets");
                if (AssetDatabase.IsValidFolder("Assets/Toolbox/Generated Assets/Submenu") == false)
                    AssetDatabase.CreateFolder("Assets/Toolbox/Generated Assets", "Submenu");
                if (!string.IsNullOrWhiteSpace(subfolderName))
                {
                    if (AssetDatabase.IsValidFolder($"Assets/Toolbox/Generated Assets/Submenu/{subfolderName}") == false)
                        AssetDatabase.CreateFolder("Assets/Toolbox/Generated Assets/Submenu", $"{subfolderName}");
                    AssetDatabase.CreateAsset(vrcSubMenu, $"Assets/Toolbox/Generated Assets/Submenu/{subfolderName}/{menuName}.asset");
                }
                else
                {
                    AssetDatabase.CreateAsset(vrcSubMenu, $"Assets/Toolbox/Generated Assets/Submenu/{menuName}.asset");
                }
            }
            EditorUtility.SetDirty(vrcSubMenu);
            EditorUtility.SetDirty(vrcMenu);

            return vrcSubMenu;
        }

        public static void AddControl(VRCExpressionsMenu vrcMenu, string submenuName, string controlName, string parameterName, float val)
        {
            VRCExpressionsMenu subMenu = vrcMenu;

            if (submenuName != null)
            {
                subMenu = AddSubmenu(vrcMenu, submenuName, null);
            }

            VRCExpressionsMenu lastPage = FindLastPage(subMenu);
            if (lastPage.controls.Count() >= 7)
            {
                Debug.Log("Menu is full, adding page.");
                AddPage(subMenu);
                lastPage = FindLastPage(lastPage);
            }

            VRCExpressionsMenu.Control newControl = new VRCExpressionsMenu.Control()
            {
                name = controlName,
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter() { name = parameterName },
                value = val
            };
            lastPage.controls.Add(newControl);
            EditorUtility.SetDirty(lastPage);
            EditorUtility.SetDirty(vrcMenu);
            EditorUtility.SetDirty(subMenu);
        }
        public static void AddSubmenuControl(VRCExpressionsMenu vrcMenu, VRCExpressionsMenu subMenu, string menuName)
        {
            VRCExpressionsMenu.Control newControl = new VRCExpressionsMenu.Control()
            {
                name = menuName,
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                subMenu = subMenu
            };
            vrcMenu.controls.Add(newControl);
            EditorUtility.SetDirty(vrcMenu);
            EditorUtility.SetDirty(subMenu);
        }

        public static VRCExpressionsMenu AddPage(VRCExpressionsMenu vrcMenu)
        {
            VRCExpressionsMenu finalPage = FindLastPage(vrcMenu);

            int pageCount = 2;
            var menu = vrcMenu;

            while (true)
            {
                var targetControl = menu.controls.FirstOrDefault(control => Regex.IsMatch(control.name, $@"Page {pageCount}"));
                if (targetControl != null && targetControl.subMenu != null)
                {
                    menu = targetControl.subMenu;
                    pageCount++;
                }
                else
                    break;
            }

            finalPage = AddSubmenu(finalPage, $"Page {pageCount}", vrcMenu.name);

            EditorUtility.SetDirty(vrcMenu);
            EditorUtility.SetDirty(finalPage);

            return finalPage;
        }

        public static VRCExpressionsMenu FindLastPage(VRCExpressionsMenu vrcMenu)
        {
            while (true)
            {
                var targetControl = vrcMenu.controls.FirstOrDefault(control => Regex.IsMatch(control.name, @"Page \d+"));
                if (targetControl != null && targetControl.subMenu != null)
                    vrcMenu = targetControl.subMenu;
                else
                    break;
            }
            return vrcMenu;
        }
    }
}