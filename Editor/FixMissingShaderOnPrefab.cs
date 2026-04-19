using UnityEditor;
using UnityEngine;

namespace LogicCuteGuy.Editor
{
    public class FixMissingShaderOnPrefab
    {
        [MenuItem("Assets/Fix Missing Shaders (Prefab → Standard)", true)]
        static bool Validate()
        {
            foreach (Object obj in Selection.objects)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab"))
                {
                    return true;
                }
            }
            return false;
        }

        [MenuItem("Assets/Fix Missing Shaders (Prefab → Standard)")]
        static void Fix()
        {
            int fixedCount = 0;
            Shader standardShader = Shader.Find("Standard");
            
            if (standardShader == null)
            {
                Debug.LogError("Standard shader not found! Cannot fix missing shaders.");
                return;
            }

            foreach (Object obj in Selection.objects)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".prefab"))
                    continue;

                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefabAsset == null) continue;

                bool wasModified = false;
                using (var editingScope = new PrefabUtility.EditPrefabContentsScope(assetPath))
                {
                    GameObject prefabRoot = editingScope.prefabContentsRoot;
                    wasModified = FixShadersRecursively(prefabRoot, standardShader);
                }

                if (wasModified)
                {
                    fixedCount++;
                    Debug.Log($"Fixed missing shaders in prefab: {assetPath}");
                }
            }

            if (fixedCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"Fixed missing shaders on {fixedCount} prefab(s) → Standard");
            }
            else
            {
                Debug.Log("No missing shaders found in selected prefabs.");
            }
        }

        static bool FixShadersRecursively(GameObject go, Shader replacementShader)
        {
            bool wasModified = false;
            
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material[] materials = renderer.sharedMaterials;
                bool rendererModified = false;
                
                for (int i = 0; i < materials.Length; i++)
                {
                    Material mat = materials[i];
                    if (mat != null && IsShaderMissingOrBroken(mat.shader))
                    {
                        string oldShaderName = mat.shader != null ? mat.shader.name : "null";
                        mat.shader = replacementShader;
                        EditorUtility.SetDirty(mat);
                        rendererModified = true;
                        Debug.Log($"Fixed shader on material '{mat.name}' in GameObject '{go.name}' (was: {oldShaderName})");
                    }
                }
                
                if (rendererModified)
                {
                    renderer.sharedMaterials = materials;
                    EditorUtility.SetDirty(renderer);
                    wasModified = true;
                }
            }

            // Recurse on children
            for (int i = 0; i < go.transform.childCount; i++)
            {
                if (FixShadersRecursively(go.transform.GetChild(i).gameObject, replacementShader))
                {
                    wasModified = true;
                }
            }
            
            return wasModified;
        }

        static bool IsShaderMissingOrBroken(Shader shader)
        {
            if (shader == null)
                return true;
            
            string shaderName = shader.name;
            
            // Check for Unity's error shaders
            if (shaderName.Contains("InternalError") || 
                shaderName.Contains("Hidden/InternalErrorShader") ||
                shaderName == "Hidden/InternalErrorShader")
            {
                return true;
            }
            
            return false;
        }
    }
}
