#if UNITY_EDITOR 
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

/*
    Changelog:
    Fixed script errors on avatar build
    Made AddSubmenu add a page if there are 8 items in the menu instead of 7
        - It copies item #8, then puts it in the page it just created.
    AddControl now allows for folders to go deeper
        - for example, "Boolean menus/silly sounds/cow sounds"
          will make a "Boolean menus" menu, put it in vrcMenu,
          create "silly sounds", put it in "Boolean menus" etc.
    AddControl now prevents duplicate menu items.
        - It will now check if there is an item with identical name, parameter, type, and value
*/

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
                { // updatedParameters is created here because Unity is weird and won't properly append
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
            bool copyLastItem = false;

            Undo.RecordObject(vrcMenu, "Add Submenu");

            VRCExpressionsMenu.Control lastControl = null;

            if (vrcMenu.controls.Any())
            {
                lastControl = vrcMenu.controls.Last();

                if (vrcMenu.controls.Count() == 8)
                {
                    copyLastItem = true;
                }
            }

            VRCExpressionsMenu.Control existingControl = vrcMenu.controls.FirstOrDefault(control => control.name == menuName);
            VRCExpressionsMenu vrcSubMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();

            if (existingControl == null)
            {
                if (copyLastItem && lastControl != null)
                {
                    vrcSubMenu.controls.Add(lastControl);
                    vrcMenu.controls.Remove(vrcMenu.controls.Last());
                }
                AddSubmenuControl(vrcMenu, vrcSubMenu, menuName);
            }
            else if (existingControl.subMenu != null)
            {
                vrcSubMenu = existingControl.subMenu;
                menuExists = true;
            }
            else
            {
                existingControl.subMenu = vrcSubMenu;
            }

            // Save the submenu as an asset
            if (!menuExists)
            {
                string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(vrcMenu)).Replace("\\", "/");

                if (!path.EndsWith("/Submenu") && subfolderName == null)
                {
                    if (AssetDatabase.IsValidFolder($"{path}/Submenu") == false)
                    {
                        AssetDatabase.CreateFolder(path, "Submenu");
                    }
                    path += "/Submenu";
                }
                else if (path.EndsWith(subfolderName) && subfolderName != null)
                {
                    path = path.Replace($"/{subfolderName}", "");
                }

                if (!string.IsNullOrWhiteSpace(subfolderName))
                {
                    if (AssetDatabase.IsValidFolder($"{path}/{subfolderName}") == false)
                    {
                        AssetDatabase.CreateFolder(path, subfolderName);
                    }

                    AssetDatabase.CreateAsset(vrcSubMenu, $"{path}/{subfolderName}/{menuName}.asset");
                }
                else
                {
                    AssetDatabase.CreateAsset(vrcSubMenu, $"{path}/{menuName}.asset");
                }
            }

            EditorUtility.SetDirty(vrcSubMenu);
            EditorUtility.SetDirty(vrcMenu);

            return vrcSubMenu;
        }

        public static void AddControl(VRCExpressionsMenu vrcMenu, string submenuName, string controlName, string parameterName, float val)
        {

            VRCExpressionsMenu subMenu = vrcMenu;
            Undo.RecordObject(subMenu, "Add Control");

            if (submenuName != null)
            {
                string[] submenus = submenuName.Split('/');
                if (submenus.Length > 1)
                {
                    subMenu = AddSubmenu(vrcMenu, submenus[0], null);
                    for (int i = 1; i < submenus.Length; i++)
                    {
                        subMenu = AddSubmenu(subMenu, submenus[i], submenus[i - 1]);
                    }
                }
                else
                {
                    subMenu = AddSubmenu(vrcMenu, submenuName, null);
                }
            }

            VRCExpressionsMenu lastPage = FindLastPage(subMenu);
            if (lastPage.controls.Count() == 8)
            {
                Debug.Log("Menu is full, adding page.");
                AddPage(subMenu);
                lastPage = FindLastPage(lastPage);
            }
            Undo.RecordObject(lastPage, "Add Control");

            VRCExpressionsMenu.Control newControl = new VRCExpressionsMenu.Control()
            {
                name = controlName,
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter() { name = parameterName },
                value = val
            };
            bool controlExists = false;

            foreach (VRCExpressionsMenu.Control control in lastPage.controls)
            {
                if (control.name == newControl.name &&
                    control.type == newControl.type &&
                    control.parameter.name == newControl.parameter.name &&
                    control.value == newControl.value)
                {
                    controlExists = true;
                    break;
                }
            }
            if (controlExists == false)
            {
                lastPage.controls.Add(newControl);
            }
            EditorUtility.SetDirty(lastPage);
            EditorUtility.SetDirty(vrcMenu);
            EditorUtility.SetDirty(subMenu);
        }
        public static void AddSubmenuControl(VRCExpressionsMenu vrcMenu, VRCExpressionsMenu subMenu, string menuName)
        {
            Undo.RecordObject(vrcMenu, "Add Submenu Control");
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
            Undo.RecordObject(vrcMenu, "Add Page");
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
                {
                    vrcMenu = targetControl.subMenu;
                }
                else
                    break;
            }
            return vrcMenu;
        }
    }
}
#endif