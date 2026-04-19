using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LogicCuteGuy.Editor
{
    public class RemapComponentReferencesByPattern : LogicCuteGuyEditorWindow
    {
        private enum SearchMode
        {
            ExactName,
            Contains,
            StartsWith,
            EndsWith,
            All
        }

        private enum MatchMode
        {
            HierarchyPath,
            NameAndType,
            NameOnly
        }

        private SearchMode searchMode = SearchMode.All;
        private MatchMode matchMode = MatchMode.HierarchyPath;
        private string componentNamePattern = "";
        private bool includeInactive = true;
        private bool dryRun = false;
        private Transform parentRef;

        [MenuItem("CONTEXT/MonoBehaviour/LogicCuteGuy/Open Fix Cloned Component References...")]
        private static void OpenFromComponentContext(MenuCommand command)
        {
            var component = command.context as Component;
            OpenWindowInternal(component != null ? component.gameObject : null);
        }

        private static RemapComponentReferencesByPattern OpenWindowInternal(GameObject root)
        {
            if (root != null)
            {
                Selection.activeGameObject = root;
            }

            var wnd = GetWindow<RemapComponentReferencesByPattern>(
                T("Fix Cloned Refs", "複製後の参照を修正", "แก้ไขการอ้างอิงที่โคลน"));
            wnd.minSize = new Vector2(420f, 240f);
            return wnd;
        }

        protected override void OnWindowGUI()
        {
            EditorGUILayout.LabelField(
                T("Remap object references after clone/copy by matching objects inside the selected root.",
                    "複製/コピー後、選択ルート内のオブジェクト照合を利用し参照を再接続します。",
                    "รีแมปการอ้างอิงออบเจ็กต์หลังจากสร้างโคลน/คัดลอกโดยจับคู่ออบเจ็กต์ในรูตที่เลือก"),
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                T(
                    "Select the cloned root object in Hierarchy, then run this tool. It replaces references that still point to the old source hierarchy.",
                    "Hierarchyで複製したルートオブジェクトを選択後、このツールを実行。元の階層を指す参照を自動置換します。",
                    "เลือกออบเจ็กต์รูตที่โคลนใน Hierarchy แล้วเรียกใช้เครื่องมือนี้ มันจะแทนที่การอ้างอิงที่ยังชี้ไปที่ลำดับชั้นต้นทางเก่า"),
                MessageType.Info);

            parentRef = (Transform)EditorGUILayout.ObjectField(T("Source Root (Optional)", "ソースルート (任意)", "รูตต้นทาง (ไม่บังคับ)"), parentRef, typeof(Transform),
                true);
            searchMode = (SearchMode)LCG_EnumPopup(T("Component Filter", "コンポーネントフィルター", "ตัวกรองส่วนประกอบ"),
                searchMode,
                new string[] { "Exact Name", "Contains", "Starts With", "Ends With", "All" },
                new string[] { "完全一致", "含む", "前方一致", "後方一致", "すべて" },
                new string[] { "ชื่อตรงกัน", "มีคำว่า", "ขึ้นต้นด้วย", "ลงท้ายด้วย", "ทั้งหมด" });
            componentNamePattern =
                EditorGUILayout.TextField(T("Component Name Pattern", "コンポーネント名のパターン", "รูปแบบชื่อส่วนประกอบ"),
                    componentNamePattern);
            matchMode = (MatchMode)LCG_EnumPopup(T("Remap Match", "リマップ一致", "รีแมปที่ตรงกัน"), matchMode,
                new string[] { "Hierarchy Path", "Name And Type", "Name Only" },
                new string[] { "ヒエラルキーパス", "名前と型", "名前のみ" },
                new string[] { "เส้นทางลำดับชั้น", "ชื่อและประเภท", "ชื่อเท่านั้น" });
            includeInactive = EditorGUILayout.Toggle(T("Include Inactive", "非アクティブを含める", "รวมรายการที่ไม่ได้ใช้งาน"),
                includeInactive);
            dryRun = EditorGUILayout.Toggle(T("Dry Run (No Write)", "ドライラン（書き込みなし）", "ทดสอบการทำงาน (ไม่เขียน)"),
                dryRun);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
            {
                if (GUILayout.Button(dryRun ? "Preview Remap" : "Remap References"))
                {
                    RemapReferences();
                }
            }

            if (Selection.activeGameObject == null)
            {
                EditorGUILayout.HelpBox(
                    T("Select a cloned root GameObject in Hierarchy first.", "最初にHierarchyで複製されたルートオブジェクトを選択してください。",
                        "เลือก GameObject รูตที่โคลนใน Hierarchy เป็นอันดับแรก"), MessageType.Warning);
            }
        }

        private void RemapReferences()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog(T("No Selection", "未選択", "ไม่ได้เลือก"),
                    T("Select a root GameObject first.", "最初にルートオブジェクトを選択してください。", "เลือก GameObject รูตก่อน"), "OK");
                return;
            }

            Component[] components = root.GetComponentsInChildren<Component>(includeInactive);
            if (components == null || components.Length == 0)
            {
                EditorUtility.DisplayDialog(T("No Components", "コンポーネントなし", "ไม่มีส่วนประกอบ"),
                    T("No components found under selected root.", "選択されたルートの配下にコンポーネントが見つかりませんでした。",
                        "ไม่พบส่วนประกอบภายใต้รูตที่เลือก"), "OK");
                return;
            }

            var pathLookup = BuildPathLookup(root.transform, includeInactive);
            var nameLookup = BuildNameLookup(root.transform, includeInactive);

            int changedFields = 0;
            int changedComponents = 0;
            int scannedFields = 0;

            Undo.SetCurrentGroupName("Remap Cloned Component References");
            int undoGroup = Undo.GetCurrentGroup();

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                if (!PassComponentFilter(component))
                {
                    continue;
                }

                SerializedObject so = new SerializedObject(component);
                SerializedProperty prop = so.GetIterator();

                bool enterChildren = true;
                bool componentChanged = false;

                while (prop.NextVisible(enterChildren))
                {
                    enterChildren = true;

                    if (prop.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    Object currentRef = prop.objectReferenceValue;
                    if (currentRef == null)
                    {
                        continue;
                    }

                    scannedFields++;

                    Transform sourceTransform = ExtractTransform(currentRef);
                    if (sourceTransform == null)
                    {
                        continue;
                    }

                    if (IsDescendantOf(sourceTransform, root.transform))
                    {
                        continue;
                    }

                    Object remappedRef =
                        TryRemapReference(currentRef, sourceTransform, pathLookup, nameLookup, parentRef);
                    if (remappedRef == null || remappedRef == currentRef)
                    {
                        continue;
                    }

                    if (!dryRun)
                    {
                        Undo.RecordObject(component,
                            T("Remap Component Reference", "コンポーネント参照の再マッピング", "รีแมปอ้างอิงส่วนประกอบ"));
                        prop.objectReferenceValue = remappedRef;
                        componentChanged = true;
                    }

                    changedFields++;
                }

                if (!dryRun && componentChanged)
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(component);
                    changedComponents++;
                }
            }

            if (!dryRun)
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            string title = dryRun ? "Preview Complete" : "Remap Complete";
            string msg = "Scanned object-reference fields: " + scannedFields + "\n" +
                         "Changed fields: " + changedFields + "\n" +
                         "Changed components: " + changedComponents + "\n" +
                         "Root: " + root.name;
            EditorUtility.DisplayDialog(title, msg, "OK");
        }

        private bool PassComponentFilter(Component component)
        {
            if (searchMode == SearchMode.All)
            {
                return true;
            }

            string componentName = component.GetType().Name;
            string pattern = componentNamePattern != null ? componentNamePattern : "";
            if (string.IsNullOrEmpty(pattern))
            {
                return true;
            }

            switch (searchMode)
            {
                case SearchMode.ExactName:
                    return componentName == pattern;
                case SearchMode.Contains:
                    return componentName.Contains(pattern);
                case SearchMode.StartsWith:
                    return componentName.StartsWith(pattern);
                case SearchMode.EndsWith:
                    return componentName.EndsWith(pattern);
                default:
                    return true;
            }
        }

        private static Dictionary<string, Transform> BuildPathLookup(Transform root, bool includeInactiveChildren)
        {
            var map = new Dictionary<string, Transform>();
            Transform[] transforms = root.GetComponentsInChildren<Transform>(includeInactiveChildren);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                string path = GetRelativePath(root, t);
                if (!map.ContainsKey(path))
                {
                    map.Add(path, t);
                }
            }

            return map;
        }

        private static Dictionary<string, List<Transform>> BuildNameLookup(Transform root, bool includeInactiveChildren)
        {
            var map = new Dictionary<string, List<Transform>>();
            Transform[] transforms = root.GetComponentsInChildren<Transform>(includeInactiveChildren);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                List<Transform> list;
                if (!map.TryGetValue(t.name, out list))
                {
                    list = new List<Transform>();
                    map.Add(t.name, list);
                }

                list.Add(t);
            }

            return map;
        }

        private Object TryRemapReference(Object currentRef, Transform sourceTransform,
            Dictionary<string, Transform> pathLookup, Dictionary<string, List<Transform>> nameLookup,
            Transform sourceRoot)
        {
            Transform targetTransform = null;

            if (matchMode == MatchMode.HierarchyPath)
            {
                if (sourceRoot != null && IsDescendantOf(sourceTransform, sourceRoot))
                {
                    string relativePath = GetRelativePath(sourceRoot, sourceTransform);
                    if (pathLookup.TryGetValue(relativePath, out var exactMatch))
                    {
                        targetTransform = exactMatch;
                    }
                }

                if (targetTransform == null)
                {
                    targetTransform = FindByPathSuffix(sourceTransform, pathLookup);
                }

                if (targetTransform == null)
                {
                    targetTransform = FindByNameAndDepth(sourceTransform, pathLookup);
                }
            }
            else
            {
                List<Transform> candidates;
                if (nameLookup.TryGetValue(sourceTransform.name, out candidates) && candidates.Count > 0)
                {
                    if (matchMode == MatchMode.NameOnly)
                    {
                        targetTransform = candidates[0];
                    }
                    else if (matchMode == MatchMode.NameAndType)
                    {
                        targetTransform = FindBestTypeMatchedTransform(currentRef, candidates);
                    }
                }
            }

            if (targetTransform == null)
            {
                return null;
            }

            if (currentRef is GameObject)
            {
                return targetTransform.gameObject;
            }

            if (currentRef is Transform)
            {
                return targetTransform;
            }

            Component sourceComponent = currentRef as Component;
            if (sourceComponent == null)
            {
                return null;
            }

            return RemapComponentByTypeAndOrder(sourceComponent, targetTransform);
        }

        private static Component RemapComponentByTypeAndOrder(Component sourceComponent, Transform targetTransform)
        {
            if (sourceComponent == null || targetTransform == null)
            {
                return null;
            }

            var sourceType = sourceComponent.GetType();
            Component[] sourceComps = sourceComponent.gameObject.GetComponents(sourceType);
            Component[] targetComps = targetTransform.gameObject.GetComponents(sourceType);

            if (targetComps == null || targetComps.Length == 0)
            {
                return null;
            }

            if (sourceComps == null || sourceComps.Length == 0)
            {
                return targetComps[0];
            }

            int sourceIndex = 0;
            for (int i = 0; i < sourceComps.Length; i++)
            {
                if (sourceComps[i] == sourceComponent)
                {
                    sourceIndex = i;
                    break;
                }
            }

            if (sourceIndex >= 0 && sourceIndex < targetComps.Length)
            {
                return targetComps[sourceIndex];
            }

            return targetComps[0];
        }

        private static Transform FindBestTypeMatchedTransform(Object currentRef, List<Transform> candidates)
        {
            Component sourceComponent = currentRef as Component;
            if (sourceComponent == null)
            {
                return candidates[0];
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].GetComponent(sourceComponent.GetType()) != null)
                {
                    return candidates[i];
                }
            }

            return candidates[0];
        }

        private static Transform FindByNameAndDepth(Transform source, Dictionary<string, Transform> pathLookup)
        {
            string path = source.name;
            if (pathLookup.TryGetValue(path, out var found))
            {
                return found;
            }

            return null;
        }

        private static Transform FindByPathSuffix(Transform source, Dictionary<string, Transform> pathLookup)
        {
            string sourcePath = GetPathFromRootToTop(source);
            if (string.IsNullOrEmpty(sourcePath))
            {
                return null;
            }

            string[] segments = sourcePath.Split('/');

            // Try longest suffix first so "Slot 0/Icon" wins over just "Icon".
            for (int start = 0; start < segments.Length; start++)
            {
                string suffix = JoinSegments(segments, start);
                if (pathLookup.TryGetValue(suffix, out var found))
                {
                    return found;
                }
            }

            return null;
        }

        private static Transform ExtractTransform(Object obj)
        {
            if (obj is GameObject)
            {
                return ((GameObject)obj).transform;
            }

            if (obj is Component)
            {
                return ((Component)obj).transform;
            }

            return null;
        }

        private static bool IsDescendantOf(Transform t, Transform parent)
        {
            Transform current = t;
            while (current != null)
            {
                if (current == parent)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (target == root)
            {
                return "";
            }

            var stack = new Stack<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                stack.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", stack.ToArray());
        }

        private static string GetPathFromRootToTop(Transform leaf)
        {
            var stack = new Stack<string>();
            Transform current = leaf;
            while (current != null)
            {
                stack.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", stack.ToArray());
        }

        private static string JoinSegments(string[] segments, int start)
        {
            if (segments == null || segments.Length == 0 || start < 0 || start >= segments.Length)
            {
                return "";
            }

            string result = segments[start];
            for (int i = start + 1; i < segments.Length; i++)
            {
                result += "/" + segments[i];
            }

            return result;
        }
    }
}
