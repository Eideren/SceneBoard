namespace SceneBoard.NodeEditor
{
	using System;
	using System.Collections.Generic;
	using JetBrains.Annotations;
	using UnityEditor;
	using UnityEngine;



	public class NodeDrawer
	{
		public const int ClippedFontSize = 4;
		public Rect NextRect;
		public Rect SurfaceCovered;
		public float Margin;
		public NodeEditorWindow Editor{ get; private set; }
		public bool PreviouslyOutOfView{ get; private set; }
		public Color DefaultLine = new( 33 / 255f, 33 / 255f, 33 / 255f );
		int _rectIndex;
		Rect _screenRect;
		bool _firstRect;


		public abstract class Connection : IDraggable
		{
			public Vector2 Pos { get; set; }
		}

		public class ConnectionTarget<T> : Connection
		{
			public T Target;
		}
		public class ConnectionKey<T> : Connection
		{
			public T Key;
		}



		public void Clear( Rect newRect, NodeEditorWindow newEditor )
		{
			Editor = newEditor;
			_screenRect = new Rect( Vector2.zero, Editor.ViewportSize );
			_rectIndex = 0;
			PreviouslyOutOfView = IsInView( SurfaceCovered ) == false;
			NextRect = newRect;
			SurfaceCovered = newRect;
			_firstRect = false;
			Margin = newRect.height * 0.1f;
		}



		public void MarkNextAsInput<T>( T target, bool pointOnRightSide = false, Color? color = default ) where T : class
		{
			bool isDragSource = Editor.Selection.FirstOrDefault() is ConnectionTarget<T> connection && ReferenceEquals(connection.Target, target);
			var rect = ConnectionRect( pointOnRightSide );
			var rRef = Editor.LinkTargets[ target ];
			rRef.rect = rect;
			rRef.upToDate = true;
			
			if( NewConnection( isDragSource, null, rect, color, out _ ) )
			{
				Editor.Selection.Clear();
				Editor.Selection.Add(new ConnectionTarget<T>() { Pos = rect.center, Target = target });
				Event.current.Use();
			}
		}
		
		

		/// <summary>
		/// Setup a receiver for a connection, this is an item which draws and receives connection changes.
		/// Draws a connection to a <paramref name="target"/> which is used in a <see cref="MarkNextAsInput{T}"/>.
		/// If this connection is changed by the user, <paramref name="target"/> will be changed to the item this is now connected to.
		/// </summary>
		/// <param name="key">
		/// Can be any kind of object as long as it uniquely identifies this specific call and is stable.
		/// For example, for a field you could use a tuple of (name of the field, object containing that field)
		/// As long as no other <see cref="MarkNextAsReceiver{TKey,TTarget}"/> of this editor are using those two values they won't be problematic
		/// </param>
		/// <param name="target">
		/// The current value that this field contains,
		/// 'will create a line automatically to it if something called <see cref="MarkNextAsInput{T}"/> with this value as parameter
		/// </param>
		/// <param name="pointOnRightSide">Whether to put the link anchor on the right or left side</param>
		/// <param name="color">The color of the link anchor</param>
		/// <param name="acceptableTarget">
		/// Tests whether a new connection is valid,
		/// return true if the potential new target is valid for the given key,
		/// if it was valid and the user finished the connection this method will return true and <paramref name="target"/> will be assigned to the linked value.
		/// Note that the target might be null if user cut the connection, you should return true if you allow null values.
		/// Note also that if this delegate is null this method allows null and all values of type <see cref="TTarget"/>.
		/// </param>
		/// <returns>True when connection has changed - <paramref name="target"/> has been assigned to a new value</returns>
		public bool MarkNextAsReceiver<TKey, TTarget>( TKey key, [ CanBeNull ] ref TTarget target, bool pointOnRightSide = true, Color? color = default, Func<TKey, TTarget, bool> acceptableTarget = null ) where TTarget : class
		{
			var e = Event.current;
			bool isDragSource = Editor.Selection.FirstOrDefault() is ConnectionKey<TKey> conn && key.Equals( conn.Key );
			var rect = ConnectionRect( pointOnRightSide );
			
			if( NewConnection( isDragSource, target, rect, color, out bool mouseHover ) )
			{
				Editor.Selection.Clear();
				Editor.Selection.Add(new ConnectionKey<TKey>() { Pos = rect.center, Key = key });
				isDragSource = true;
				Event.current.Use();
			}

			if( isDragSource )
			{
				foreach( var (k, v) in Editor.LinkTargets )
				{
					if( k is TTarget asTTarget
					    && ( acceptableTarget == null || acceptableTarget( key, asTTarget ) ) 
					    && IsInView( v.rect ) )
					{
						var cpyRect = v.rect;
						cpyRect.min -= v.rect.size * 0.2f;
						cpyRect.max += v.rect.size * 0.2f;
						EditorGUI.DrawRect( cpyRect, Color.green );
					}
				}
			}

			{
				if( Editor.Selection.FirstOrDefault() is ConnectionTarget<TTarget> connection
				    && IsInView( rect )
				    && (acceptableTarget == null || acceptableTarget(key, connection.Target))  )
				{
					var cpyRect = rect;
					cpyRect.min -= rect.size * 0.2f;
					cpyRect.max += rect.size * 0.2f;
					EditorGUI.DrawRect( cpyRect, Color.green );
				}
			}


			if( e.type == EventType.MouseUp && e.button == 0)
			{
				bool changedTarget = false;
				// Linking target to this
				{
					if( mouseHover 
					    && Editor.Selection.FirstOrDefault() is ConnectionTarget<TTarget> connection
					    && (acceptableTarget == null || acceptableTarget(key, connection.Target))  )
					{
						target = connection.Target;
						changedTarget = true;
					}
				}

				if(  Editor.Selection.FirstOrDefault() is ConnectionKey<TKey> keyConn && key.Equals( keyConn.Key ) )
				{
					// Linking this to target under cursor ?
					foreach( var (k, v) in Editor.LinkTargets )
					{
						if( v.rect.Contains( e.mousePosition )
						    && k is TTarget asTTarget
						    && ( acceptableTarget == null || acceptableTarget( key, asTTarget ) ) )
						{
							target = asTTarget;
							changedTarget = true;
						}
					}

					// Released over nothing
					if( changedTarget == false && ( acceptableTarget == null || acceptableTarget( key, null ) ) )
					{
						target = null;
						changedTarget = true;
					}
				}
				
				if( changedTarget )
				{
					Editor.Selection.Clear();
					Editor.ScheduleRepaint = true;
					Event.current.Use();
					return true;
				}
			}

			return false;
		}



		Rect ConnectionRect( bool pointOnRightSide )
		{
			var rect = NextRect;
			rect.height *= 0.66f;
			rect.width = rect.height;
			rect.y += rect.height * 0.25f;
			rect.x += pointOnRightSide ? NextRect.width : - rect.width;
			return rect;
		}



		bool NewConnection( bool isDraggingSource, [ CanBeNull ] object lineTarget, Rect rect, Color? color, out bool hover )
		{
			var e = Event.current;
			hover = false;

			var baseColor = ( color ?? DefaultLine );
			var activeColor = baseColor + Color.white * 0.4f;

			if( isDraggingSource )
			{
				Editor.Lines.Add( ( rect.center, e.mousePosition, activeColor ) );
				hover = true;
			}
			else if( lineTarget != null && Editor.LinkTargets.TryGetValue( lineTarget, out var targetRect ) )
			{
				var origin = rect.center;

				var lineDelta = targetRect.rect.center - origin;
				var mouseDelta = e.mousePosition - origin;

				float deltaDots = Vector3.Dot( mouseDelta, lineDelta );
				var vectorAlongLine = lineDelta * deltaDots / Vector3.Dot( lineDelta, lineDelta );
				var pointOnLine = origin + vectorAlongLine;

				// Is the cursor on this line, note the distance is just a random 10 units constant, not the actual line width, doesn't really matter and could help with low zoom
				hover = hover || Editor.Selection.FirstOrDefault() is not Connection && vectorAlongLine.sqrMagnitude <= lineDelta.sqrMagnitude && deltaDots > 0f && Vector2.Distance( pointOnLine, e.mousePosition ) < 10f;

				Editor.Lines.Add( ( rect.center, targetRect.rect.center, ( hover ? activeColor : baseColor ) ) );
			}
			
			hover = hover || rect.Contains( e.mousePosition );

			if( IsInView( rect ) )
				EditorGUI.DrawRect( rect, ( hover ? activeColor : baseColor ) );

			if( hover && e.type == EventType.MouseMove )
			{
				Editor.ScheduleRepaint = true;
				Editor.ScheduleNextRepaint = true; // *Next*Repaint so that we paint when mouse leaves as well
			}

			return hover && e.type == EventType.MouseDown && e.button == 0 && Editor.Selection.FirstOrDefault() is not Connection;
		}



		public void MoveNextRectRaw(float unscaledHeight)
		{
			NextRect.y += unscaledHeight;
			SurfaceCovered.height += unscaledHeight;
		}



		public Rect UseRect(float heightMult = 1f, bool drawOtherLineRect = true)
		{
			if(_firstRect)
				SurfaceCovered.height += Margin;
			_firstRect = true;

			var outRect = NextRect;
			outRect.height *= heightMult;
			MoveNextRectRaw(outRect.height);
			NextRect.y += Margin;

			if( drawOtherLineRect && ++_rectIndex % 2 == 0 && IsInView( outRect ) )
				EditorGUI.DrawRect( outRect, new Color( 1f, 1f, 1f, 0.02f ) );

			return outRect;
		}



		public bool IsNextInView() => PreviouslyOutOfView == false && IsInView( NextRect );



		public bool IsInView( Rect r ) => _screenRect.Overlaps( r );



		public bool IsInView(out Rect r, float heightMul = 1f, bool drawOtherLineRect = true)
		{
			r = UseRect( heightMul, drawOtherLineRect );
			return IsInView( r );
		}



		public int GetRelativeFontSize(int BaseFontSize)
		{
			var scaledFontSize = Mathf.FloorToInt( BaseFontSize * Editor.ZoomLevel );
			return scaledFontSize <= 0 ? 1 : scaledFontSize;
		}



		public void Split( Rect r, out Rect halfA, out Rect halfB )
		{
			halfA = r;
			halfA.width *= 0.66f;
			halfB = r;
			halfB.x += halfA.width;
			halfB.width *= 0.34f;
		}



		public void DrawLabel( string s )
		{
			if( IsNextInView() == false || Editor.GUIStyle.fontSize < ClippedFontSize )
			{
				UseRect();
				return;
			}
			var r = UseRect();
			GUI.Label( r, s, Editor.GUIStyle );
		}



		public void DrawProperty<T>( ref T field, string name, out bool valueChanged )
		{
			if( IsNextInView() == false || Editor.GUIStyle.fontSize < ClippedFontSize )
			{
				UseRect();
				valueChanged = false;
				return;
			}

			EditorGUI.BeginChangeCheck();
			try
			{
				Split( UseRect(), out var labelRect, out var valueRect );
				GUI.Label( labelRect, name, Editor.GUIStyle );
				GenericBranch<T>.DrawField(ref field, valueRect, Editor.GUIStyle);
			}
			finally
			{
				valueChanged = EditorGUI.EndChangeCheck();
			}
		}



		public bool Handles<T>(T field) => GenericBranch<T>.Handles(field);

		static class GenericBranch<T>
		{
			static bool? _val;
			static Delegate _drawer;

			public static void DrawField(ref T value, Rect valueRect, GUIStyle style)
			{
				_drawer ??= value switch
				{
					Boolean => (Func<bool, Rect, GUIStyle, bool>)((v, valueRect, style) =>
					{
						if (GUI.Button(valueRect, v ? "True" : "False", style)) v = !v;
						return v;
					}),
					Byte => (Func<Byte, Rect, GUIStyle, Byte>)((v, valueRect, style) => (byte)EditorGUI.IntField(valueRect, v, style)),
					SByte => (Func<SByte, Rect, GUIStyle, SByte>)((v, valueRect, style) => (sbyte)EditorGUI.IntField(valueRect, v, style)),
					Int16 => (Func<Int16, Rect, GUIStyle, Int16>)((v, valueRect, style) => (short)EditorGUI.IntField(valueRect, v, style)),
					UInt16 => (Func<UInt16, Rect, GUIStyle, UInt16>)((v, valueRect, style) => (ushort)EditorGUI.IntField(valueRect, v, style)),
					Int32 => (Func<Int32, Rect, GUIStyle, Int32>)((v, valueRect, style) => EditorGUI.IntField(valueRect, v, style)),
					UInt32 => (Func<UInt32, Rect, GUIStyle, UInt32>)((v, valueRect, style) => (uint)EditorGUI.LongField(valueRect, v, style)),
					Int64 => (Func<Int64, Rect, GUIStyle, Int64>)((v, valueRect, style) => EditorGUI.LongField(valueRect, v, style)),
					UInt64 => (Func<UInt64, Rect, GUIStyle, UInt64>)((v, valueRect, style) => (ulong)EditorGUI.LongField(valueRect, (long)v, style)),
					Single => (Func<Single, Rect, GUIStyle, Single>)((v, valueRect, style) => EditorGUI.FloatField(valueRect, v, style)),
					Double => (Func<Double, Rect, GUIStyle, Double>)((v, valueRect, style) => EditorGUI.DoubleField(valueRect, v, style)),
					String => (Func<String, Rect, GUIStyle, String>)((v, valueRect, style) => EditorGUI.TextField(valueRect, v, style)),
					_ => (Func<T, Rect, GUIStyle, T>)((v, valueRect, style) =>
					{
						GUI.Label(valueRect, $"unsupported type:'{typeof(T).Name}'", style);
						return v;
					})
				};

				value = ((Func<T, Rect, GUIStyle, T>)_drawer).Invoke(value, valueRect, style);
			}

			public static bool Handles(T value)
			{
				if (_val.HasValue == false)
				{
					switch( value )
					{
						case Boolean _:
						case Byte _:
						case SByte _:
						case Int16 _:
						case UInt16 _:
						case Int32 _:
						case UInt32 _:
						case Int64 _:
						case UInt64 _:
						case Single _:
						case Double _:
						case String _:
							_val = true;
							break;
						default: 
							_val = false;
							break;
					}
				}
				return _val.Value;
			}
		}
	}

	public static class Extensions
	{
		public static T FirstOrDefault<T>(this HashSet<T> @this)
		{
			for (var e = @this.GetEnumerator(); e.MoveNext(); )
				return e.Current;

			return default;
		}
	}
}