using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LogicCuteGuy.Editor
{
    public class RandomRotateChildrenMenu : LogicCuteGuyEditorWindow
    {
        private enum RotationMode
        {
            AbsoluteRange,
            OffsetFromCurrent
        }

        private bool rotateSelectedObjects = false;
        private bool rotateChildrenRecursive = true;
        private bool requireMeshRenderer = true;
        private RotationMode rotationMode = RotationMode.OffsetFromCurrent;
        private Vector3 minRotation = new Vector3(0f, 0f, 0f);
        private Vector3 maxRotation = new Vector3(0f, 360f, 0f);
        private Vector3 maxAngleFromCurrent = new Vector3(0f, 180f, 0f);

        [MenuItem("GameObject/LogicCuteGuy/Random Rotate Children")]
        private static void ShowWindow()
        {
            RandomRotateChildrenMenu window = GetWindow<RandomRotateChildrenMenu>(T("Random Rotate Children", "子をランダム回転", "สุ่มหมุนลูก"));
            window.minSize = new Vector2(360f, 220f);
        }

        protected override void OnWindowGUI()
        {
            EditorGUILayout.HelpBox(T("Apply random local rotation to targets with a MeshRenderer.", "MeshRendererを持つターゲットにランダムなローカル回転を適用します。", "ใช้การหมุนแบบสุ่มกับเป้าหมายที่มี MeshRenderer"), MessageType.Info);

            rotateSelectedObjects = EditorGUILayout.Toggle(T("Rotate Selected Objects", "選択オブジェクトを回転", "หมุนออบเจ็กต์ที่เลือก"), rotateSelectedObjects);
            rotateChildrenRecursive = EditorGUILayout.Toggle(T("Rotate Children Recursive", "子を再帰的に回転", "หมุนลูกแบบวนซ้ำ"), rotateChildrenRecursive);
            requireMeshRenderer = EditorGUILayout.Toggle(T("Require MeshRenderer", "MeshRendererが必須", "ต้องมี MeshRenderer"), requireMeshRenderer);
            
            EditorGUILayout.Space();

            rotationMode = (RotationMode)LCG_EnumPopup(T("Rotation Mode", "回転モード", "โหมดการหมุน"), rotationMode,
                new string[] { "Absolute Range", "Offset From Current" },
                new string[] { "絶対範囲", "現在値からのオフセット" },
                new string[] { "ช่วงสัมบูรณ์", "ออฟเซ็ตจากปัจจุบัน" });

            EditorGUILayout.Space();

            if (rotationMode == RotationMode.OffsetFromCurrent)
            {
                maxAngleFromCurrent = EditorGUILayout.Vector3Field(T("Max Angle From Current", "現在値からの最大角度", "มุมสูงสุดจากปัจจุบัน"), maxAngleFromCurrent);
            }
            else if (rotationMode == RotationMode.AbsoluteRange)
            {
                minRotation = EditorGUILayout.Vector3Field(T("Min Rotation", "最小回転", "การหมุนต่ำสุด"), minRotation);
                maxRotation = EditorGUILayout.Vector3Field(T("Max Rotation", "最大回転", "การหมุนสูงสุด"), maxRotation);
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0))
            {
                if (GUILayout.Button(T("Apply To Selected", "選択オブジェクトに適用", "นำไปใช้กับสิ่งที่เลือก"), GUILayout.Height(30)))
                {
                    ApplyRotationToSelection();
                }
            }

            if (Selection.gameObjects.Length == 0)
            {
                EditorGUILayout.HelpBox(T("Select one or more parent GameObjects in the Hierarchy.", "ヒエラルキーで1つ以上の親オブジェクトを選択してください。", "เลือก GameObjects พาเรนต์หนึ่งรายการขึ้นไปในลำดับชั้น"), MessageType.None);
            }
        }

        private void ApplyRotationToSelection()
        {
            if (Selection.gameObjects.Length == 0)
            {
                Debug.LogWarning("No GameObjects selected.");
                return;
            }

            System.Collections.Generic.HashSet<Transform> transformsToRotate = new System.Collections.Generic.HashSet<Transform>();

            foreach (GameObject selectedObject in Selection.gameObjects)
            {
                if (rotateSelectedObjects)
                {
                    transformsToRotate.Add(selectedObject.transform);
                }

                if (rotateChildrenRecursive)
                {
                    Transform[] allChildren = selectedObject.GetComponentsInChildren<Transform>(true);
                    foreach (Transform child in allChildren)
                    {
                        if (child != selectedObject.transform)
                        {
                            transformsToRotate.Add(child);
                        }
                    }
                }
            }

            int processedCount = 0;
            Undo.SetCurrentGroupName("Random Rotate Targets");
            int undoGroup = Undo.GetCurrentGroup();

            System.Collections.Generic.HashSet<UnityEngine.SceneManagement.Scene> dirtyScenes = new System.Collections.Generic.HashSet<UnityEngine.SceneManagement.Scene>();

            foreach (Transform t in transformsToRotate)
            {
                if (!requireMeshRenderer || t.GetComponent<MeshRenderer>() != null)
                {
                    Undo.RecordObject(t, T("Random Rotate", "ランダム回転", "หมุนแบบสุ่ม"));
                    t.localRotation = GetRandomRotation(t);
                    EditorUtility.SetDirty(t);
                    dirtyScenes.Add(t.gameObject.scene);
                    processedCount++;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            foreach (var scene in dirtyScenes)
            {
                if (scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            Debug.Log($"Applied random rotation to {processedCount} object(s).");
        }

        private Quaternion GetRandomRotation(Transform child)
        {
            if (rotationMode == RotationMode.OffsetFromCurrent)
            {
                Vector3 currentEuler = child.localEulerAngles;
                Vector3 randomOffset = new Vector3(
                    Random.Range(-maxAngleFromCurrent.x, maxAngleFromCurrent.x),
                    Random.Range(-maxAngleFromCurrent.y, maxAngleFromCurrent.y),
                    Random.Range(-maxAngleFromCurrent.z, maxAngleFromCurrent.z));

                return Quaternion.Euler(currentEuler + randomOffset);
            }

            if (rotationMode == RotationMode.AbsoluteRange)
            {
                return Quaternion.Euler(
                    Random.Range(minRotation.x, maxRotation.x),
                    Random.Range(minRotation.y, maxRotation.y),
                    Random.Range(minRotation.z, maxRotation.z));
            }

            return child.localRotation;
        }
    }
}