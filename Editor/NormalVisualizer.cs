using UnityEngine;
using UnityEditor;

namespace LDraw
{
    [CustomEditor(typeof(MeshFilter))]
    public class NormalVisualizer : UnityEditor.Editor
    {
        private static bool showNormals = false;
        private static float normalLength = 0.1f;
        
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Normal Visualization", EditorStyles.boldLabel);
            
            showNormals = EditorGUILayout.Toggle("Show Normals", showNormals);
            normalLength = EditorGUILayout.Slider("Normal Length", normalLength, 0.01f, 1f);
            
            if (GUI.changed)
            {
                SceneView.RepaintAll();
            }
        }
        
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        static void DrawNormalGizmos(MeshFilter meshFilter, GizmoType gizmoType)
        {
            if (!showNormals || meshFilter == null)
                return;
            
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null)
                return;
            
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            
            if (normals == null || normals.Length == 0)
                return;
            
            Transform transform = meshFilter.transform;
            
            Handles.color = Color.cyan;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 worldPos = transform.TransformPoint(vertices[i]);
                Vector3 worldNormal = transform.TransformDirection(normals[i]);
                
                Handles.DrawLine(worldPos, worldPos + worldNormal * normalLength);
                Handles.DrawWireDisc(worldPos + worldNormal * normalLength, worldNormal, normalLength * 0.1f);
            }
        }
    }
}
