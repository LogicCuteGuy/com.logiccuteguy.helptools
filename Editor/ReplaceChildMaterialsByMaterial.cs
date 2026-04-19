using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LogicCuteGuy.Editor
{
    public class ReplaceChildMaterialsByMaterial : LogicCuteGuyEditorWindow
    {
        private enum MaterialAction
        {
            Replace,
            Remove
        }

        private MaterialAction materialAction = MaterialAction.Replace;
        private Material targetMaterial;
        private Material replacementMaterial;
        private bool includeInactive = true;

        [MenuItem("GameObject/LogicCuteGuy/Replace Or Remove Child Materials")]
        private static void ShowWindow()
        {
            ReplaceChildMaterialsByMaterial window =
                GetWindow<ReplaceChildMaterialsByMaterial>(T("Child Materials", "子のマテリアル", "วัสดุของลูก"));
            window.minSize = new Vector2(420f, 220f);
        }

        protected override void OnWindowGUI()
        {
            EditorGUILayout.HelpBox(
                T(
                    "Replace or remove a target material from MeshRenderer components in selected objects and all nested children.",
                    "選択オブジェクトおよびその配下のMeshRendererに対して、指定のマテリアルを置換または削除します。",
                    "แทนที่หรือลบวัสดุเป้าหมายจากส่วนประกอบ MeshRenderer ในออบเจ็กต์ที่เลือกและลูกที่ซ้อนกันทั้งหมด"),
                MessageType.Info);

            materialAction = (MaterialAction)LCG_EnumPopup(T("Action", "アクション", "การดำเนินการ"), materialAction,
                new string[] { "Replace", "Remove" },
                new string[] { "置換", "削除" },
                new string[] { "แทนที่", "ลบ" });
            targetMaterial =
                (Material)EditorGUILayout.ObjectField(T("Target Material", "対象のマテリアル", "วัสดุเป้าหมาย"), targetMaterial, typeof(Material), false);

            if (materialAction == MaterialAction.Replace)
            {
                replacementMaterial = (Material)EditorGUILayout.ObjectField(T("Replacement Material", "置換後のマテリアル", "วัสดุทดแทน"), replacementMaterial,
                    typeof(Material), false);
                EditorGUILayout.HelpBox(
                    T("Replacement Material can be None to clear matching material slots.",
                        "置換後のマテリアルをNoneにするとスロットをクリアできます。", "วัสดุทดแทนสามารถเป็น None เพื่อล้างสล็อตวัสดุที่ตรงกัน"),
                    MessageType.None);
            }

            includeInactive = EditorGUILayout.Toggle(T("Include Inactive", "非アクティブを含める", "รวมรายการที่ไม่ได้ใช้งาน"),
                includeInactive);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(!CanApply()))
            {
                if (GUILayout.Button(T("Apply To Selected", "選択オブジェクトに適用", "นำไปใช้กับสิ่งที่เลือก"),
                        GUILayout.Height(30f)))
                {
                    ApplyToSelection();
                }
            }

            if (Selection.gameObjects.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    T("Select one or more parent GameObjects in the Hierarchy.", "ヒエラルキーで1つ以上の親オブジェクトを選択してください。",
                        "เลือก GameObjects พาเรนต์หนึ่งรายการขึ้นไปในลำดับชั้น"), MessageType.None);
            }
        }

        private bool CanApply()
        {
            if (Selection.gameObjects.Length == 0 || targetMaterial == null)
            {
                return false;
            }

            return true;
        }

        private void ApplyToSelection()
        {
            int rendererCount = 0;
            int materialSlotCount = 0;
            HashSet<SceneAssetKey> dirtyScenes = new HashSet<SceneAssetKey>();

            foreach (GameObject selectedObject in Selection.gameObjects)
            {
                MeshRenderer[] renderers = selectedObject.GetComponentsInChildren<MeshRenderer>(includeInactive);
                foreach (MeshRenderer meshRenderer in renderers)
                {
                    if (!ProcessRenderer(meshRenderer, ref materialSlotCount))
                    {
                        continue;
                    }

                    rendererCount++;
                    dirtyScenes.Add(new SceneAssetKey(meshRenderer.gameObject.scene));
                }
            }

            foreach (SceneAssetKey sceneKey in dirtyScenes)
            {
                if (sceneKey.Scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(sceneKey.Scene);
                }
            }

            Debug.Log(
                $"{materialAction} finished. Updated {rendererCount} renderer(s) and {materialSlotCount} material slot(s).");
        }

        private bool ProcessRenderer(MeshRenderer meshRenderer, ref int materialSlotCount)
        {
            Material[] materials = meshRenderer.sharedMaterials;
            bool changed = false;

            if (materialAction == MaterialAction.Replace)
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != targetMaterial)
                    {
                        continue;
                    }

                    if (!changed)
                    {
                        Undo.RecordObject(meshRenderer, T("Replace Child Materials", "子のマテリアルを置換", "เปลี่ยนวัสดุลูก"));
                        changed = true;
                    }

                    materials[i] = replacementMaterial;
                    materialSlotCount++;
                }
            }
            else if (materialAction == MaterialAction.Remove)
            {
                List<Material> filteredMaterials = new List<Material>(materials.Length);
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == targetMaterial)
                    {
                        if (!changed)
                        {
                            Undo.RecordObject(meshRenderer, T("Remove Child Materials", "子のマテリアルを削除", "ลบวัสดุลูก"));
                            changed = true;
                        }

                        materialSlotCount++;
                        continue;
                    }

                    filteredMaterials.Add(materials[i]);
                }

                if (changed)
                {
                    materials = filteredMaterials.ToArray();
                }
            }

            if (!changed)
            {
                return false;
            }

            meshRenderer.sharedMaterials = materials;
            EditorUtility.SetDirty(meshRenderer);
            return true;
        }

        private readonly struct SceneAssetKey
        {
            public readonly UnityEngine.SceneManagement.Scene Scene;

            public SceneAssetKey(UnityEngine.SceneManagement.Scene scene)
            {
                Scene = scene;
            }
        }
    }
}