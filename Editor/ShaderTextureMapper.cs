using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace LogicCuteGuy.Editor
{
    /// <summary>
    /// Maps texture properties between different shaders (handles different property names)
    /// </summary>
    [System.Serializable]
    public class TexturePropertyMapping
    {
        public string oldPropertyName = "";
        public string newPropertyName = "";
        public bool enabled = true;

        public TexturePropertyMapping() { }
        public TexturePropertyMapping(string old, string newName)
        {
            oldPropertyName = old;
            newPropertyName = newName;
            enabled = true;
        }
    }

    /// <summary>
    /// Shader replacement configuration
    /// </summary>
    [System.Serializable]
    public class ShaderReplacementConfig
    {
        public Shader oldShader;
        public Shader newShader;
        public List<TexturePropertyMapping> textureMapppings = new List<TexturePropertyMapping>();

        public ShaderReplacementConfig()
        {
            textureMapppings = new List<TexturePropertyMapping>();
        }
    }

    public class ShaderTextureMapper : LogicCuteGuyEditorWindow
    {
        private ShaderReplacementConfig config = new ShaderReplacementConfig();
        private Vector2 scrollPosition;
        private bool showHelp = false;
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private List<Material> foundMaterials = new List<Material>();
        private bool showScanResults = false;
        private Vector2 scanResultsScroll;

        [MenuItem("Window/LogicCuteGuy/Shader Texture Mapper")]
        public static void ShowWindow()
        {
            GetWindow<ShaderTextureMapper>(T("Shader Mapper", "シェーダーマッパー", "การจับคู่เชเดอร์"));
        }

        protected override void OnWindowGUI()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14
                };
                boxStyle = new GUIStyle(GUI.skin.box);
            }

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            // Title
            EditorGUILayout.LabelField(T("Shader & Texture Mapper", "シェーダー＆テクスチャ・マッパー", "การจับคู่เชเดอร์และพื้นผิว"), headerStyle);
            EditorGUILayout.Space();

            // Help Toggle
            showHelp = EditorGUILayout.Foldout(showHelp, T("Help", "ヘルプ", "ช่วยเหลือ"), true);
            if (showHelp)
            {
                EditorGUILayout.HelpBox(
                    T("This tool helps replace shaders while preserving texture assignments.\n\n", "このツールは、テクスチャの割り当てを維持しながらシェーダーを置き換えるのに役立ちます。\n\n", "เครื่องมือนี้ช่วยแทนที่เชเดอร์ในขณะที่ยังคงกำหนดพื้นผิวไว้เช่นเดิม\n\n") +
                    T("Steps:\n", "手順:\n", "ขั้นตอน:\n") +
                    T("1. Select Old Shader (current shader)\n", "1. 古いシェーダーを選択（現在のシェーダー）\n", "1. เลือกเชเดอร์เก่า (เชเดอร์ปัจจุบัน)\n") +
                    T("2. Select New Shader (target shader)\n", "2. 新しいシェーダーを選択（ターゲットシェーダー）\n", "2. เลือกเชเดอร์ใหม่ (เชเดอร์เป้าหมาย)\n") +
                    T("3. Map texture properties (old property → new property name)\n", "3. テクスチャプロパティのマッピング（旧プロパティ名 → 新プロパティ名）\n", "3. จับคู่คุณสมบัติพื้นผิว (ชื่อคุณสมบัติเก่า → ชื่อคุณสมบัติใหม่)\n") +
                    T("4. Select material(s) or gameobject(s)\n", "4. マテリアルまたはオブジェクトを選択\n", "4. เลือกวัสดุหรือออบเจ็กต์\n") +
                    T("5. Click 'Apply Replacement' to replace shaders and transfer textures", "5. 「適用」をクリックしてシェーダーを置換し、テクスチャを転送", "5. คลิก 'ใช้การแทนที่' เพื่อแทนที่เชเดอร์และโอนย้ายพื้นผิว"),
                    MessageType.Info);
                EditorGUILayout.Space();
            }

            // Shader Selection
            EditorGUILayout.LabelField(T("Shader Configuration", "シェーダー構成", "การกำหนดค่าเชเดอร์"), EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(boxStyle);

            config.oldShader = EditorGUILayout.ObjectField(T("Old Shader", "古いシェーダー", "เชเดอร์เก่า"), config.oldShader, typeof(Shader), false) as Shader;
            config.newShader = EditorGUILayout.ObjectField(T("New Shader", "新しいシェーダー", "เชเดอร์ใหม่"), config.newShader, typeof(Shader), false) as Shader;

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // Texture Mappings
            EditorGUILayout.LabelField(T("Texture Property Mappings", "テクスチャプロパティのマッピング", "การจับคู่คุณสมบัติพื้นผิว"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(T("Map properties from old shader to new shader (handles different property names)", "旧シェーダーから新シェーダーへプロパティをマッピングします（異なるプロパティ名にも対応）", "จับคู่คุณสมบัติจากเชเดอร์เก่าไปยังเชเดอร์ใหม่ (รองรับชื่อโปรเจ็กต์ที่ต่างกัน)"), MessageType.Info);

            EditorGUILayout.BeginVertical(boxStyle);

            if (config.textureMapppings.Count == 0)
            {
                EditorGUILayout.HelpBox(T("No mappings added yet. Click 'Add Mapping' to create one.", "まだマッピングが追加されていません。「+ 追加」をクリックして下さい。", "ยังไม่ได้เพิ่มการจับคู่ คลิก '+ เพิ่มการจับคู่' เพื่อสร้าง"), MessageType.Info);
            }

            // Display existing mappings
            for (int i = 0; i < config.textureMapppings.Count; i++)
            {
                DrawTextureMapping(i);
            }

            // Add/Remove buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(T("+ Add Mapping", "+ マッピングを追加", "+ เพิ่มการจับคู่"), GUILayout.Height(25)))
            {
                config.textureMapppings.Add(new TexturePropertyMapping());
            }

            GUI.enabled = config.textureMapppings.Count > 0;
            if (GUILayout.Button(T("- Remove Last", "- 最後を削除", "- ลบรายการสุดท้าย"), GUILayout.Height(25)))
            {
                config.textureMapppings.RemoveAt(config.textureMapppings.Count - 1);
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // Scan Project Section
            EditorGUILayout.LabelField(T("Scan Project", "プロジェクトをスキャン", "สแกนโครงการ"), EditorStyles.boldLabel);
            GUI.enabled = config.oldShader != null;
            if (GUILayout.Button(T("Scan Project for Old Shader", "古いシェーダーのプロジェクトをスキャン", "สแกนโครงการสำหรับเชเดอร์เก่า"), GUILayout.Height(30)))
            {
                ScanProjectForShader();
            }
            GUI.enabled = true;

            if (foundMaterials.Count > 0)
            {
                showScanResults = EditorGUILayout.Foldout(showScanResults, T("Found Materials", "見つかったマテリアル", "พบวัสดุ") + $" ({foundMaterials.Count})", true);
                if (showScanResults)
                {
                    DrawScanResults();
                }
            }

            EditorGUILayout.Space();
            if (config.oldShader != null && config.newShader != null)
            {
                if (GUILayout.Button(T("Auto-Detect Common Properties", "共通プロパティの自動検出", "ตรวจจับคุณสมบัติทั่วไปอัตโนมัติ"), GUILayout.Height(30)))
                {
                    AutoDetectMappings();
                }
            }

            EditorGUILayout.Space();

            // Apply Button
            GUI.enabled = config.oldShader != null && config.newShader != null;
            EditorGUILayout.LabelField(T("Apply Changes", "変更を適用", "ใช้การเปลี่ยนแปลง"), EditorStyles.boldLabel);
            
            if (GUILayout.Button(T("Apply Shader Replacement to Selection", "選択にシェーダー置換を適用", "ใช้การแทนที่เชเดอร์กับสิ่งที่เลือก"), GUILayout.Height(40)))
            {
                ApplyShaderReplacement();
            }

            GUI.enabled = true;

            EditorGUILayout.EndScrollView();
        }

        private void DrawTextureMapping(int index)
        {
            var mapping = config.textureMapppings[index];

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            mapping.enabled = EditorGUILayout.Toggle(mapping.enabled, GUILayout.Width(20));
            EditorGUILayout.LabelField(T("Mapping", "マッピング", "การจับคู่") + $" {index + 1}", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(T("Old Property:", "古いプロパティ:", "คุณสมบัติเก่า:"), GUILayout.Width(100));
            mapping.oldPropertyName = EditorGUILayout.TextField(mapping.oldPropertyName);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(T("New Property:", "新しいプロパティ:", "คุณสมบัติใหม่:"), GUILayout.Width(100));
            mapping.newPropertyName = EditorGUILayout.TextField(mapping.newPropertyName);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void AutoDetectMappings()
        {
            config.textureMapppings.Clear();

            // Get all texture properties from both shaders
            List<string> oldTextureProps = GetTextureProperties(config.oldShader);
            List<string> newTextureProps = GetTextureProperties(config.newShader);

            // Try to match them
            foreach (var oldProp in oldTextureProps)
            {
                string matchedNewProp = oldProp;

                // Try exact match first
                if (!newTextureProps.Contains(oldProp))
                {
                    // Try case-insensitive match
                    var caseInsensitiveMatch = newTextureProps.FirstOrDefault(p => 
                        p.Equals(oldProp, System.StringComparison.OrdinalIgnoreCase));
                    
                    if (caseInsensitiveMatch != null)
                    {
                        matchedNewProp = caseInsensitiveMatch;
                    }
                    else
                    {
                        // Try partial match (e.g., "_MainTex" might match "_BaseMap")
                        matchedNewProp = FindBestMatch(oldProp, newTextureProps);
                    }
                }

                if (!string.IsNullOrEmpty(matchedNewProp))
                {
                    config.textureMapppings.Add(new TexturePropertyMapping(oldProp, matchedNewProp));
                }
            }

            Debug.Log($"Auto-detected {config.textureMapppings.Count} texture property mappings");
        }

        private string FindBestMatch(string oldProp, List<string> newProps)
        {
            // Simple fuzzy matching
            var matches = newProps
                .Select(p => new { prop = p, score = GetSimilarityScore(oldProp, p) })
                .OrderByDescending(x => x.score)
                .FirstOrDefault();

            return matches?.score > 0.5f ? matches.prop : oldProp;
        }

        private float GetSimilarityScore(string a, string b)
        {
            int matches = 0;
            for (int i = 0; i < Mathf.Min(a.Length, b.Length); i++)
            {
                if (char.ToLower(a[i]) == char.ToLower(b[i]))
                    matches++;
            }
            return (float)matches / Mathf.Max(a.Length, b.Length);
        }

        private List<string> GetTextureProperties(Shader shader)
        {
            List<string> textureProps = new List<string>();

            if (shader == null) return textureProps;

            // Get shader properties count
            int propCount = ShaderUtil.GetPropertyCount(shader);
            for (int i = 0; i < propCount; i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    textureProps.Add(ShaderUtil.GetPropertyName(shader, i));
                }
            }

            return textureProps;
        }

        private void ApplyShaderReplacement()
        {
            Object[] selectedObjects = Selection.objects;

            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog(T("No Selection", "未選択", "ไม่ได้เลือก"), T("Please select materials or GameObjects", "マテリアルまたはオブジェクトを選択してください", "โปรดเลือกวัสดุหรือ GameObjects"), "OK");
                return;
            }

            int materialCount = 0;
            int textureCount = 0;

            foreach (var obj in selectedObjects)
            {
                if (obj is Material material)
                {
                    if (ReplaceMaterial(material, ref textureCount))
                        materialCount++;
                }
                else if (obj is GameObject gameObject)
                {
                    materialCount += ReplaceGameObjectMaterials(gameObject, ref textureCount);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                T("Shader Replacement Complete", "シェーダー置換完了", "การแทนที่เชเดอร์เสร็จสมบูรณ์"),
                T("Replaced shaders on ", "シェーダーを置換しました: ", "แทนที่เชเดอร์ใน ") + materialCount + T(" material(s)\nTransferred ", " 個のマテリアル\n転送しました: ", " วัสดุ\nโอนย้ายแล้ว ") + textureCount + T(" texture(s)", " 個のテクスチャ", " พื้นผิว"),
                "OK");
        }

        private bool ReplaceMaterial(Material material, ref int textureCount)
        {
            if (material.shader == config.oldShader)
            {
                // Transfer textures before changing shader
                Dictionary<string, Texture> texturesToTransfer = new Dictionary<string, Texture>();

                foreach (var mapping in config.textureMapppings)
                {
                    if (!mapping.enabled) continue;

                    if (material.HasProperty(mapping.oldPropertyName))
                    {
                        Texture texture = material.GetTexture(mapping.oldPropertyName);
                        if (texture != null)
                        {
                            texturesToTransfer[mapping.newPropertyName] = texture;
                            textureCount++;
                        }
                    }
                }

                // Change shader
                material.shader = config.newShader;

                // Apply transferred textures
                foreach (var kvp in texturesToTransfer)
                {
                    if (material.HasProperty(kvp.Key))
                    {
                        material.SetTexture(kvp.Key, kvp.Value);
                    }
                }

                EditorUtility.SetDirty(material);
                return true;
            }

            return false;
        }

        private int ReplaceGameObjectMaterials(GameObject gameObject, ref int textureCount)
        {
            int count = 0;

            // Get all renderers
            Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();

            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.materials)
                {
                    if (ReplaceMaterial(material, ref textureCount))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private void ScanProjectForShader()
        {
            if (config.oldShader == null)
            {
                EditorUtility.DisplayDialog(T("Error", "エラー", "ข้อผิดพลาด"), T("Please select an Old Shader first", "最初に古いシェーダーを選択してください", "โปรดเลือกเชเดอร์เก่าเป็นอันดับแรก"), "OK");
                return;
            }

            foundMaterials.Clear();
            string shaderName = config.oldShader.name;

            // Find all material assets
            string[] materialGuids = AssetDatabase.FindAssets("t:Material");

            foreach (var guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material != null && material.shader != null && material.shader.name == shaderName)
                {
                    foundMaterials.Add(material);
                }
            }

            showScanResults = true;
            Debug.Log($"Found {foundMaterials.Count} material(s) using shader '{shaderName}'");
        }

        private void DrawScanResults()
        {
            EditorGUILayout.BeginVertical(boxStyle);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Select All in Project", "プロジェクト内で全選択", "เลือกทั้งหมดในโครงการ"), GUILayout.Height(25)))
            {
                Selection.objects = foundMaterials.ToArray();
                Debug.Log($"Selected {foundMaterials.Count} materials in project");
            }

            if (GUILayout.Button(T("Clear Scan Results", "スキャン結果をクリア", "ล้างผลการสแกน"), GUILayout.Height(25)))
            {
                foundMaterials.Clear();
                showScanResults = false;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            scanResultsScroll = GUILayout.BeginScrollView(scanResultsScroll, GUILayout.MaxHeight(300));

            for (int i = 0; i < foundMaterials.Count; i++)
            {
                var material = foundMaterials[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                // Ping button
                if (GUILayout.Button(T("📍", "📍", "📍"), GUILayout.Width(30)))
                {
                    EditorGUIUtility.PingObject(material);
                }

                // Material field
                EditorGUILayout.ObjectField(material, typeof(Material), false);

                // Asset path
                string assetPath = AssetDatabase.GetAssetPath(material);
                EditorGUILayout.LabelField(assetPath, GUILayout.Width(300));

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }
    }
}
