using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace LogicCuteGuy.Editor
{
    public class CheckAssetUsageInScene : LogicCuteGuyEditorWindow
    {
        private Vector2 scrollPosition;
        private float currentNameColumnWidth = 200f;
        private List<UsageResult> results = new List<UsageResult>();
        private string selectedPath;
        private TreeNode rootNode;
        private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();
        private bool showOnlyUnused = false;
        private string searchFilter = "";

        private enum SortColumn
        {
            Name,
            Files,
            Used,
            Unused,
            Size,
            UsedPercent
        }
        private SortColumn currentSortColumn = SortColumn.Name;
        private bool sortAscending = true;

        // Simple in-memory snapshot so the tree and foldouts survive minor UI actions
        private static CacheSnapshot lastCache;

        private class UsageResult
        {
            public string assetPath;
            public List<GameObject> usedInObjects = new List<GameObject>();
            public bool isUsed => usedInObjects.Count > 0;
            public long fileSize;
        }

        private class TreeNode
        {
            public string path;
            public string name;
            public bool isFolder;
            public List<TreeNode> children = new List<TreeNode>();
            public UsageResult result; // For files only
            
            // Aggregated stats for folders
            public int totalFiles;
            public int usedFiles;
            public int unusedFiles;
            public long totalSize;
            public long usedSize;
            public long unusedSize;
        }

        private class CacheSnapshot
        {
            public string selectedPath;
            public List<UsageResult> results;
            public TreeNode rootNode;
            public Dictionary<string, bool> foldoutStates;
            public SortColumn sortColumn;
            public bool sortAscending;
        }

        [MenuItem("Assets/Check Usage in Scene", false, 2000)]
        private static void CheckUsageInSceneMenu()
        {
            var window = GetWindow<CheckAssetUsageInScene>(T("Asset Usage in Scene", "シーン内のアセット使用状況", "การใช้แอสเซ็ทในฉาก"));
            window.minSize = new Vector2(600, 400);
            window.LoadCache();
            window.AnalyzeSelection();
            window.Show();
        }

        [MenuItem("Assets/Check Usage in Scene", true)]
        private static bool CheckUsageInSceneValidation()
        {
            // Only show menu if something is selected
            return Selection.objects.Length > 0;
        }

        private void AnalyzeSelection()
        {
            results.Clear();
            // Keep foldoutStates so expansion state persists across refreshes
            
            // Get all selected asset paths
            List<string> assetPaths = new List<string>();
            
            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path))
                {
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        // If it's a folder, get all assets in it
                        selectedPath = path;
                        var guids = AssetDatabase.FindAssets("", new[] { path });
                        foreach (var guid in guids)
                        {
                            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                            if (!AssetDatabase.IsValidFolder(assetPath))
                            {
                                assetPaths.Add(assetPath);
                            }
                        }
                    }
                    else
                    {
                        // Single asset
                        selectedPath = path;
                        assetPaths.Add(path);
                    }
                }
            }

            if (assetPaths.Count == 0)
            {
                EditorUtility.DisplayDialog(T("No Assets", "アセットなし", "ไม่มีแอสเซ็ท"), T("No valid assets selected.", "有効なアセットが選択されていません。", "ไม่ได้เลือกแอสเซ็ทที่ถูกต้อง"), "OK");
                return;
            }

            // Scan the current scene
            ScanScene(assetPaths);
            
            // Build tree structure
            BuildTree();
        }

        private void ScanScene(List<string> assetPaths)
        {
            EditorUtility.DisplayProgressBar("Scanning Scene", "Finding asset references...", 0);

            try
            {
                // Initialize results
                foreach (var assetPath in assetPaths)
                {
                    var result = new UsageResult { assetPath = assetPath };
                    
                    // Get file size
                    string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), assetPath);
                    if (File.Exists(fullPath))
                    {
                        FileInfo fileInfo = new FileInfo(fullPath);
                        result.fileSize = fileInfo.Length;
                    }
                    
                    results.Add(result);
                }

                // Get all GameObjects in the scene
                Scene activeScene = SceneManager.GetActiveScene();
                if (!activeScene.IsValid())
                {
                    EditorUtility.DisplayDialog(T("No Scene", "シーンなし", "ไม่มีฉาก"), T("No active scene found.", "アクティブなシーンが見つかりませんでした。", "ไม่พบฉากที่ใช้งานอยู่"), "OK");
                    return;
                }

                GameObject[] allObjects = activeScene.GetRootGameObjects();
                List<GameObject> allGameObjects = new List<GameObject>();

                // Get all GameObjects including children
                foreach (var root in allObjects)
                {
                    allGameObjects.AddRange(root.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject));
                }

                int total = allGameObjects.Count;
                int current = 0;

                // Check each GameObject for references
                foreach (var go in allGameObjects)
                {
                    current++;
                    if (current % 10 == 0)
                    {
                        EditorUtility.DisplayProgressBar("Scanning Scene", 
                            $"Checking {go.name}...", (float)current / total);
                    }

                    CheckGameObjectForReferences(go, assetPaths);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void CheckGameObjectForReferences(GameObject go, List<string> assetPaths)
        {
            // Check all components
            Component[] components = go.GetComponents<Component>();
            
            foreach (var component in components)
            {
                if (component == null) continue;

                SerializedObject so = new SerializedObject(component);
                SerializedProperty prop = so.GetIterator();

                while (prop.NextVisible(true))
                {
                    if (prop.propertyType == SerializedPropertyType.ObjectReference && prop.objectReferenceValue != null)
                    {
                        string refPath = AssetDatabase.GetAssetPath(prop.objectReferenceValue);
                        
                        if (!string.IsNullOrEmpty(refPath))
                        {
                            // Check if this reference matches any of our target assets
                            for (int i = 0; i < assetPaths.Count; i++)
                            {
                                if (refPath == assetPaths[i] || refPath.StartsWith(assetPaths[i]))
                                {
                                    if (!results[i].usedInObjects.Contains(go))
                                    {
                                        results[i].usedInObjects.Add(go);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void BuildTree()
        {
            if (string.IsNullOrEmpty(selectedPath))
                return;

            rootNode = new TreeNode
            {
                path = selectedPath,
                name = Path.GetFileName(selectedPath),
                isFolder = true
            };

            // Add all results to tree
            foreach (var result in results)
            {
                AddToTree(rootNode, result);
            }

            // Calculate aggregated stats
            CalculateStats(rootNode);

            // Sort tree
            SortTree(rootNode);

            // Save snapshot so the tree can be restored without re-scan
            SaveCache();
        }

        private void AddToTree(TreeNode parent, UsageResult result)
        {
            string relativePath = result.assetPath;
            if (relativePath.StartsWith(selectedPath + "/"))
            {
                relativePath = relativePath.Substring(selectedPath.Length + 1);
            }

            string[] parts = relativePath.Split('/');
            TreeNode current = parent;

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                bool isLastPart = (i == parts.Length - 1);

                TreeNode child = current.children.Find(n => n.name == part);
                
                if (child == null)
                {
                    child = new TreeNode
                    {
                        name = part,
                        isFolder = !isLastPart,
                        path = current.path + "/" + part
                    };

                    if (isLastPart)
                    {
                        child.result = result;
                    }

                    current.children.Add(child);
                }

                current = child;
            }
        }

        private void CalculateStats(TreeNode node)
        {
            if (!node.isFolder)
            {
                // Leaf node (file)
                node.totalFiles = 1;
                node.totalSize = node.result.fileSize;
                
                if (node.result.isUsed)
                {
                    node.usedFiles = 1;
                    node.usedSize = node.result.fileSize;
                }
                else
                {
                    node.unusedFiles = 1;
                    node.unusedSize = node.result.fileSize;
                }
                return;
            }

            // Folder node - aggregate children
            node.totalFiles = 0;
            node.usedFiles = 0;
            node.unusedFiles = 0;
            node.totalSize = 0;
            node.usedSize = 0;
            node.unusedSize = 0;

            foreach (var child in node.children)
            {
                CalculateStats(child);
                
                node.totalFiles += child.totalFiles;
                node.usedFiles += child.usedFiles;
                node.unusedFiles += child.unusedFiles;
                node.totalSize += child.totalSize;
                node.usedSize += child.usedSize;
                node.unusedSize += child.unusedSize;
            }

        }

        private void SortTree(TreeNode node)
        {
            if (node == null || node.children == null) return;
            
            node.children.Sort((a, b) =>
            {
                if (a.isFolder != b.isFolder)
                    return a.isFolder ? -1 : 1;

                int result = 0;
                switch (currentSortColumn)
                {
                    case SortColumn.Name:
                        result = string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase);
                        break;
                    case SortColumn.Files:
                        result = a.totalFiles.CompareTo(b.totalFiles);
                        break;
                    case SortColumn.Used:
                        result = a.usedFiles.CompareTo(b.usedFiles);
                        break;
                    case SortColumn.Unused:
                        result = a.unusedFiles.CompareTo(b.unusedFiles);
                        break;
                    case SortColumn.Size:
                        result = a.totalSize.CompareTo(b.totalSize);
                        break;
                    case SortColumn.UsedPercent:
                        float aPercent = a.totalFiles > 0 ? (float)a.usedFiles / a.totalFiles : 0f;
                        float bPercent = b.totalFiles > 0 ? (float)b.usedFiles / b.totalFiles : 0f;
                        result = aPercent.CompareTo(bPercent);
                        break;
                }
                return sortAscending ? result : -result;
            });

            foreach (var child in node.children)
            {
                if (child.isFolder)
                {
                    SortTree(child);
                }
            }
        }

        private void DrawSortableHeader(string label, SortColumn column, GUILayoutOption layoutOption)
        {
            string displayLabel = label;
            if (currentSortColumn == column)
                displayLabel += sortAscending ? " ▲" : " ▼";

            if (GUILayout.Button(displayLabel, EditorStyles.toolbarButton, layoutOption))
            {
                if (currentSortColumn == column)
                {
                    sortAscending = !sortAscending;
                }
                else
                {
                    currentSortColumn = column;
                    sortAscending = column == SortColumn.Name;
                }
                SortTree(rootNode);
                SaveCache();
            }
        }

        protected override void OnWindowGUI()
        {
            // If we have no results but a cache exists (e.g., after minor repaint), restore it
            if ((results == null || results.Count == 0) && lastCache != null)
            {
                LoadCache();
            }

            EditorGUILayout.Space(5);
            
            // Header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField(T("Asset Usage Analysis - Tree View", "アセット使用状況の分析 - ツリービュー", "การวิเคราะห์การใช้แอสเซ็ท - มุมมองต้นไม้"), EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField(T("Path: ", "パス: ", "เส้นทาง: ") + selectedPath, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            if (results.Count == 0)
            {
                EditorGUILayout.HelpBox(T("No results. Select an asset or folder and use 'Assets > Check Usage in Scene'", "結果がありません。アセットまたフォルダを選択し、'Assets > Check Usage in Scene' を実行してください。", "ไม่มีผลลัพธ์ เลือกแอสเซ็ทหรือโฟลเดอร์และใช้ 'Assets > Check Usage in Scene'"), MessageType.Info);
                return;
            }

            // Toolbar with filters and controls
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            if (GUILayout.Button(T("Refresh", "更新", "รีเฟรช"), EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                AnalyzeSelection();
            }
            
            GUILayout.Space(10);
            
            EditorGUI.BeginChangeCheck();
            showOnlyUnused = GUILayout.Toggle(showOnlyUnused, T("Show Only Unused", "未使用のみ表示", "แสดงเฉพาะที่ไม่ได้ใช้"), EditorStyles.toolbarButton, GUILayout.Width(120));
            if (EditorGUI.EndChangeCheck())
            {
                Repaint();
            }
            
            GUILayout.Space(10);
            GUILayout.Label(T("Search:", "検索:", "ค้นหา:"), GUILayout.Width(50));
            searchFilter = GUILayout.TextField(searchFilter, EditorStyles.toolbarTextField, GUILayout.Width(150));
            
            if (GUILayout.Button(T("Clear", "クリア", "ล้าง"), EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                searchFilter = "";
                GUI.FocusControl(null);
            }
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button(T("Expand All", "すべて展開", "ขยายทั้งหมด"), EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                ExpandAll(rootNode, true);
            }
            
            if (GUILayout.Button(T("Collapse All", "すべて折りたたむ", "ย่อทั้งหมด"), EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                ExpandAll(rootNode, false);
            }
            
            EditorGUILayout.EndHorizontal();

            // Summary stats (respect filter)
            if (rootNode != null)
            {
                int totalFilesDisplay = showOnlyUnused ? rootNode.unusedFiles : rootNode.totalFiles;
                long totalSizeDisplay = showOnlyUnused ? rootNode.unusedSize : rootNode.totalSize;
                int usedFilesDisplay = showOnlyUnused ? 0 : rootNode.usedFiles;
                long usedSizeDisplay = showOnlyUnused ? 0 : rootNode.usedSize;
                int unusedFilesDisplay = rootNode.unusedFiles;
                long unusedSizeDisplay = rootNode.unusedSize;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField(T("Total: ", "合計: ", "ทั้งหมด: ") + totalFilesDisplay + T(" files (", " 個のファイル (", " ไฟล์ (") + FormatBytes(totalSizeDisplay) + ")", GUILayout.Width(220));
                GUI.color = Color.green;
                EditorGUILayout.LabelField(T("Used: ", "使用中: ", "ใช้อยู่: ") + usedFilesDisplay + " (" + FormatBytes(usedSizeDisplay) + ")", GUILayout.Width(200));
                GUI.color = new Color(1f, 0.5f, 0f);
                EditorGUILayout.LabelField(T("Unused: ", "未使用: ", "ไม่ได้ใช้: ") + unusedFilesDisplay + " (" + FormatBytes(unusedSizeDisplay) + ")", GUILayout.Width(200));
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            currentNameColumnWidth = Mathf.Max(200f, EditorGUIUtility.currentViewWidth - 340f - 30f);

            // Column headers
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            DrawSortableHeader(T("Name", "名前", "ชื่อ"), SortColumn.Name, GUILayout.Width(currentNameColumnWidth));
            DrawSortableHeader(T("Files", "ファイル", "ไฟล์"), SortColumn.Files, GUILayout.Width(50));
            DrawSortableHeader(T("Used", "使用中", "ใช้อยู่"), SortColumn.Used, GUILayout.Width(50));
            DrawSortableHeader(T("Unused", "未使用", "ไม่ได้ใช้"), SortColumn.Unused, GUILayout.Width(60));
            DrawSortableHeader(T("Size", "サイズ", "ขนาด"), SortColumn.Size, GUILayout.Width(80));
            DrawSortableHeader(T("Used %", "使用率 %", "การใช้ %"), SortColumn.UsedPercent, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            // Tree view
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            if (rootNode != null)
            {
                DrawTreeNode(rootNode, 0);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTreeNode(TreeNode node, int depth)
        {
            // Apply search filter
            if (!string.IsNullOrEmpty(searchFilter) && !NodeMatchesSearch(node, searchFilter))
                return;

            // Apply "show only unused" filter
            if (showOnlyUnused && node.unusedFiles == 0)
                return;

            EditorGUILayout.BeginHorizontal();

            if (node.isFolder)
            {
                // Folder node
                EditorGUILayout.BeginHorizontal(GUILayout.Width(currentNameColumnWidth));
                bool isExpanded = GetFoldoutState(node.path);
                
                // Foldout arrow
                bool newExpanded = EditorGUILayout.Foldout(isExpanded, "", true);
                if (newExpanded != isExpanded)
                {
                    SetFoldoutState(node.path, newExpanded);
                }

                // Indentation + Folder icon
                GUILayout.Space(depth * 15);
                GUILayout.Label(EditorGUIUtility.IconContent("Folder Icon"), GUILayout.Width(18), GUILayout.Height(16));
                
                // Folder name
                GUIStyle folderStyle = new GUIStyle(EditorStyles.label);
                folderStyle.fontStyle = FontStyle.Bold;
                
                Rect folderRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true));
                if (GUI.Button(folderRect, node.name, folderStyle))
                {
                    Object folder = AssetDatabase.LoadAssetAtPath<Object>(node.path);
                    if (folder != null)
                    {
                        EditorGUIUtility.PingObject(folder);
                        Selection.activeObject = folder;
                    }
                }
                
                // Right-click context menu for folder
                if (Event.current.type == EventType.ContextClick && folderRect.Contains(Event.current.mousePosition))
                {
                    ShowFolderContextMenu(node);
                    Event.current.Use();
                }
                
                EditorGUILayout.EndHorizontal();

                // Stats - properly aligned columns (respect filter)
                int displayTotal = showOnlyUnused ? node.unusedFiles : node.totalFiles;
                int displayUsed = showOnlyUnused ? 0 : node.usedFiles;
                int displayUnused = node.unusedFiles;
                long displaySize = showOnlyUnused ? node.unusedSize : node.totalSize;

                GUILayout.Label(displayTotal.ToString(), EditorStyles.label, GUILayout.Width(50));
                
                GUI.color = Color.green;
                GUILayout.Label(displayUsed.ToString(), EditorStyles.label, GUILayout.Width(50));
                
                GUI.color = new Color(1f, 0.5f, 0f);
                GUILayout.Label(displayUnused.ToString(), EditorStyles.label, GUILayout.Width(60));
                GUI.color = Color.white;
                
                GUILayout.Label(FormatBytes(displaySize), EditorStyles.label, GUILayout.Width(80));
                
                // Usage percentage
                float usagePercent = displayTotal > 0 ? (float)displayUsed / displayTotal * 100 : 0;
                string percentText = $"{usagePercent:F0}%";
                GUILayout.Label(percentText, EditorStyles.label, GUILayout.Width(100));

                EditorGUILayout.EndHorizontal();

                // Draw children if expanded
                if (newExpanded)
                {
                    foreach (var child in node.children)
                    {
                        DrawTreeNode(child, depth + 1);
                    }
                }
            }
            else
            {
                // File node
                EditorGUILayout.BeginHorizontal(GUILayout.Width(currentNameColumnWidth));

                // Foldout placeholder (no foldout for files)
                GUILayout.Space(15);
                
                // Indentation
                GUILayout.Space(depth * 15);

                // File icon
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(node.result.assetPath);
                Texture2D icon = AssetDatabase.GetCachedIcon(node.result.assetPath) as Texture2D;
                if (icon != null)
                {
                    GUILayout.Label(icon, GUILayout.Width(18), GUILayout.Height(16));
                }
                else
                {
                    GUILayout.Space(18);
                }

                // File name with color based on usage
                GUIStyle fileStyle = new GUIStyle(EditorStyles.label);
                if (node.result.isUsed)
                {
                    fileStyle.normal.textColor = Color.green;
                }
                else
                {
                    fileStyle.normal.textColor = new Color(1f, 0.5f, 0f);
                }
                
                Rect fileRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true));
                if (GUI.Button(fileRect, node.name, fileStyle))
                {
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }
                }
                
                // Right-click context menu for file
                if (Event.current.type == EventType.ContextClick && fileRect.Contains(Event.current.mousePosition))
                {
                    ShowFileContextMenu(node);
                    Event.current.Use();
                }

                EditorGUILayout.EndHorizontal();

                // Stats
                GUILayout.Label(T("1", "1", "1"), EditorStyles.label, GUILayout.Width(50));
                GUILayout.Label(node.result.isUsed ? "1" : "0", EditorStyles.label, GUILayout.Width(50));
                GUILayout.Label(node.result.isUsed ? "0" : "1", EditorStyles.label, GUILayout.Width(60));
                GUILayout.Label(FormatBytes(node.result.fileSize), EditorStyles.label, GUILayout.Width(80));
                
                // Usage indicator
                if (node.result.isUsed)
                {
                    GUI.color = Color.green;
                    GUILayout.Label(T("100%", "100%", "100%"), EditorStyles.label, GUILayout.Width(100));
                }
                else
                {
                    GUI.color = new Color(1f, 0.5f, 0f);
                    GUILayout.Label(T("0%", "0%", "0%"), EditorStyles.label, GUILayout.Width(100));
                }
                GUI.color = Color.white;

                EditorGUILayout.EndHorizontal();

                // Show usage details if file is used
                if (node.result.isUsed && node.result.usedInObjects.Count > 0)
                {
                    int displayCount = Mathf.Min(2, node.result.usedInObjects.Count);
                    for (int i = 0; i < displayCount; i++)
                    {
                        GameObject go = node.result.usedInObjects[i];
                        if (go != null)
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(15 + (depth + 1) * 15 + 18);
                            
                            GUI.color = new Color(0.6f, 0.6f, 0.6f);
                            if (GUILayout.Button($"→ {go.name}", EditorStyles.miniLabel, GUILayout.ExpandWidth(true)))
                            {
                                EditorGUIUtility.PingObject(go);
                                Selection.activeGameObject = go;
                            }
                            GUI.color = Color.white;
                            
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                    
                    if (node.result.usedInObjects.Count > displayCount)
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(15 + (depth + 1) * 15 + 18);
                        GUI.color = new Color(0.6f, 0.6f, 0.6f);
                        GUILayout.Label($"... and {node.result.usedInObjects.Count - displayCount} more", EditorStyles.miniLabel);
                        GUI.color = Color.white;
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
        }

        private bool NodeMatchesSearch(TreeNode node, string search)
        {
            if (string.IsNullOrEmpty(search))
                return true;

            search = search.ToLower();

            // Check node name
            if (node.name.ToLower().Contains(search))
                return true;

            // Check children
            if (node.isFolder)
            {
                foreach (var child in node.children)
                {
                    if (NodeMatchesSearch(child, search))
                        return true;
                }
            }

            return false;
        }

        private bool GetFoldoutState(string path)
        {
            if (!foldoutStates.ContainsKey(path))
            {
                foldoutStates[path] = false;
            }
            return foldoutStates[path];
        }

        private void SetFoldoutState(string path, bool state)
        {
            foldoutStates[path] = state;
        }

        private void ExpandAll(TreeNode node, bool expand)
        {
            if (node == null)
                return;

            if (node.isFolder)
            {
                SetFoldoutState(node.path, expand);
                foreach (var child in node.children)
                {
                    ExpandAll(child, expand);
                }
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }

        private void DrawAssetResult(UsageResult result, Color labelColor)
        {
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.BeginHorizontal();
            GUI.color = labelColor;
            EditorGUILayout.LabelField(result.isUsed ? "✓" : "✗", GUILayout.Width(20));
            GUI.color = Color.white;

            Object asset = AssetDatabase.LoadAssetAtPath<Object>(result.assetPath);
            if (GUILayout.Button(Path.GetFileName(result.assetPath), EditorStyles.linkLabel))
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
            EditorGUILayout.EndHorizontal();

            if (result.isUsed)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(T("Used in ", "使用箇所: ", "ใช้ใน ") + result.usedInObjects.Count + T(" object(s):", " 個のオブジェクト:", " ออบเจ็กต์:"), EditorStyles.miniLabel);
                
                foreach (var go in result.usedInObjects.Take(10))
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button($"→ {go.name}", EditorStyles.linkLabel))
                    {
                        EditorGUIUtility.PingObject(go);
                        Selection.activeGameObject = go;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (result.usedInObjects.Count > 10)
                {
                    EditorGUILayout.LabelField(T("... and ", "... 他 ", "... และ ") + (result.usedInObjects.Count - 10) + T(" more", " 件", " รายการ"), EditorStyles.miniLabel);
                }
                
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void ShowFolderContextMenu(TreeNode node)
        {
            GenericMenu menu = new GenericMenu();
            
            menu.AddItem(new GUIContent("Ping in Project"), false, () =>
            {
                Object folder = AssetDatabase.LoadAssetAtPath<Object>(node.path);
                if (folder != null)
                {
                    EditorGUIUtility.PingObject(folder);
                    Selection.activeObject = folder;
                }
            });
            
            menu.AddItem(new GUIContent("Open in Explorer"), false, () =>
            {
                string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), node.path);
                EditorUtility.RevealInFinder(fullPath);
            });
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Copy Path"), false, () =>
            {
                EditorGUIUtility.systemCopyBuffer = node.path;
                Debug.Log($"Copied: {node.path}");
            });
            
            menu.AddItem(new GUIContent("Copy Full Path"), false, () =>
            {
                string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), node.path);
                EditorGUIUtility.systemCopyBuffer = fullPath;
                Debug.Log($"Copied: {fullPath}");
            });
            
            menu.ShowAsContext();
        }

        private void ShowFileContextMenu(TreeNode node)
        {
            GenericMenu menu = new GenericMenu();
            
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(node.result.assetPath);
            
            menu.AddItem(new GUIContent("Ping in Project"), false, () =>
            {
                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                    Selection.activeObject = asset;
                }
            });
            
            menu.AddItem(new GUIContent("Open in Explorer"), false, () =>
            {
                string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), node.result.assetPath);
                EditorUtility.RevealInFinder(fullPath);
            });
            
            menu.AddSeparator("");
            
            if (node.result.isUsed && node.result.usedInObjects.Count > 0)
            {
                menu.AddItem(new GUIContent("Select Used In..."), false, null);
                
                foreach (var go in node.result.usedInObjects.Take(10))
                {
                    string goName = go.name;
                    menu.AddItem(new GUIContent($"  Select Used In/{goName}"), false, () =>
                    {
                        EditorGUIUtility.PingObject(go);
                        Selection.activeGameObject = go;
                    });
                }
                
                if (node.result.usedInObjects.Count > 10)
                {
                    menu.AddDisabledItem(new GUIContent($"  Select Used In/... and {node.result.usedInObjects.Count - 10} more"));
                }
                
                menu.AddSeparator("");
            }
            
            menu.AddItem(new GUIContent("Copy Path"), false, () =>
            {
                EditorGUIUtility.systemCopyBuffer = node.result.assetPath;
                Debug.Log($"Copied: {node.result.assetPath}");
            });
            
            menu.AddItem(new GUIContent("Copy Full Path"), false, () =>
            {
                string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), node.result.assetPath);
                EditorGUIUtility.systemCopyBuffer = fullPath;
                Debug.Log($"Copied: {fullPath}");
            });
            
            menu.ShowAsContext();
        }

        private void SaveCache()
        {
            lastCache = new CacheSnapshot
            {
                selectedPath = selectedPath,
                results = results,
                rootNode = rootNode,
                foldoutStates = new Dictionary<string, bool>(foldoutStates),
                sortColumn = currentSortColumn,
                sortAscending = sortAscending
            };
        }

        private void LoadCache()
        {
            if (lastCache == null)
                return;

            selectedPath = lastCache.selectedPath;
            results = lastCache.results ?? new List<UsageResult>();
            rootNode = lastCache.rootNode;
            foldoutStates = lastCache.foldoutStates != null
                ? new Dictionary<string, bool>(lastCache.foldoutStates)
                : new Dictionary<string, bool>();
            currentSortColumn = lastCache.sortColumn;
            sortAscending = lastCache.sortAscending;
        }
    }
}
