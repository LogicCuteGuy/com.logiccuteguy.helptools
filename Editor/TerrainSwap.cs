using UnityEngine;
using UnityEditor;

namespace LogicCuteGuy.Editor
{
    public class TerrainSwap : LogicCuteGuyEditorWindow
    {
        private Terrain targetTerrain;
        private TerrainData newTerrainData;

        [MenuItem("CONTEXT/Terrain/LogicCuteGuy/Terrain Swap...")]
        public static void ShowWindowFromContext(MenuCommand command)
        {
            TerrainSwap window = GetWindow<TerrainSwap>(T("Terrain Swap", "地形(Terrain)を入れ替え", "สลับภูมิประเทศ"));
            Terrain clickedTerrain = command.context as Terrain;
            if (clickedTerrain != null)
            {
                window.targetTerrain = clickedTerrain;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Selection.activeGameObject != null)
            {
                targetTerrain = Selection.activeGameObject.GetComponent<Terrain>();
            }
        }

        private void OnSelectionChange()
        {
            if (Selection.activeGameObject != null)
            {
                Terrain t = Selection.activeGameObject.GetComponent<Terrain>();
                if (t != null)
                {
                    targetTerrain = t;
                    Repaint();
                }
            }
        }

        protected override void OnWindowGUI()
        {
            GUILayout.Label(T("Terrain Swap Settings", "地形入れ替えの設定", "การตั้งค่าสลับภูมิประเทศ"), EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(T("Select a Terrain and assign new TerrainData to swap it out.", "地形(Terrain)を選択し、新しいTerrainDataを割り当てて入れ替えます。", "เลือกภูมิประเทศและกำหนด TerrainData ใหม่เพื่อสลับ"), MessageType.Info);
            EditorGUILayout.Space();

            targetTerrain =
                (Terrain)EditorGUILayout.ObjectField(T("Target Terrain", "対象の地形", "ภูมิประเทศเป้าหมาย"), targetTerrain, typeof(Terrain), true);
            newTerrainData =
                (TerrainData)EditorGUILayout.ObjectField(T("New Terrain Data", "新しい地形データ", "ข้อมูลภูมิประเทศใหม่"), newTerrainData, typeof(TerrainData),
                    false);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(targetTerrain == null || newTerrainData == null))
            {
                if (GUILayout.Button(T("Swap Terrain Data", "地形(Terrain)データを入れ替える", "สลับข้อมูลภูมิประเทศ"), GUILayout.Height(30)))
                {
                    SwapData();
                }
            }
        }

        private void SwapData()
        {
            Undo.RecordObject(targetTerrain, T("Swap Terrain Data", "地形(Terrain)データを入れ替える", "สลับข้อมูลภูมิประเทศ"));

            targetTerrain.terrainData = newTerrainData;

            // Also keep the TerrainCollider in sync if it exists
            TerrainCollider terrainCollider = targetTerrain.GetComponent<TerrainCollider>();
            if (terrainCollider != null)
            {
                Undo.RecordObject(terrainCollider, T("Swap Terrain Data", "地形(Terrain)データを入れ替える", "สลับข้อมูลภูมิประเทศ"));
                terrainCollider.terrainData = newTerrainData;
            }

            EditorUtility.SetDirty(targetTerrain);

            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(targetTerrain.gameObject.scene);
            }

            Debug.Log($"TerrainSwap: Terrain data swapped successfully to {newTerrainData.name}.", targetTerrain);
            EditorUtility.DisplayDialog(T("Complete", "完了", "เสร็จสมบูรณ์"), T("Terrain data swapped successfully.", "Terrainデータの入れ替えが完了しました。", "สลับข้อมูลภูมิประเทศสำเร็จแล้ว"), "OK");
        }
    }
}