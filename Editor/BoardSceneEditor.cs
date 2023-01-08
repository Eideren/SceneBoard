namespace SceneBoard.Editor
{
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(SceneBoardStorage), true, isFallback = true), CanEditMultipleObjects]
    public class BoardSceneEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var asset = (SceneBoardStorage)target;

            DrawDefaultInspector();
            if (GUILayout.Button("Open"))
                BoardEditor.CreateWindow<BoardEditor>().Storage = asset;
        }
    }
}