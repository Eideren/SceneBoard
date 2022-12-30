namespace SceneBoard.NodeEditor
{
	using System;
	using UnityEditor;
	using UnityEngine;
	using System.Collections.Generic;
	using Framework;
	using static UnityEngine.Debug;



	public class NodeEditorWindow : EditorWindow
	{
		protected Vector2 FieldSize = new( 320, 24 );
		protected int FontSize = 16;
		protected float LineWidth = 5f;

		public readonly List<(Vector2 a, Vector2 b, Color color)> Lines = new();
		public readonly WeakCache<INode, NodeDrawer> Drawers = new();
		public readonly WeakCacheEnum<object, RectRef> LinkTargets = new();
		public readonly HashSet<IDraggable> Selection = new();
		public GUIStyle GUIStyle => _cachedStyle ??= new GUIStyle( EditorStyles.whiteLabel ) { fontSize = FontSize };
		public GUIStyle GUIStyleFields => _cachedStyleFields ??= new GUIStyle( EditorStyles.numberField ) { fontSize = FontSize };
		public GUIStyle GUIStyleCentered => _cachedStyleCentered ??= new GUIStyle( EditorStyles.whiteLabel ) { fontSize = FontSize, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
		public GUIStyle GUIStyleWordWrapped => _cachedWWStyle ??= new GUIStyle( EditorStyles.whiteLabel ) { fontSize = FontSize, wordWrap = true };
		public float ZoomLevel
		{
			get => _zoomLevel;
			set
			{
				if(_zoomLevel == value)
					return;

				_zoomLevel = value;
				var scaledFontSize = (int) ( FontSize * ZoomLevel );
				scaledFontSize = scaledFontSize <= 0 ? 1 : scaledFontSize;
				GUIStyleFields.fontSize = scaledFontSize;
				GUIStyle.fontSize = scaledFontSize;
				GUIStyleCentered.fontSize = (int) ( FontSize * ZoomLevel * 1.5f );
				GUIStyleCentered.fontSize = GUIStyleCentered.fontSize <= 0 ? 1 : GUIStyleCentered.fontSize;
				GUIStyleWordWrapped.fontSize = scaledFontSize;
				ScheduleRepaint = true;
			}
		}
		public Vector2 ViewportPosition;
		public Vector2 ViewportCenter => ViewportPosition + ViewportSize * 0.5f / ZoomLevel;
		public Vector2 ViewportSize => position.size;
		public INode NodeUnderMouse { get; private set; }


		protected float Separation
		{
			get => _separation;
			set
			{
				if(_separation == value)
					return;
				_separation = value;
				ScheduleNextRepaint = true;
			}
		}
		public bool ScheduleRepaint = false;
		public bool ScheduleNextRepaint = false;

		IDraggable _viewportNode;
		bool _centerOnInit = true;
		float _zoomLevel = 1f;
		float _separation = 1f;
		GUIStyle _cachedStyle, _cachedWWStyle, _cachedStyleFields, _cachedStyleCentered;
		bool _realtime;
		List<INode> _dummyNodes;
		List<(GUIContent content, Action action)> _buttonsCache;
		(GUIContent label, float v) _zoomCache = (new GUIContent("0"), 0f);
		(GUIContent label, float v) _separationCache = (new GUIContent("0"), 0f);
		Vector2? _boxSelection;

		static Material _sLineMat;
		static readonly List<object> _tempBuffer = new();


		[ MenuItem( "Window/Dummy Node Editor" ) ]
		static void Init()
		{
			var window = CreateInstance<NodeEditorWindow>();
			window.Show();
		}



		public NodeEditorWindow()
		{
			_viewportNode = new ViewportNode { Viewport = this };
			titleContent = new GUIContent( GetType().Name );
		}



		void OnGUI()
		{
			if( ScheduleNextRepaint && Event.current.type == EventType.Repaint )
			{
				ScheduleNextRepaint = false;
				ScheduleRepaint = true;
			}

			OnGUIDraw();
			
			if( ScheduleRepaint )
			{
				ScheduleRepaint = false;
				Repaint();
			}
		}



		protected virtual IEnumerable<INode> Nodes()
		{
			_dummyNodes ??= new List<INode> { new DummyNode(), new DummyNode(), new DummyNode() };
			return _dummyNodes;
		}

		

		protected virtual void PrepareToolbar(List<(GUIContent, Action)> buttons)
		{
			buttons.Add((_zoomCache.label, () => ZoomLevel = 1f));
			buttons.Add((_separationCache.label, () => Separation = 1f));
			buttons.Add((new GUIContent("+"), () => Separation *= 2f));
			buttons.Add((new GUIContent("-"), () => Separation *= 0.5f));
			var realtimeSwapCache = new GUIContent(
				_realtime ? "Realtime Enabled" : "Realtime Disabled", 
				"Realtime forces the window to always redraw which wastes performance");
			buttons.Add((realtimeSwapCache, () =>
			{
				_realtime = !_realtime;
				realtimeSwapCache.text = _realtime ? "Realtime Enabled" : "Realtime Disabled";
			}));
		}



		protected virtual void OnGUIDraw()
		{
			if( _centerOnInit )
			{
				_centerOnInit = false;
				CenterView();
			}

			wantsMouseMove = true;
			wantsMouseEnterLeaveWindow = true;

			var e = Event.current;

			// Moving viewport
			if( e.type == EventType.MouseDrag && e.button == 2 )
			{
				_viewportNode.Pos += e.delta / ZoomLevel;
				Repaint();
			}
			
			if(GUIUtility.keyboardControl == 0)
			{
				// Moving selection
				if( e.type == EventType.MouseDrag && e.button == 0 && _boxSelection.HasValue == false )
				{
					foreach (var draggable in Selection)
						draggable.Pos += e.delta / ZoomLevel;
					e.Use();
					Repaint();
				}
				
				// Center viewport
				if( e.type == EventType.KeyDown && e.keyCode == KeyCode.F )
				{
					CenterView();
					e.Use();
					Repaint();
				}
			}

			if( e.type == EventType.Repaint )
			{
				foreach( var line in Lines )
					DrawLine( line.a, line.b, line.color, LineWidth * ZoomLevel );
			}
			Lines.Clear();

			if( e.type == EventType.ScrollWheel )
				ZoomLevel *= e.delta.y < 0 ? 1.1f : 0.9f;

			foreach( var (_, rRef) in LinkTargets )
				rRef.upToDate = false;

			var viewportCenter = ViewportCenter;
			
			// center square
			EditorGUI.DrawRect(new Rect( (new Vector2(-5, -5) * _separation + viewportCenter) * _zoomLevel, new Vector2(10, 10) * _zoomLevel ), Color.black);
			
			{
				INode nodeUnderMouse = null;
				foreach( INode node in Nodes() )
				{
					var drawer = Drawers[ node ];
					var fieldSize = new Vector2(node.Width ?? FieldSize.x, FieldSize.y);
					var fieldRect = new Rect( (node.Pos * _separation + viewportCenter) * _zoomLevel, fieldSize * _zoomLevel );
					var nodeSurface = fieldRect;
					nodeSurface.height = drawer.SurfaceCovered.height + fieldRect.height * 0.1f;
					
					node.DrawBackgroundAndTitleBar(ref fieldRect, nodeSurface, Selection.Contains(node), this);
					drawer.Clear( fieldRect, this );
					node.OnDraw( drawer );

					if (nodeSurface.Contains(e.mousePosition))
						nodeUnderMouse = node;
				}
				NodeUnderMouse = nodeUnderMouse;
			}

			if (_buttonsCache == null)
			{
				_buttonsCache = new List<(GUIContent, Action)>();
				PrepareToolbar(_buttonsCache);
			}

			// Toolbar rendering
			{
				var style = EditorStyles.toolbarButton;
				float buttonHeight = 0f;
				var totalWidth = 0f;
				Span<float> widths = stackalloc float[_buttonsCache.Count];
				for (int i = 0; i < widths.Length; i++)
				{
					var size = style.CalcSize(_buttonsCache[i].content);
					totalWidth += widths[i] = size.x;
					buttonHeight = Mathf.Max(size.y, buttonHeight);
				}

				var ratio = totalWidth / position.width;
				ratio = ratio < 1f ? 1f : ratio;

				float offset = 0f;
				for (int i = 0; i < widths.Length; i++)
				{
					var width = widths[i] / ratio;
					if (GUI.Button(new Rect(offset, 0, width, buttonHeight), _buttonsCache[i].content, style))
						_buttonsCache[i].action();
					offset += width;
				}
			}

			if (e.type == EventType.MouseDown && e.button == 0 && GUIUtility.keyboardControl != 0)
			{
				// Clicked on a non-interactive element, likely node background or window background,
				// make sure keyboard control is lost in such cases
				GUIUtility.keyboardControl = 0;
				Repaint();
			}

			// Box selection
			if (e.type == EventType.MouseDown && e.button == 0 && NodeUnderMouse == null)
			{
				_boxSelection = ViewportToWorld(e.mousePosition);
				Selection.Clear();
				e.Use();
				Repaint();
			}
			else if (e.type == EventType.MouseUp && e.button == 0 && _boxSelection.HasValue)
			{
				_boxSelection = null;
			}

			// Selecting individual nodes
			if (e.type == EventType.MouseDown && e.button == 0 && NodeUnderMouse != null)
			{
				// Change to only select this node when it wasn't previously selected and not included through ctrl/shift
				if (Selection.Contains(NodeUnderMouse) == false && (e.shift || e.control) == false)
					Selection.Clear();
				
				Selection.Add(NodeUnderMouse);
				e.Use();
				Repaint();
			}

			if (_boxSelection.HasValue)
			{
				NodeUnderMouse = null;
				var start = WorldToViewport(_boxSelection.Value);
				var end = e.mousePosition;
				var min = new Vector2(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y));
				var max = new Vector2(Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y));
				var rect = new Rect(min.x, min.y, max.x-min.x, max.y-min.y);
				var color = Color.blue;
				color.a = 0.1f;
				EditorGUI.DrawRect(rect, color);
				Repaint();
				if (e.type == EventType.MouseDrag)
				{
					Selection.Clear();
					foreach (var node in Nodes())
					{
						if (Drawers.TryGetValue(node, out var drawer) && rect.Overlaps(drawer.SurfaceCovered))
							Selection.Add(node);
					}
				}
			}

			{
				foreach( var (obj, rRef) in LinkTargets )
					if(rRef.upToDate == false)
						_tempBuffer.Add( obj );

				foreach( object o in _tempBuffer )
					LinkTargets.Remove( o );
				
				_tempBuffer.Clear();
			}

			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if( _zoomCache.v != ZoomLevel )
			{
				_zoomCache.label.text = $"Zoom:{ZoomLevel:F}";
				_zoomCache.v = ZoomLevel;
			}
			
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if( _separationCache.v != Separation )
			{
				_separationCache.label.text = $"Separation:{Separation:F}";
				_separationCache.v = Separation;
			}

			ScheduleRepaint = ScheduleRepaint || _realtime;
		}



		public Vector2 WorldToViewport( Vector2 p ) => ( p * _separation + ViewportCenter ) * ZoomLevel;
		public Vector2 ViewportToWorld( Vector2 p ) => (p / ZoomLevel - ViewportCenter) /  _separation;



		public void CenterView()
		{
			var med = Vector2.zero;
			int count = 0;
			foreach( INode node in Nodes() )
			{
				if( Drawers.TryGetValue( node, out var drawer ) )
				{
					// Frequent path, just send center without transforming for perf'
					med += drawer.SurfaceCovered.center;
				}
				else
				{
					// Infrequent path, do inverse transformation to counteract the transformation post average
					med += WorldToViewport(node.Pos);
				}

				count++;
			}

			if( count == 0 )
				return;

			med /= count;
			med = ViewportToWorld( med );

			if( med.magnitude > 100f / Separation )
			{
				LogWarning( "Re-centered nodes around origin, median distance was greater than 100" );
				foreach( var node in Nodes() )
					node.Pos -= med;
				med = default;
			}

			ViewportPosition = - med;
			ScheduleRepaint = true;
		}



		static void DrawLine( Vector2 pointA, Vector2 pointB, Color color, float width = 5f )
		{
			var perp = pointB - pointA;
			perp = new Vector2( perp.y, perp.x );
			perp = perp.normalized * width;

			if( _sLineMat == null )
			{
				Shader shader = Shader.Find( "Hidden/Internal-Colored" );
				_sLineMat = new Material( shader );
				_sLineMat.hideFlags = HideFlags.HideAndDontSave;
				_sLineMat.SetInt( "_SrcBlend", (int) UnityEngine.Rendering.BlendMode.SrcAlpha );
				_sLineMat.SetInt( "_DstBlend", (int) UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha );
				_sLineMat.SetInt( "_Cull", (int) UnityEngine.Rendering.CullMode.Off );
				_sLineMat.SetInt( "_ZWrite", 0 );
			}

			_sLineMat.SetPass( 0 );
			GL.Begin( GL.QUADS );
			GL.Color( color );
			GL.Vertex3( pointA.x + perp.x, pointA.y - perp.y, 0 );
			GL.Vertex3( pointA.x - perp.x, pointA.y + perp.y, 0 );
			GL.Vertex3( pointB.x - perp.x, pointB.y + perp.y, 0 );
			GL.Vertex3( pointB.x + perp.x, pointB.y - perp.y, 0 );
			GL.End();
		}




		public bool IsInView(Rect r)
		{
			var screenRect = new Rect( Vector2.zero, ViewportSize );
			return screenRect.Overlaps( r );
		}



		class ViewportNode : INode
		{
			public NodeEditorWindow Viewport;


			public Vector2 Pos
			{
				get => Viewport.ViewportPosition;
				set => Viewport.ViewportPosition = value;
			}

			public float? Width { get; set; } = null;
			public void OnDraw( NodeDrawer drawer ) => throw new InvalidOperationException();
		}



		class DummyNode : INode
		{
			public Vector2 Pos{ get; set; }

			public float? Width { get; set; } = null;

			float someFloat = 101f;
			object link;
			bool init;
			
			public void OnDraw( NodeDrawer drawer )
			{
				if( init == false )
				{
					using var v = drawer.Editor.Nodes().GetEnumerator();
					v.MoveNext();
					link ??= v.Current;
					init = true;
				}

				drawer.DrawProperty( ref someFloat, nameof( someFloat ), out _ );
				drawer.MarkNextAsReceiver( this, ref link );
				drawer.MarkNextAsInput( this );
				drawer.DrawLabel( "Link" );
				drawer.DrawProperty( ref someFloat, nameof( someFloat ), out _ );
				drawer.DrawProperty( ref someFloat, nameof( someFloat ), out _ );
			}
		}



		public class RectRef
		{
			public Rect rect;
			public bool upToDate;
		}
	}
}