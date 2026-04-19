using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace LogicCuteGuy.Editor
{
    public class RandomSpawnChildrenOnNavMesh : LogicCuteGuyEditorWindow
    {
        private List<GameObject> parentObjects = new List<GameObject>();
        private Vector2 scrollPos;
        private float heightOffset = 0f;
        private float verticalJitter = 5f;
        private float sampleMaxDistance = 10f;
        private int attemptsPerChild = 25;
        private bool includeInactiveChildren = true;
        private bool fullNavMeshPlacement = true;
        private int selectedAgentIndex;
        private int selectedAreaMask = NavMesh.AllAreas;

        [MenuItem("GameObject/LogicCuteGuy/Random Spawn Children On NavMesh")]
        public static void ShowWindow()
        {
            GetWindow<RandomSpawnChildrenOnNavMesh>(T("Random Spawn On NavMesh", "NavMesh上にランダム生成", "สุ่มสร้างบน NavMesh"));
        }

        public static void RandomSpawnSelectedParents()
        {
            Transform[] selected = Selection.transforms;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog(T("No Selection", "未選択", "ไม่ได้เลือก"), T("Select one or more parent objects first.", "最初に1つ以上の親オブジェクトを選択してください。", "เลือกออบเจ็กต์พาเรนต์หนึ่งรายการขึ้นไปก่อน"), "OK");
                return;
            }

            int totalMoved = 0;
            int totalFailed = 0;

            int agentTypeId = GetDefaultAgentTypeId();
            int areaMask = NavMesh.AllAreas;

            for (int i = 0; i < selected.Length; i++)
            {
                int moved;
                int failed;
                SpawnForParent(selected[i], 0f, 5f, 10f, 25, true, true, agentTypeId, areaMask, out moved, out failed);
                totalMoved += moved;
                totalFailed += failed;
            }

            EditorUtility.DisplayDialog(
                T("Random Spawn Complete", "ランダム生成完了", "สุ่มสร้างเสร็จสมบูรณ์"),
                T("Moved ", "移動量 ", "ย้ายแล้ว ") + totalMoved + " child object(s). Failed to place " + totalFailed + " child object(s).",
                "OK");
        }

        protected override void OnWindowGUI()
        {
            GUILayout.Label(T("Random Spawn Children On NavMesh", "NavMesh上に子をランダム生成", "สุ่มสร้างลูกบน NavMesh"), EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(T("Parent Objects:", "親オブジェクト:", "ออบเจ็กต์พาเรนต์:"), EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(80));
            // Let user add one empty slot if they want, but usually populated from selection
            for (int i = 0; i < parentObjects.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    parentObjects[i] =
                        (GameObject)EditorGUILayout.ObjectField(parentObjects[i], typeof(GameObject), true);
                    if (GUILayout.Button(T("X", "X", "X"), EditorStyles.miniButton, GUILayout.Width(24)))
                    {
                        parentObjects.RemoveAt(i);
                        i--;
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(T("Set from Selection", "選択状態から設定", "ตั้งค่าจากการเลือก")))
                {
                    parentObjects.Clear();
                    foreach (var go in Selection.gameObjects)
                    {
                        parentObjects.Add(go);
                    }
                }

                if (GUILayout.Button(T("Add Empty", "空を追加", "เพิ่มความว่างเปล่า")))
                {
                    parentObjects.Add(null);
                }

                if (GUILayout.Button(T("Clear", "クリア", "ล้าง")))
                {
                    parentObjects.Clear();
                }
            }

            EditorGUILayout.Space();
            DrawAgentAndAreaOptions();
            EditorGUILayout.Space();
            fullNavMeshPlacement = EditorGUILayout.Toggle(T("Full NavMesh Place", "NavMesh全体に配置", "การวางบน NavMesh แบบเต็ม"), fullNavMeshPlacement);
            heightOffset = EditorGUILayout.FloatField(T("Height Offset", "高さのオフセット", "ออฟเซ็ตความสูง"), heightOffset);
            verticalJitter = EditorGUILayout.FloatField(T("Vertical Jitter", "垂直方向のジッター", "การสั่นไหวแนวตั้ง"), verticalJitter);
            sampleMaxDistance = EditorGUILayout.FloatField(T("Sample Max Distance", "最大サンプル距離", "ระยะทางสุ่มตัวอย่างสูงสุด"), sampleMaxDistance);
            attemptsPerChild = EditorGUILayout.IntField(T("Attempts Per Child", "1子あたりの試行回数", "การพยายามต่อลูกหนึ่งรายการ"), attemptsPerChild);
            includeInactiveChildren = EditorGUILayout.Toggle(T("Include Inactive Children", "非アクティブな子を含める", "รวมลูกที่ไม่ได้ใช้งาน"), includeInactiveChildren);

            verticalJitter = Mathf.Max(0f, verticalJitter);
            sampleMaxDistance = Mathf.Max(0.01f, sampleMaxDistance);
            attemptsPerChild = Mathf.Max(1, attemptsPerChild);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(parentObjects.Count == 0))
            {
                if (GUILayout.Button(T("Random Spawn Children", "子をランダム生成", "สุ่มสร้างลูก"), GUILayout.Height(32)))
                {
                    int totalMoved = 0;
                    int totalFailed = 0;
                    int agentTypeId = GetSelectedAgentTypeId();

                    foreach (GameObject parent in parentObjects)
                    {
                        if (parent == null) continue;
                        int moved;
                        int failed;
                        SpawnForParent(parent.transform, heightOffset, verticalJitter, sampleMaxDistance,
                            attemptsPerChild, includeInactiveChildren, fullNavMeshPlacement, agentTypeId,
                            selectedAreaMask, out moved, out failed);
                        totalMoved += moved;
                        totalFailed += failed;
                    }

                    EditorUtility.DisplayDialog(
                        T("Random Spawn Complete", "ランダム生成完了", "สุ่มสร้างเสร็จสมบูรณ์"),
                        T("Moved ", "移動量 ", "ย้ายแล้ว ") + totalMoved + " child object(s). Failed to place " + totalFailed +
                        " child object(s).",
                        "OK");
                }
            }
        }

        private void DrawAgentAndAreaOptions()
        {
            int settingsCount = NavMesh.GetSettingsCount();
            if (settingsCount <= 0)
            {
                EditorGUILayout.HelpBox(T("No NavMesh agent settings found.", "NavMesh Agent設定が見つかりませんでした。", "ไม่พบการตั้งค่า NavMesh agent"), MessageType.Warning);
                return;
            }

            List<string> agentLabels = new List<string>(settingsCount);
            for (int i = 0; i < settingsCount; i++)
            {
                int id = NavMesh.GetSettingsByIndex(i).agentTypeID;
                string name = NavMesh.GetSettingsNameFromID(id);
                if (string.IsNullOrEmpty(name))
                {
                    name = "Agent " + id;
                }

                agentLabels.Add(name + " (" + id + ")");
            }

            selectedAgentIndex = Mathf.Clamp(selectedAgentIndex, 0, settingsCount - 1);
            selectedAgentIndex = EditorGUILayout.Popup(T("Agent Type", "エージェント型", "ประเภทข้อมูลตัวแทน"), selectedAgentIndex, agentLabels.ToArray());

            string[] areaNames = BuildFallbackAreaNames();
            selectedAreaMask = EditorGUILayout.MaskField(T("Areas", "エリア", "พื้นที่"), selectedAreaMask, areaNames);
            if (selectedAreaMask == 0)
            {
                selectedAreaMask = 1;
            }
        }

        private static string[] BuildFallbackAreaNames()
        {
            string[] names = new string[32];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = "Area " + i;
            }

            return names;
        }

        private int GetSelectedAgentTypeId()
        {
            int settingsCount = NavMesh.GetSettingsCount();
            if (settingsCount <= 0)
            {
                return GetDefaultAgentTypeId();
            }

            selectedAgentIndex = Mathf.Clamp(selectedAgentIndex, 0, settingsCount - 1);
            return NavMesh.GetSettingsByIndex(selectedAgentIndex).agentTypeID;
        }

        private static int GetDefaultAgentTypeId()
        {
            int settingsCount = NavMesh.GetSettingsCount();
            if (settingsCount <= 0)
            {
                return 0;
            }

            return NavMesh.GetSettingsByIndex(0).agentTypeID;
        }

        private static void SpawnForParent(
            Transform parent,
            float yOffset,
            float yJitter,
            float maxSampleDistance,
            int attempts,
            bool includeInactive,
            bool useFullNavMesh,
            int agentTypeId,
            int areaMask,
            out int moved,
            out int failed)
        {
            moved = 0;
            failed = 0;

            if (parent == null)
            {
                return;
            }

            Transform[] children = parent.GetComponentsInChildren<Transform>(includeInactive);
            if (children == null || children.Length <= 1)
            {
                return;
            }

            NavMeshTriangulation triangulation = default;
            if (useFullNavMesh)
            {
                triangulation = NavMesh.CalculateTriangulation();
            }

            NavMeshQueryFilter filter = new NavMeshQueryFilter();
            filter.agentTypeID = agentTypeId;
            filter.areaMask = areaMask;

            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || child == parent)
                {
                    continue;
                }

                bool placed = useFullNavMesh
                    ? TryFindFullNavMeshPosition(triangulation, filter, attempts, out Vector3 hitPosition)
                    : TryFindNavMeshPosition(parent.position, yJitter, maxSampleDistance, attempts, filter,
                        out hitPosition);

                if (!placed)
                {
                    failed++;
                    continue;
                }

                Undo.RecordObject(child, T("Random Spawn Children On NavMesh", "NavMesh上で子をランダム生成", "สุ่มสร้างลูกบน NavMesh"));
                child.position = hitPosition + Vector3.up * yOffset;
                EditorUtility.SetDirty(child);
                moved++;
            }
        }

        private static bool TryFindNavMeshPosition(
            Vector3 center,
            float yJitter,
            float maxSampleDistance,
            int attempts,
            NavMeshQueryFilter filter,
            out Vector3 position)
        {
            position = center;

            for (int i = 0; i < attempts; i++)
            {
                Vector2 randomCircle = Random.insideUnitCircle * maxSampleDistance;
                float randomY = yJitter > 0f ? Random.Range(-yJitter, yJitter) : 0f;
                Vector3 candidate = center + new Vector3(randomCircle.x, randomY, randomCircle.y);

                NavMeshHit hit;
                if (NavMesh.SamplePosition(candidate, out hit, maxSampleDistance, filter))
                {
                    position = hit.position;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindFullNavMeshPosition(NavMeshTriangulation triangulation, NavMeshQueryFilter filter,
            int attempts, out Vector3 position)
        {
            position = Vector3.zero;

            if (triangulation.vertices == null || triangulation.indices == null || triangulation.indices.Length < 3)
            {
                return false;
            }

            int triangleCount = triangulation.indices.Length / 3;
            if (triangleCount <= 0)
            {
                return false;
            }

            for (int i = 0; i < attempts; i++)
            {
                int tri = Random.Range(0, triangleCount) * 3;
                Vector3 a = triangulation.vertices[triangulation.indices[tri]];
                Vector3 b = triangulation.vertices[triangulation.indices[tri + 1]];
                Vector3 c = triangulation.vertices[triangulation.indices[tri + 2]];

                float r1 = Random.value;
                float r2 = Random.value;
                float sqrtR1 = Mathf.Sqrt(r1);
                Vector3 candidate = (1f - sqrtR1) * a + (sqrtR1 * (1f - r2)) * b + (sqrtR1 * r2) * c;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(candidate, out hit, 1.0f, filter))
                {
                    position = hit.position;
                    return true;
                }
            }

            return false;
        }
    }
}
