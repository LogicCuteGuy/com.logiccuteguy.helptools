using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace LogicCuteGuy.Editor
{
    public enum ScaleMode
    {
        GlobalMultiplier,
        MinMaxRatio
    }

    public class AutoScaleInLightmap : LogicCuteGuyEditorWindow
    {
        private ScaleMode currentMode = ScaleMode.GlobalMultiplier;
        private float globalMultiplier = 1.0f; // Multiplier applied after normalizing
        private float maxScale = 2.0f; // Scale for smallest objects
        private float minScale = 0.1f; // Scale for largest objects
        private bool includeInactive = false;
        private bool applyToSelection = false;
        private bool useVolume = true; // Use volume instead of max dimension

        [MenuItem("GameObject/LogicCuteGuy/Auto Scale In Lightmap")]
        public static void ShowWindow()
        {
            GetWindow<AutoScaleInLightmap>(T("Auto Scale In Lightmap", "ライトマップのオートスケール", "ออโต้สเกลในไลท์แมป"));
        }

        protected override void OnWindowGUI()
        {
            GUILayout.Label(T("Auto Scale Settings", "オートスケールの設定", "การตั้งค่าออโต้สเกล"), EditorStyles.boldLabel);
            EditorGUILayout.Space();

            currentMode = (ScaleMode)LCG_EnumPopup(T("Scaling Mode", "スケールモード", "โหมดการปรับสเกล"), currentMode,
                new string[] { "Global Multiplier", "Min Max Ratio" },
                new string[] { "グローバル倍率", "最小最大比率" },
                new string[] { "ตัวคูณระดับโกลบอล", "อัตราส่วนขั้นต่ำสูงสุด" });
            EditorGUILayout.Space();

            if (currentMode == ScaleMode.GlobalMultiplier)
            {
                EditorGUILayout.HelpBox(
                    T("MATCH SCALE FIRST: Normalizes every object so they naturally use the same default lightmap space, regardless of physical size.\nSCALE LATER: Multiplies that normalized value by your Global Multiplier.", "MATCH SCALE FIRST: 物理的サイズに関係なく同じライトマップ空間を優先して正規化します。\nSCALE LATER: 正規化された値にグローバル乗数を掛けます。", "MATCH SCALE FIRST: แปลงทุกออบเจ็กต์ให้ใช้พื้นที่ไลท์แมปเริ่มต้นเดียวกัน ไม่ว่าขนาดกายภาพจะเป็นเท่าใด\nSCALE LATER: คูณค่านั้นด้วยตัวคูณระดับโกลบอลของคุณ"),
                    MessageType.Info);
                globalMultiplier = EditorGUILayout.FloatField(T("Global Scale Multiplier", "グローバルスケール乗数", "ตัวคูณสเกลระดับโกลบอล"), globalMultiplier);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    T("Automatically adjusts Scale In Lightmap based on the smallest and largest objects in the scene.\nSmallest objects get Max Scale, Largest objects get Min Scale.", "シーン内の最小・最大オブジェクトに基づいてライトマップ用スケールを自動調整します。\n最小オブジェクトが最大スケールに、最大オブジェクトが最小スケールになります。", "ปรับสเกลใน Lightmap อัตโนมัติตามออบเจ็กต์ที่เล็กและใหญ่ที่สุดในฉาก\nออบเจ็กต์ที่เล็กที่สุดจะได้รับสเกลสูงสุด ออบเจ็กต์ที่ใหญ่ที่สุดจะได้รับสเกลต่ำสุด"),
                    MessageType.Info);
                maxScale = EditorGUILayout.FloatField(T("Max Scale (Smallest Objects)", "最大スケール (最小オブジェクト)", "สเกลสูงสุด (ออบเจ็กต์ที่เล็กที่สุด)"), maxScale);
                minScale = EditorGUILayout.FloatField(T("Min Scale (Largest Objects)", "最小スケール (最大オブジェクト)", "สเกลต่ำสุด (ออบเจ็กต์ที่ใหญ่ที่สุด)"), minScale);
            }

            EditorGUILayout.Space();

            useVolume = EditorGUILayout.Toggle(T("Calculate by Volume", "体積で計算", "คำนวณตามปริมาตร"), useVolume);
            includeInactive = EditorGUILayout.Toggle(T("Include Inactive Objects", "非アクティブなオブジェクトを含める", "รวมออบเจ็กต์ที่ไม่ได้ใช้งาน"), includeInactive);
            applyToSelection = EditorGUILayout.Toggle(T("Apply to Selection Only", "選択のみに適用", "ใช้กับส่วนที่เลือกเท่านั้น"), applyToSelection);
            EditorGUILayout.Space();

            if (GUILayout.Button(T("Apply Auto Scale", "オートスケールを適用", "ใช้อัตโนมัติสเกล"), GUILayout.Height(30)))
            {
                ApplyAutoScale();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button(T("Reset All to 1.0", "すべて1.0にリセット", "รีเซ็ตทั้งหมดเป็น 1.0"), GUILayout.Height(25)))
            {
                ResetAllScales();
            }
        }

        private void ApplyAutoScale()
        {
            GameObject[] objects;

            if (applyToSelection && Selection.gameObjects.Length > 0)
            {
                // Get selected objects and their children
                List<GameObject> allObjects = new List<GameObject>();
                foreach (GameObject selected in Selection.gameObjects)
                {
                    allObjects.Add(selected);
                    allObjects.AddRange(selected.GetComponentsInChildren<MeshRenderer>(includeInactive)
                        .Select(mr => mr.gameObject));
                }

                objects = allObjects.Distinct().ToArray();
            }
            else
            {
                // Get all objects in scene
                objects = FindObjectsOfType<GameObject>(includeInactive);
            }

            var targetObjects = objects.Where(o => o.GetComponent<MeshRenderer>() != null).ToArray();

            if (targetObjects.Length == 0)
            {
                EditorUtility.DisplayDialog(T("Notice", "お知らせ", "ประกาศ"), T("No MeshRenderers found to scale.", "スケールするMeshRendererが見つかりませんでした。", "ไม่พบ MeshRenderers ที่จะปรับขนาด"), "OK");
                return;
            }

            Dictionary<MeshRenderer, float> objectSizes = new Dictionary<MeshRenderer, float>();
            float minSizeInScene = float.MaxValue;
            float maxSizeInScene = float.MinValue;

            foreach (GameObject obj in targetObjects)
            {
                MeshRenderer renderer = obj.GetComponent<MeshRenderer>();

                Vector3 preciseSize = renderer.bounds.size;
                MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    preciseSize = Vector3.Scale(meshFilter.sharedMesh.bounds.size, obj.transform.lossyScale);
                    preciseSize = new Vector3(Mathf.Abs(preciseSize.x), Mathf.Abs(preciseSize.y),
                        Mathf.Abs(preciseSize.z));
                }

                float objectSize;

                if (useVolume)
                {
                    float volume = preciseSize.x * preciseSize.y * preciseSize.z;
                    objectSize = Mathf.Pow(volume, 1f / 3f);
                }
                else
                {
                    objectSize = Mathf.Max(preciseSize.x, preciseSize.y, preciseSize.z);
                }

                objectSizes[renderer] = objectSize;
                minSizeInScene = Mathf.Min(minSizeInScene, objectSize);
                maxSizeInScene = Mathf.Max(maxSizeInScene, objectSize);
            }

            int modifiedCount = 0;
            Undo.RecordObjects(objectSizes.Keys.ToArray(), "Auto Scale In Lightmap");

            foreach (var kvp in objectSizes)
            {
                MeshRenderer renderer = kvp.Key;
                float objectSize = kvp.Value;
                float newScale;

                if (currentMode == ScaleMode.GlobalMultiplier)
                {
                    float normalizedScale = objectSize > 0.0001f ? (1.0f / objectSize) : 1.0f;
                    newScale = normalizedScale * globalMultiplier;
                }
                else
                {
                    float normalizedSize = (maxSizeInScene > minSizeInScene)
                        ? Mathf.InverseLerp(minSizeInScene, maxSizeInScene, objectSize)
                        : 0f;
                    newScale = Mathf.Lerp(maxScale, minScale, normalizedSize);
                }

                newScale = Mathf.Clamp(newScale, 0.0001f, 1024f);

                SerializedObject so = new SerializedObject(renderer);
                SerializedProperty scaleInLightmap = so.FindProperty("m_ScaleInLightmap");

                if (scaleInLightmap != null)
                {
                    scaleInLightmap.floatValue = newScale;
                    so.ApplyModifiedProperties();
                    modifiedCount++;
                }
            }

            Debug.Log($"Auto Scale In Lightmap: Scaled {modifiedCount} objects using {currentMode} mode.");
            EditorUtility.DisplayDialog(T("Complete", "完了", "เสร็จสมบูรณ์"),
                T("Successfully updated Scale In Lightmap for ", "正常にライトマップのスケールを更新しました: ", "อัปเดตมาตราส่วนใน Lightmap สำเร็จสำหรับ ") + modifiedCount + T(" objects.", " 個のオブジェクト。", " ออบเจ็กต์"), "OK");
        }

        private void ResetAllScales()
        {
            if (!EditorUtility.DisplayDialog(T("Reset All Scales", "すべてのスケールをリセット", "รีเซ็ตสเกลทั้งหมด"),
                    T("This will reset Scale In Lightmap to 1.0 for all objects in the scene. Continue?", "これにより、シーン内の全オブジェクトのScale In Lightmapが1.0にリセットされます。続行しますか？", "การดำเนินการนี้จะรีเซ็ต Scale In Lightmap เป็น 1.0 สำหรับทุกออบเจ็กต์ในฉาก ยืนยันไหม?"),
                    "Yes", "Cancel"))
            {
                return;
            }

            GameObject[] objects = FindObjectsOfType<GameObject>(true);
            int resetCount = 0;

            Undo.RecordObjects(objects.SelectMany(go => go.GetComponents<MeshRenderer>()).ToArray(),
                "Reset Scale In Lightmap");

            foreach (GameObject obj in objects)
            {
                MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    SerializedObject so = new SerializedObject(renderer);
                    SerializedProperty scaleInLightmap = so.FindProperty("m_ScaleInLightmap");

                    if (scaleInLightmap != null)
                    {
                        scaleInLightmap.floatValue = 1.0f;
                        so.ApplyModifiedProperties();
                        resetCount++;
                    }
                }
            }

            Debug.Log($"Reset Scale In Lightmap: Reset {resetCount} objects to 1.0.");
        }
    }
}
