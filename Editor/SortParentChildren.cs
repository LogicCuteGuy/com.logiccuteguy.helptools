using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LogicCuteGuy.Editor
{
    public class SortParentChildren : LogicCuteGuyEditorWindow
    {
        private enum SortMode
        {
            NameAZ,
            NameZA,
            LocalPosXAsc,
            LocalPosXDesc,
            LocalPosYAsc,
            LocalPosYDesc,
            LocalPosZAsc,
            LocalPosZDesc
        }

        private GameObject parentObject;
        private SortMode sortMode = SortMode.NameAZ;

        [MenuItem("GameObject/LogicCuteGuy/Sort Parent Children")]
        public static void ShowWindow()
        {
            GetWindow<SortParentChildren>(T("Sort Parent Children", "親の子要素をソート", "จัดเรียงลูกของพาเรนต์"));
        }

        private void OnEnable()
        {
            if (Selection.activeGameObject != null)
            {
                parentObject = Selection.activeGameObject;
            }
        }

        private void OnSelectionChange()
        {
            if (Selection.activeGameObject != null && Selection.activeGameObject != parentObject)
            {
                parentObject = Selection.activeGameObject;
                Repaint();
            }
        }

        protected override void OnWindowGUI()
        {
            GUILayout.Label(T("Sort Parent Children", "親の子要素をソート", "จัดเรียงลูกของพาเรนต์"), EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                T("Sort direct children of a parent object.\n", "親オブジェクト直下の子をソートします。\n",
                    "จัดเรียงลูกโดยตรงของออบเจ็กต์พาเรนต์\n") +
                T("You can sort by name (A-Z / Z-A) or by local position axis.", "名前 (A-Z / Z-A) またはローカル座標軸でソートできます。",
                    "คุณสามารถจัดเรียงตามชื่อ (A-Z / Z-A) หรือตามแกนตำแหน่งโลคอล"),
                MessageType.Info);

            parentObject = (GameObject)EditorGUILayout.ObjectField(T("Parent Object", "親オブジェクト", "ออบเจ็กต์พาเรนต์"),
                parentObject, typeof(GameObject), true);

            EditorGUILayout.Space();
            sortMode = (SortMode)LCG_EnumPopup(T("Sort Mode", "ソートモード", "โหมดการเรียงลำดับ"), sortMode,
                new string[]
                {
                    "Name A-Z", "Name Z-A", "Local Pos X Asc", "Local Pos X Desc", "Local Pos Y Asc",
                    "Local Pos Y Desc", "Local Pos Z Asc", "Local Pos Z Desc"
                },
                new string[]
                {
                    "名前 A-Z", "名前 Z-A", "ローカル座標 X (昇順)", "ローカル座標 X (降順)", "ローカル座標 Y (昇順)", "ローカル座標 Y (降順)",
                    "ローカル座標 Z (昇順)", "ローカル座標 Z (降順)"
                },
                new string[]
                {
                    "ชื่อ A-Z", "ชื่อ Z-A", "ตำแหน่ง X (น้อยไปมาก)", "ตำแหน่ง X (มากไปน้อย)", "ตำแหน่ง Y (น้อยไปมาก)",
                    "ตำแหน่ง Y (มากไปน้อย)", "ตำแหน่ง Z (น้อยไปมาก)", "ตำแหน่ง Z (มากไปน้อย)"
                });
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(parentObject == null))
            {
                if (GUILayout.Button(T("Sort Children", "子をソート", "เรียงลำดับลูก"), GUILayout.Height(30)))
                {
                    if (parentObject != null)
                    {
                        int sorted = SortParent(parentObject.transform, sortMode);
                        if (sorted >= 0)
                        {
                            EditorUtility.DisplayDialog(
                                "Sort Complete",
                                $"Sorted {sorted} children on '{parentObject.name}' using {GetModeLabel(sortMode)}.",
                                "OK");
                        }
                    }
                }
            }

            EditorGUILayout.Space();

            if (GUILayout.Button(T("Sort Selected Parents", "選択した親をソート", "เรียงลำดับพาเรนต์ที่เลือก")))
            {
                SortSelection(sortMode);
            }
        }

        private static void SortSelection(SortMode mode)
        {
            Transform[] parents = Selection.transforms
                .Where(t => t != null)
                .Distinct()
                .ToArray();

            if (parents.Length == 0)
            {
                EditorUtility.DisplayDialog(T("No Selection", "未選択", "ไม่ได้เลือก"),
                    T("Select one or more parent objects first.", "最初に1つ以上の親オブジェクトを選択してください。",
                        "เลือกออบเจ็กต์พาเรนต์หนึ่งรายการขึ้นไปก่อน"), "OK");
                return;
            }

            int parentCount = 0;
            int totalChildren = 0;

            foreach (Transform parent in parents)
            {
                int sorted = SortParent(parent, mode);
                if (sorted > 0)
                {
                    parentCount++;
                    totalChildren += sorted;
                }
            }

            EditorUtility.DisplayDialog(
                "Sort Complete",
                $"Sorted {totalChildren} children across {parentCount} selected parent(s) using {GetModeLabel(mode)}.",
                "OK");
        }

        private static int SortParent(Transform parent, SortMode mode)
        {
            if (parent == null)
                return -1;

            List<Transform> children = new List<Transform>();
            foreach (Transform child in parent)
            {
                children.Add(child);
            }

            if (children.Count < 2)
                return 0;

            Dictionary<Transform, int> originalOrder = new Dictionary<Transform, int>(children.Count);
            for (int i = 0; i < children.Count; i++)
            {
                originalOrder[children[i]] = i;
            }

            IOrderedEnumerable<Transform> orderedChildren;

            switch (mode)
            {
                case SortMode.NameAZ:
                    orderedChildren = children.OrderBy(t => t.name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(t => originalOrder[t]);
                    break;
                case SortMode.NameZA:
                    orderedChildren = children.OrderByDescending(t => t.name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(t => originalOrder[t]);
                    break;
                case SortMode.LocalPosXAsc:
                    orderedChildren = children.OrderBy(t => t.localPosition.x)
                        .ThenBy(t => originalOrder[t]);
                    break;
                case SortMode.LocalPosXDesc:
                    orderedChildren = children.OrderByDescending(t => t.localPosition.x)
                        .ThenBy(t => originalOrder[t]);
                    break;
                case SortMode.LocalPosYAsc:
                    orderedChildren = children.OrderBy(t => t.localPosition.y)
                        .ThenBy(t => originalOrder[t]);
                    break;
                case SortMode.LocalPosYDesc:
                    orderedChildren = children.OrderByDescending(t => t.localPosition.y)
                        .ThenBy(t => originalOrder[t]);
                    break;
                case SortMode.LocalPosZAsc:
                    orderedChildren = children.OrderBy(t => t.localPosition.z)
                        .ThenBy(t => originalOrder[t]);
                    break;
                case SortMode.LocalPosZDesc:
                    orderedChildren = children.OrderByDescending(t => t.localPosition.z)
                        .ThenBy(t => originalOrder[t]);
                    break;
                default:
                    orderedChildren = children.OrderBy(t => t.name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(t => originalOrder[t]);
                    break;
            }

            List<Transform> sortedList = orderedChildren.ToList();

            Undo.RecordObjects(sortedList.Cast<UnityEngine.Object>().ToArray(), "Sort Parent Children");
            Undo.RecordObject(parent, T("Sort Parent Children", "親の子要素をソート", "จัดเรียงลูกของพาเรนต์"));

            for (int i = 0; i < sortedList.Count; i++)
            {
                sortedList[i].SetSiblingIndex(i);
            }

            EditorUtility.SetDirty(parent);
            return sortedList.Count;
        }

        private static string GetModeLabel(SortMode mode)
        {
            switch (mode)
            {
                case SortMode.NameAZ:
                    return "Name A-Z";
                case SortMode.NameZA:
                    return "Name Z-A";
                case SortMode.LocalPosXAsc:
                    return "Local Position X Asc";
                case SortMode.LocalPosXDesc:
                    return "Local Position X Desc";
                case SortMode.LocalPosYAsc:
                    return "Local Position Y Asc";
                case SortMode.LocalPosYDesc:
                    return "Local Position Y Desc";
                case SortMode.LocalPosZAsc:
                    return "Local Position Z Asc";
                case SortMode.LocalPosZDesc:
                    return "Local Position Z Desc";
                default:
                    return mode.ToString();
            }
        }
    }
}
