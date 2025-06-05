using UnityEngine;
using UnityEditor;
using static WeChatWASM.WXConvertCore;

namespace WeChatWASM
{

    public class WXEditorWin : EditorWindow
    {
        [MenuItem("WeChat Mini Game / Convert Mini Game", false, 1)]
        public static void Open()
        {
            var win = GetWindow(typeof(WXEditorWin), false, "WeChat Mini Game Conversion Tool Panel");
            win.minSize = new Vector2(350, 400);
            
            Rect mainWindowPosition = EditorGUIUtility.GetMainWindowPosition();
            float centerWidth = (mainWindowPosition.width - 600) * 0.5f;
            float centerHeight = (mainWindowPosition.height - 700) * 0.5f;
            win.position = new Rect(mainWindowPosition.x + centerWidth, mainWindowPosition.y + centerHeight, 600, 700);
            
            win.Show();
        }

        // For backward compatibility, please use WXConvertCore.cs
        public static WXExportError DoExport(bool buildWebGL = true)
        {
            return WXConvertCore.DoExport(buildWebGL);
        }

        public void OnFocus()
        {
            WXSettingsHelperInterface.helper.OnFocus();
        }

        public void OnLostFocus()
        {
            WXSettingsHelperInterface.helper.OnLostFocus();
        }

        public void OnDisable()
        {
            WXSettingsHelperInterface.helper.OnDisable();
        }

        public void OnGUI()
        {
            WXSettingsHelperInterface.helper.OnSettingsGUI(this);
            WXSettingsHelperInterface.helper.OnBuildButtonGUI(this);
        }
    }
}