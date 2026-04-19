using UnityEngine;
using UnityEditor;

namespace LogicCuteGuy.Editor
{
    public abstract class LogicCuteGuyEditorWindow : EditorWindow
    {
        public enum LCG_Language
        {
            EN,
            JP,
            TH
        }

        private static LCG_Language? _currentLanguage;
        protected static LCG_Language currentLanguage
        {
            get
            {
                if (!_currentLanguage.HasValue)
                {
                    _currentLanguage = (LCG_Language)EditorPrefs.GetInt("LogicCuteGuy_SelectedLanguage", 0);
                }
                return _currentLanguage.Value;
            }
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    EditorPrefs.SetInt("LogicCuteGuy_SelectedLanguage", (int)value);
                    foreach (var window in Resources.FindObjectsOfTypeAll<LogicCuteGuyEditorWindow>())
                    {
                        window.Repaint();
                    }
                }
            }
        }

        private Texture2D headerLogo;
        private Texture2D supportIcon;
        private GUIStyle footerStyle;

        private string _version;
        protected virtual string Version => string.IsNullOrEmpty(_version) ? (_version = LoadVersion()) : _version;

        protected virtual string SupportLink => "https://profile.logiccuteguy.com/#donate";

        private string LoadVersion()
        {
            // 1. Try finding by assembly
            try
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(GetType().Assembly);
                if (info != null && !string.IsNullOrEmpty(info.version)) return info.version;
            }
            catch { }

            // 2. Try finding by asset path of this script
            try
            {
                string path = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
                if (!string.IsNullOrEmpty(path))
                {
                    var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
                    if (info != null && !string.IsNullOrEmpty(info.version)) return info.version;

                    // 3. Deep Fallback: Read package.json file directly relative to script
                    // Editor/LogicCuteGuyEditorWindow.cs -> ../package.json
                    string root = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));
                    string jsonPath = System.IO.Path.Combine(root, "package.json");
                    if (System.IO.File.Exists(jsonPath))
                    {
                        string content = System.IO.File.ReadAllText(jsonPath);
                        var match = System.Text.RegularExpressions.Regex.Match(content, "\"version\"\\s*:\\s*\"([^\"]+)\"");
                        if (match.Success) return match.Groups[1].Value;
                    }
                }
            }
            catch { }

            return "1.0.0";
        }

        protected static string T(string en, string jp, string th)
        {
            switch (currentLanguage)
            {
                case LCG_Language.JP: return jp;
                case LCG_Language.TH: return th;
                default: return en;
            }
        }

        protected System.Enum LCG_EnumPopup(string label, System.Enum selected, string[] en, string[] jp, string[] th)
        {
            string[] options = en;
            if (currentLanguage == LCG_Language.JP) options = jp;
            else if (currentLanguage == LCG_Language.TH) options = th;

            int index = System.Array.IndexOf(System.Enum.GetValues(selected.GetType()), selected);
            int newIndex = EditorGUILayout.Popup(label, index, options);
            return (System.Enum)System.Enum.GetValues(selected.GetType()).GetValue(newIndex);
        }

        protected virtual void OnEnable()
        {
            headerLogo =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Packages/com.logiccuteguy.helptools/Editor/Assets/Texture/LogicCuteGuyText256.png");
            supportIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/com.logiccuteguy.helptools/Editor/Assets/Texture/heart.png");
        }

        protected void OnGUI()
        {
            DrawHeader();

            OnWindowGUI();

            GUILayout.FlexibleSpace();
            DrawFooter();
        }

        protected abstract void OnWindowGUI();

        private void DrawHeader()
        {
            if (headerLogo != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(headerLogo, GUILayout.MaxHeight(64));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }

        private void DrawFooter()
        {
            if (footerStyle == null)
            {
                footerStyle = new GUIStyle(EditorStyles.helpBox);
                footerStyle.padding = new RectOffset(5, 5, 5, 5);
                footerStyle.margin = new RectOffset(0, 0, 0, 0);
            }

            using (new EditorGUILayout.HorizontalScope(footerStyle))
            {
                GUILayout.Label("v" + Version, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                LCG_Language newLang = (LCG_Language)EditorGUILayout.EnumPopup(currentLanguage, GUILayout.Width(60));
                if (newLang != currentLanguage)
                {
                    currentLanguage = newLang;
                }

                GUILayout.FlexibleSpace();

                string supportText = "Support Me";
                if (currentLanguage == LCG_Language.JP) supportText = "サポート";
                else if (currentLanguage == LCG_Language.TH) supportText = "สนับสนุน";

                GUIContent supportContent = new GUIContent(supportText, supportIcon);
                if (GUILayout.Button(supportContent, EditorStyles.miniButtonRight, GUILayout.Height(20)))
                {
                    Application.OpenURL(SupportLink);
                }
            }
        }
    }
}
