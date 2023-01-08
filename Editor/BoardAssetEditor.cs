namespace SceneBoard.Editor
{
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(BoardAsset), true, isFallback = true), CanEditMultipleObjects]
    public class BoardAssetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var asset = (BoardAsset)target;

            DrawDefaultInspector();
            if (GUILayout.Button("Open"))
                BoardEditor.CreateWindow<BoardEditor>().Storage = asset;
        }
    }
}