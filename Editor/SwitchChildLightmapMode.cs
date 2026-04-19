using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicCuteGuy.Editor
{
    public class SwitchChildLightmapMode : LogicCuteGuyEditorWindow
    {
        private enum LightingMode
        {
            Lightmap,
            LightProbes
        }

        private GameObject targetObject;
        private LightingMode lightingMode = LightingMode.LightProbes;
        private bool applyToSelectedObjects = false;
        private bool applyToChildrenRecursive = true;
        private bool includeInactiveChildren = true;
        private bool forceMeshRendererScaleInLightmap = false;
        private float scaleInLightmapWhenEnabled = 1f;
        private float scaleInLightmapWhenDisabled = 0f;

        [MenuItem("CONTEXT/MeshRenderer/LogicCuteGuy/Switch Child Lightmap Mode...")]
        public static void ShowWindowFromContext(MenuCommand command)
        {
            SwitchChildLightmapMode window =
                GetWindow<SwitchChildLightmapMode>(T("Child Lightmap Mode", "子のライトマップモード", "โหมดไลท์แมปของลูก"));
            MeshRenderer clickedRenderer = command.context as MeshRenderer;
            if (clickedRenderer != null)
            {
                window.targetObject = clickedRenderer.gameObject;
            }
        }

        protected override void OnWindowGUI()
        {
            GUILayout.Label(
                T("Switch Child Lightmap / Light Probes", "子のライトマップ/ライトプローブ切替", "สลับไลท์แมป/ไลท์โพรบของลูก"),
                EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                T(
                    "Recursively switches all child renderers under a parent between baked Lightmap mode and Light Probes mode.",
                    "親の配下にある全レンダラーのモードをLightmap（ベイク済み）とLight Probes間で切り替えます。",
                    "สลับโหมด Lightmap ที่อบและชุด Light Probes สำหรับตัวเรนเดอร์ย่อยทั้งหมดที่อยู่ภายใต้พาเรนต์แบบวนซ้ำ"),
                MessageType.Info);

            targetObject =
                (GameObject)EditorGUILayout.ObjectField(T("Target Object", "対象のオブジェクト", "ออบเจ็กต์เป้าหมาย"),
                    targetObject, typeof(GameObject), true);

            EditorGUILayout.Space();

            applyToSelectedObjects =
                EditorGUILayout.Toggle(T("Apply To Selected Objects", "選択したオブジェクトに適用", "ใช้กับออบเจ็กต์ที่เลือก"),
                    applyToSelectedObjects);
            applyToChildrenRecursive =
                EditorGUILayout.Toggle(T("Apply To Children Recursive", "子に再帰的に適用", "ใช้กับลูกแบบวนซ้ำ"),
                    applyToChildrenRecursive);
            includeInactiveChildren =
                EditorGUILayout.Toggle(T("Include Inactive", "非アクティブを含める", "รวมรายการที่ไม่ได้ใช้งาน"),
                    includeInactiveChildren);

            EditorGUILayout.Space();

            lightingMode = (LightingMode)LCG_EnumPopup(T("Lighting Mode", "ライティングモード", "โหมดแสงสว่าง"), lightingMode,
                new string[] { "Lightmap", "Light Probes" },
                new string[] { "ライトマップ", "ライトプローブ" },
                new string[] { "ไลท์แมป", "ไลท์โพรบ" });
            forceMeshRendererScaleInLightmap =
                EditorGUILayout.Toggle(T("Set Scale In Lightmap", "ライトマップ用スケールを設定", "ตั้งสเกลในไลท์แมป"),
                    forceMeshRendererScaleInLightmap);

            using (new EditorGUI.DisabledScope(!forceMeshRendererScaleInLightmap))
            {
                scaleInLightmapWhenEnabled =
                    EditorGUILayout.FloatField(T("Scale When Lightmapped", "ライトマップ時のスケール", "สเกลเมื่อใช้ไลท์แมป"),
                        scaleInLightmapWhenEnabled);
                scaleInLightmapWhenDisabled =
                    EditorGUILayout.FloatField(T("Scale When Probe Lit", "プローブ時のスケール", "สเกลเมื่อใช้โพรบ"),
                        scaleInLightmapWhenDisabled);
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(targetObject == null && Selection.gameObjects.Length == 0))
            {
                if (GUILayout.Button(
                        T("Apply To Target Or Selected", "ターゲットまたは選択オブジェクトに適用", "ใช้กับเป้าหมายหรือสิ่งที่เลือก"),
                        GUILayout.Height(30)))
                {
                    List<GameObject> combinedTargets = new List<GameObject>(Selection.gameObjects);
                    if (targetObject != null && !combinedTargets.Contains(targetObject))
                    {
                        combinedTargets.Add(targetObject);
                    }

                    ApplyToTargets(combinedTargets.ToArray());
                }
            }

            if (targetObject == null && Selection.gameObjects.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    T("Select one or more GameObjects in the Hierarchy or assign a Target Object.",
                        "ヒエラルキーから1つ以上のGameObjectを選択するか、ターゲットオブジェクトを割り当ててください。",
                        "เลือก GameObjects หนึ่งรายการขึ้นไปในลำดับชั้นหรือกำหนดออบเจ็กต์เป้าหมาย"), MessageType.None);
            }
        }

        private void ApplyToTargets(GameObject[] targets)
        {
            if (targets == null || targets.Length == 0)
            {
                EditorUtility.DisplayDialog(T("No Selection", "未選択", "ไม่ได้เลือก"),
                    T("No objects provided. Select one or more objects first.", "オブジェクトがありません。最初に1つ以上のオブジェクトを選択してください。",
                        "ไม่มีออบเจ็กต์ เลือกหนึ่งออบเจ็กต์ขึ้นไปก่อน"), "OK");
                return;
            }

            HashSet<Renderer> uniqueRenderers = new HashSet<Renderer>();

            foreach (GameObject target in targets)
            {
                if (applyToSelectedObjects)
                {
                    Renderer r = target.GetComponent<Renderer>();
                    if (r != null && (includeInactiveChildren || r.gameObject.activeInHierarchy))
                    {
                        uniqueRenderers.Add(r);
                    }
                }

                if (applyToChildrenRecursive)
                {
                    Renderer[] childRenderers = target.GetComponentsInChildren<Renderer>(includeInactiveChildren);
                    foreach (Renderer child in childRenderers)
                    {
                        if (child.gameObject != target)
                        {
                            uniqueRenderers.Add(child);
                        }
                    }
                }
            }

            if (uniqueRenderers.Count == 0)
            {
                EditorUtility.DisplayDialog(T("No Renderers", "レンダラーなし", "ไม่มีตัวเรนเดอร์"),
                    T("Found no valid renderers to update.", "更新可能なレンダラーが見つかりませんでした。",
                        "ไม่พบตัวเรนเดอร์ที่ถูกต้องในการอัปเดต"), "OK");
                return;
            }

            List<Renderer> rendererList = new List<Renderer>(uniqueRenderers);
            Undo.RecordObjects(rendererList.ToArray(), "Switch Child Lightmap Mode");
            Undo.RecordObjects(CollectStaticFlagTargets(rendererList).ToArray(), "Switch Child Lightmap Mode");

            int changed = 0;
            foreach (Renderer renderer in rendererList)
            {
                if (renderer == null) continue;

                ApplyLightingMode(renderer);
                EditorUtility.SetDirty(renderer);
                changed++;
            }

            EditorUtility.DisplayDialog(
                T("Lighting Mode Updated", "ライティングモードを更新しました", "อัปเดตโหมดแสงสว่างแล้ว"),
                T("Successfully updated ", "正常に更新されました: ", "อัปเดตสำเร็จแล้ว: ") + changed +
                " combined unique renderer(s).",
                "OK");
        }

        private List<GameObject> CollectStaticFlagTargets(List<Renderer> renderers)
        {
            List<GameObject> result = new List<GameObject>(renderers.Count);
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                GameObject go = renderer.gameObject;
                if (!result.Contains(go))
                {
                    result.Add(go);
                }
            }

            return result;
        }

        private void ApplyLightingMode(Renderer renderer)
        {
            GameObject target = renderer.gameObject;

            if (lightingMode == LightingMode.Lightmap)
            {
                GameObjectUtility.SetStaticEditorFlags(
                    target,
                    GameObjectUtility.GetStaticEditorFlags(target) | StaticEditorFlags.ContributeGI);

                if (renderer is MeshRenderer meshRenderer)
                {
                    meshRenderer.receiveGI = ReceiveGI.Lightmaps;
                    if (forceMeshRendererScaleInLightmap)
                    {
                        SetScaleInLightmap(meshRenderer, scaleInLightmapWhenEnabled);
                    }
                }

                renderer.lightProbeUsage = LightProbeUsage.Off;
                return;
            }

            GameObjectUtility.SetStaticEditorFlags(
                target,
                GameObjectUtility.GetStaticEditorFlags(target) & ~StaticEditorFlags.ContributeGI);

            if (renderer is MeshRenderer probeRenderer)
            {
                probeRenderer.receiveGI = ReceiveGI.LightProbes;
                if (forceMeshRendererScaleInLightmap)
                {
                    SetScaleInLightmap(probeRenderer, scaleInLightmapWhenDisabled);
                }
            }

            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        }

        private static void SetScaleInLightmap(Renderer renderer, float value)
        {
            SerializedObject serializedObject = new SerializedObject(renderer);
            SerializedProperty scaleInLightmap = serializedObject.FindProperty("m_ScaleInLightmap");
            if (scaleInLightmap == null)
            {
                return;
            }

            scaleInLightmap.floatValue = value;
            serializedObject.ApplyModifiedProperties();
        }
    }
}