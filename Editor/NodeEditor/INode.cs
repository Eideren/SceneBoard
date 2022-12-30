namespace SceneBoard.NodeEditor
{
	using UnityEditor;
	using UnityEngine;



	public interface INode : IDraggable
	{
		protected static Color HighlightColor = new( 44 / 255f, 93 / 255f, 135 / 255f );
		protected static Color TitleColor = new( 60 / 255f, 60 / 255f, 60 / 255f );
		protected static Color DefaultBackgroundColor = new( 44 / 255f, 44 / 255f, 44 / 255f );
		
		public float? Width { get; set; }

		public void DrawBackgroundAndTitleBar(ref Rect fieldRect, Rect background, bool isSelected,
			NodeEditorWindow editor)
		{
			DrawBackgroundAndTitleBarDefault(this, ref fieldRect, background, isSelected, editor);
		}

		public static void DrawBackgroundAndTitleBarDefault(INode n, ref Rect fieldRect, Rect background, bool isSelected, NodeEditorWindow editor)
		{
			if(editor.IsInView(background))
				EditorGUI.DrawRect( background, DefaultBackgroundColor );

			var nodeTitle = fieldRect;
			nodeTitle.height /= 2;
			if(editor.IsInView(nodeTitle))
				EditorGUI.DrawRect( nodeTitle, isSelected ? HighlightColor : TitleColor );

			var titleHighlight = nodeTitle;
			titleHighlight.height /= 10;
			if(editor.IsInView(titleHighlight))
				EditorGUI.DrawRect( titleHighlight, HighlightColor );

			fieldRect.position += new Vector2(0, nodeTitle.height * 1.3f);
			var preWidth = fieldRect.width;
			fieldRect.width *= 0.975f;
			fieldRect.x -= ( fieldRect.width - preWidth ) * 0.5f;
		}

		public void OnDraw( NodeDrawer drawer );
	}
}