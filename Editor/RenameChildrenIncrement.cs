using UnityEditor;
using UnityEngine;

namespace LogicCuteGuy.Editor
{
    public class RenameChildrenIncrement : LogicCuteGuyEditorWindow
    {
        string pattern = "{0}";
        int startIndex = 0;
        bool includeInactive = false;

        [MenuItem("GameObject/LogicCuteGuy/Rename Children Increment...")]
        static void OpenWindow()
        {
            var wnd = GetWindow<RenameChildrenIncrement>(T("Rename Children Increment", "子の連番リネーム",
                "เปลี่ยนชื่อลูกแบบเพิ่มตัวเลข"));
            wnd.minSize = new Vector2(320, 120);
        }

        protected override void OnWindowGUI()
        {
            EditorGUILayout.LabelField(
                T("Rename children of the selected GameObject using a pattern", "パターンを使用して、選択したオブジェクトの配下の名前を変更します。",
                    "เปลี่ยนชื่อลูกของ GameObject ที่เลือกโดยใช้รูปแบบ"),
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            pattern = EditorGUILayout.TextField(T("Pattern", "パターン", "รูปแบบ"), pattern);
            EditorGUILayout.HelpBox(
                T("Use {0} where the child index should be inserted. Example: Slot {0}",
                    "{0} をインデックス挿入箇所として使用します。 例: スロット {0}", "ใช้ {0} ในตำแหน่งที่จะแทรกดัชนี ตัวอย่าง: สล็อต {0}"),
                MessageType.None);
            startIndex = EditorGUILayout.IntField(T("Start Index", "開始インデックス", "ดัชนีเริ่มต้น"), startIndex);
            includeInactive = EditorGUILayout.Toggle(T("Include Inactive", "非アクティブを含める", "รวมรายการที่ไม่ได้ใช้งาน"),
                includeInactive);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
            {
                if (GUILayout.Button(T("Rename Children", "子の名前を変更", "เปลี่ยนชื่อลูกทั้งหมด")))
                {
                    if (!pattern.Contains("{0}"))
                    {
                        EditorUtility.DisplayDialog(T("Invalid Pattern", "無効なパターン", "รูปแบบไม่ถูกต้อง"),
                            T("Pattern must contain {0} to insert the index.", "パターンにはインデックスを挿入するための {0} を含める必要があります。",
                                "รูปแบบต้องมี {0} เพื่อแทรกดัชนี"),
                            "OK");
                    }
                    else
                    {
                        RenameChildren();
                    }
                }
            }

            if (Selection.activeGameObject == null)
            {
                EditorGUILayout.HelpBox(
                    T("Select a parent GameObject in the Hierarchy first.", "最初にHierarchyで親オブジェクトを選択してください。",
                        "เลือก GameObject พาเรนต์ใน Hierarchy ก่อน"), MessageType.Info);
            }
        }

        void RenameChildren()
        {
            var parent = Selection.activeGameObject;
            if (parent == null) return;

            int index = startIndex;
            var children = parent.transform;

            // Record undo for the parent so rename operation can be undone in one step
            Undo.RegisterCompleteObjectUndo(parent, T("Rename Children", "子の名前を変更", "เปลี่ยนชื่อลูกทั้งหมด"));

            for (int i = 0; i < children.childCount; i++)
            {
                var child = children.GetChild(i).gameObject;
                if (!includeInactive && !child.activeInHierarchy) continue;
                Undo.RecordObject(child, T("Rename Child", "子の名前を一括変更", "เปลี่ยนชื่อลูก"));
                child.name = string.Format(pattern, index);
                index++;
                EditorUtility.SetDirty(child);
            }

            // Close window after operation
            Close();
        }
    }
}
