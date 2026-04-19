using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LogicCuteGuy.Editor
{
    public class PasteAsChildToAllParents : LogicCuteGuyEditorWindow
    {
        List<GameObject> sources = new List<GameObject>();
        Vector2 scroll;

        enum PasteMode
        {
            PreserveWorld,
            CopySourceLocal,
            ResetLocal
        }

        PasteMode pasteMode = PasteMode.PreserveWorld;

        [MenuItem("GameObject/LogicCuteGuy/Paste As Child To All Selected Parents...")]
        static void OpenWindow()
        {
            var wnd = GetWindow<PasteAsChildToAllParents>(T("Paste As Child To Parents", "子として親にペースト",
                "วางเป็นลูกไปยังพาเรนต์"));
            wnd.minSize = new Vector2(360, 220);
        }

        protected override void OnWindowGUI()
        {
            EditorGUILayout.LabelField(
                T("Store source objects then paste copies as children into all selected parents.",
                    "ソースオブジェクトを一時保存し、選択したすべての親オブジェクトの子としてコピーをペーストします。",
                    "บันทึกออบเจ็กต์ต้นทางแล้ววางสำเนาเป็นลูกในพาเรนต์ที่เลือกทั้งหมด"),
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(T("Set Sources From Selection", "選択からソースを設定", "ตั้งค่าต้นทางจากสิ่งที่เลือก")))
                    SetSourcesFromSelection();
                if (GUILayout.Button(T("Clear Sources", "ソースをクリア", "ล้างแหล่งอ้างอิง"))) sources.Clear();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(T("Sources:", "ソース:", "ต้นทาง:"), EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(80));
            for (int i = 0; i < sources.Count; i++)
            {
                sources[i] = (GameObject)EditorGUILayout.ObjectField(sources[i], typeof(GameObject), true);
            }

            EditorGUILayout.EndScrollView();

            pasteMode = (PasteMode)LCG_EnumPopup(T("Paste Mode", "ペーストモード", "โหมดการวาง"), pasteMode,
                new string[] { "Preserve World", "Copy Source Local", "Reset Local" },
                new string[] { "世界座標を維持", "ソースのローカルをコピー", "ローカルをリセット" },
                new string[] { "รักษาทรานสฟอร์มโลก", "คัดลอกทรานสฟอร์มท้องถิ่น", "รีเซ็ตทรานสฟอร์มท้องถิ่น" });

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(sources.Count == 0))
            {
                if (GUILayout.Button(T("Paste To Selected Parents", "選択した親にペースト", "วางไปยังพาเรนต์ที่เลือก")))
                    PasteToSelectedParents();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                T(
                    "Usage: Select source objects and click 'Set Sources From Selection'. Then select one or more parent GameObjects and click 'Paste To Selected Parents'.\nPaste Mode: PreserveWorld keeps global transform, CopySourceLocal copies the source's local position/rotation/scale, ResetLocal sets local transform to zero/identity/one.",
                    "使用方法: ソースオブジェクトを選択後「選択からソースを設定」をクリック。次にペースト先の親GameObjectを選択し「選択した親にペースト」をクリックします。\nペーストモード: PreserveWorld（グローバルトランスフォーム維持）、CopySourceLocal（ソースのローカルPosition/Rotation/Scaleを反映）、ResetLocal（ローカルを0/identity/1に設定）",
                    "วิธีใช้: เลือกออบเจ็กต์แหล่งที่มาแล้วคลิก 'ตั้งต้นทางจากสิ่งที่เลือก' จากนั้นเลือก GameObjects พาเรนต์และคลิก 'วางไปยังพาเรนต์ที่เลือก'\nรูปแบบการวาง: PreserveWorld รักษาทรานสฟอร์มโลก, CopySourceLocal คัดลอกตำแหน่ง/การหมุน/สเกลในพื้นที่ออบเจ็กต์, ResetLocal รีเซ็ตทรานสฟอร์มเป็นศูนย์/ทิศเริ่มต้น/ปริมาตร 1"),
                MessageType.Info);
        }

        void SetSourcesFromSelection()
        {
            var sel = Selection.gameObjects;
            if (sel == null || sel.Length == 0)
            {
                EditorUtility.DisplayDialog(T("No Selection", "未選択", "ไม่ได้เลือก"),
                    T("Select one or more source GameObjects in the Hierarchy or Project window first.",
                        "最初にHierarchyかProjectウィンドウでソースオブジェクトを1つ以上選択してください。",
                        "เลือก GameObjects แหล่งที่มาหนึ่งรายการขึ้นไปในหน้าต่าง Hierarchy หรือ Project ก่อน"), "OK");
                return;
            }

            sources.Clear();
            foreach (var g in sel) sources.Add(g);
        }

        void PasteToSelectedParents()
        {
            var parents = Selection.gameObjects;
            if (parents == null || parents.Length == 0)
            {
                EditorUtility.DisplayDialog(T("No Parents Selected", "親が選択されていません", "ไม่ได้เลือกพาเรนต์"),
                    T("Select one or more parent GameObjects in the Hierarchy to paste into.",
                        "ペースト先となる親オブジェクトをヒエラルキーで1つ以上選択してください。",
                        "เลือก GameObjects พาเรนต์หนึ่งรายการขึ้นไปในลำดับชั้นที่จะวาง"), "OK");
                return;
            }

            var created = new List<GameObject>();

            Undo.SetCurrentGroupName("Paste As Child To Parents");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (var parent in parents)
            {
                foreach (var src in sources)
                {
                    if (src == null) continue;

                    GameObject inst = null;

                    // If the source is a prefab asset, instantiate preserving prefab connection
                    var prefabType = PrefabUtility.GetPrefabAssetType(src);
                    if (prefabType != PrefabAssetType.NotAPrefab)
                    {
                        var obj = PrefabUtility.InstantiatePrefab(src);
                        inst = obj as GameObject;
                    }
                    else
                    {
                        inst = Object.Instantiate(src);
                        inst.name = src.name;
                    }

                    if (inst == null) continue;

                    Undo.RegisterCreatedObjectUndo(inst, T("Paste As Child", "子としてペースト", "วางเป็นลูก"));

                    // Parent then apply chosen paste mode
                    if (pasteMode == PasteMode.PreserveWorld)
                    {
                        inst.transform.SetParent(parent.transform, true);
                    }
                    else
                    {
                        inst.transform.SetParent(parent.transform, false);
                        if (pasteMode == PasteMode.CopySourceLocal)
                        {
                            inst.transform.localPosition = src.transform.localPosition;
                            inst.transform.localRotation = src.transform.localRotation;
                            inst.transform.localScale = src.transform.localScale;
                        }
                        else // ResetLocal
                        {
                            inst.transform.localPosition = Vector3.zero;
                            inst.transform.localRotation = Quaternion.identity;
                            inst.transform.localScale = Vector3.one;
                        }
                    }

                    EditorUtility.SetDirty(inst);
                    created.Add(inst);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (created.Count > 0)
            {
                Selection.objects = created.ToArray();
                EditorGUIUtility.PingObject(created[0]);
            }
        }
    }
}
