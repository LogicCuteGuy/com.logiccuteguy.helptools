using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace LogicCuteGuy.Editor
{
    /// <summary>
    /// Selects children of a GameObject using a positional step pattern.
    /// Pattern uses 'x' (select) and '-' (skip), e.g.:
    ///   x-x-x   → select every other child (0, 2, 4 …)
    ///   x--x--  → select every third child  (0, 3, 6 …)
    ///   xx-xx-  → select two, skip one, repeat
    /// The pattern repeats cyclically over the child list.
    /// </summary>
    public class SelectChildrenByPattern : LogicCuteGuyEditorWindow
    {
        private GameObject parentObject;
        private string pattern = "x-x-x";
        private bool includeInactive = true;

        private List<GameObject> matchedObjects = new List<GameObject>();
        private Vector2 scrollPosition;
        private string statusMessage = "";
        private MessageType statusType = MessageType.None;

        [MenuItem("GameObject/LogicCuteGuy/Select Children By Pattern")]
        public static void ShowWindow()
        {
            var window = GetWindow<SelectChildrenByPattern>(T("Select Children By Pattern", "パターンで子を選択", "เลือกลูกตามรูปแบบ"));
            if (Selection.activeGameObject != null)
            {
                window.parentObject = Selection.activeGameObject;
                window.matchedObjects.Clear();
                window.statusMessage = "";
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
        }



        protected override void OnWindowGUI()
        {
            GUILayout.Label(T("Select Children By Pattern", "パターンで子を選択", "เลือกลูกตามรูปแบบ"), EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                T("Use  x  to select a child and  -  to skip.\n", " x を使用して子を選択し、 - でスキップします。\n", "ใช้  x  เพื่อเลือกลูก และ  -  เพื่อข้าม\n") +
                T("Examples:\n", "例:\n", "ตัวอย่าง:\n") +
                T("  x-x-x    → every other child\n", "  x-x-x    → 1つおきに選択\n", "  x-x-x    → เลือกทุกๆ ออบเจ็กต์เว้นหนึ่ง\n") +
                T("  x--x--   → every third child\n", "  x--x--   → 2つおきに選択 (3つ目ごと)\n", "  x--x--   → เลือกทุกๆ สามออบเจ็กต์\n") +
                T("  xx-xx-   → two selected, one skipped\n", "  xx-xx-   → 2つ選択して1つスキップ\n", "  xx-xx-   → เลือกสอง ข้ามหนึ่ง\n") +
                T("The pattern repeats over all direct children.", "パターンはすべての直下の子に対して繰り返されます。", "รูปแบบจะทำซ้ำในออบเจ็กต์ลูกทั้งหมด"),
                MessageType.None);

            EditorGUILayout.Space();

            // Parent object
            EditorGUI.BeginChangeCheck();
            parentObject = (GameObject)EditorGUILayout.ObjectField(T("Parent Object", "親オブジェクト", "ออบเจ็กต์พาเรนต์"), parentObject, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                matchedObjects.Clear();
                statusMessage = "";
            }

            EditorGUILayout.Space();

            // Pattern input
            EditorGUILayout.LabelField(T("Step Pattern", "ステップパターン", "รูปแบบขั้นตอน"), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            pattern = EditorGUILayout.TextField(T("Pattern  (x / -)", "パターン (x / -)", "รูปแบบ (x / -)"), pattern);
            if (EditorGUI.EndChangeCheck())
            {
                matchedObjects.Clear();
                statusMessage = "";
            }

            // Live pattern preview
            string cleanPattern = BuildCleanPattern(pattern);
            if (cleanPattern.Length > 0)
            {
                int selectCount = cleanPattern.Count(c => c == 'x');
                int skipCount = cleanPattern.Count(c => c == '-');
                EditorGUILayout.LabelField(
                    T("Cycle length: ", "サイクルの長さ: ", "ความยาวรอบ: ") + cleanPattern.Length +
                    T("  |  select ", "  |  選択: ", "  |  เลือก ") + selectCount +
                    T(", skip ", ", スキップ: ", ", ข้าม ") + skipCount +
                    T(" per cycle", " (1サイクルあたり)", " ต่อรอบ"),
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.Space();

            includeInactive = EditorGUILayout.Toggle(T("Include Inactive", "非アクティブを含める", "รวมรายการที่ไม่ได้ใช้งาน"),
                includeInactive);

            EditorGUILayout.Space();

            bool canRun = parentObject != null && cleanPattern.Length > 0 && cleanPattern.Contains('x');
            using (new EditorGUI.DisabledScope(!canRun))
            {
                if (GUILayout.Button(T("Find & Select", "検索＆選択", "ค้นหาและเลือก"), GUILayout.Height(30)))
                {
                    FindAndSelect(cleanPattern);
                }
            }

            // Status message
            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }

            // Results list
            if (matchedObjects.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(T("Matched Objects", "一致したオブジェクト", "ออบเจ็กต์ที่ตรงกัน") + $" ({matchedObjects.Count})", EditorStyles.boldLabel);

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(260));
                foreach (var go in matchedObjects)
                {
                    if (go == null) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(go, typeof(GameObject), true);
                        if (GUILayout.Button(T("Ping", "ハイライト (Ping)", "ปิง"), GUILayout.Width(44)))
                            EditorGUIUtility.PingObject(go);
                    }
                }

                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(T("Select", "選択", "เลือก")))
                    {
                        Selection.objects = matchedObjects.Where(g => g != null).Cast<Object>().ToArray();
                        statusMessage = $"Selected {matchedObjects.Count} matched object(s).";
                        statusType = MessageType.Info;
                    }

                    if (GUILayout.Button(T("Delete", "削除", "ลบ")))
                    {
                        DeleteMatchedObjects();
                    }

                    if (GUILayout.Button(T("Deselect", "選択解除", "ยกเลิกการเลือก")))
                    {
                        DeselectMatchedObjects();
                    }
                }
            }
        }

        // Strip any character that isn't x or -, and lowercase everything.
        private static string BuildCleanPattern(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in raw.ToLower())
            {
                if (c == 'x' || c == '-')
                    sb.Append(c);
            }

            return sb.ToString();
        }

        private void FindAndSelect(string cleanPattern)
        {
            matchedObjects.Clear();

            // Gather direct children in hierarchy order
            var children = new List<Transform>();
            foreach (Transform child in parentObject.transform)
            {
                if (includeInactive || child.gameObject.activeInHierarchy)
                    children.Add(child);
            }

            if (children.Count == 0)
            {
                statusMessage = "The parent has no children.";
                statusType = MessageType.Info;
                return;
            }

            int patLen = cleanPattern.Length;
            for (int i = 0; i < children.Count; i++)
            {
                char slot = cleanPattern[i % patLen];
                if (slot == 'x')
                    matchedObjects.Add(children[i].gameObject);
            }

            if (matchedObjects.Count == 0)
            {
                statusMessage = "Pattern produced no selections (all slots are '-').";
                statusType = MessageType.Warning;
            }
            else
            {
                Selection.objects = matchedObjects.Cast<Object>().ToArray();
                statusMessage = $"Selected {matchedObjects.Count} of {children.Count} children.";
                statusType = MessageType.Info;
            }
        }

        private void DeleteMatchedObjects()
        {
            var validObjects = matchedObjects.Where(g => g != null).ToList();
            if (validObjects.Count == 0)
            {
                statusMessage = "No matched objects to delete.";
                statusType = MessageType.Warning;
                return;
            }

            int deletedCount = 0;
            foreach (var go in validObjects)
            {
                Undo.DestroyObjectImmediate(go);
                deletedCount++;
            }

            matchedObjects.Clear();
            statusMessage = $"Deleted {deletedCount} object(s).";
            statusType = MessageType.Info;
            Selection.objects = new Object[0];
        }

        private void DeselectMatchedObjects()
        {
            var matchedSet = new HashSet<GameObject>(matchedObjects.Where(g => g != null));
            if (matchedSet.Count == 0)
            {
                statusMessage = "No matched objects to deselect.";
                statusType = MessageType.Warning;
                return;
            }

            var remaining = Selection.gameObjects
                .Where(g => g != null && !matchedSet.Contains(g))
                .Cast<Object>()
                .ToArray();

            Selection.objects = remaining;
            statusMessage = "Deselected matched objects from current selection.";
            statusType = MessageType.Info;
        }
    }
}
