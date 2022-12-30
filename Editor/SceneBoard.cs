﻿namespace SceneBoard.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NodeEditor;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using Object = UnityEngine.Object;


    public class SceneBoard : NodeEditorWindow
    {
        static readonly GUIContent _tempGUIContent = new();
        
        
        
        SceneBoardStorage _storage;
        
        
        
        public bool PingOnClick = true, SelectGO;
        
        

        [MenuItem("Window/Scene Board")]
        public static void OpenSceneBoard()
        {
            var win = CreateWindow<SceneBoard>();
            win.Show();
        }
        
        

        static SceneBoardStorage FindStorage()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.GetComponentInChildren<SceneBoardStorage>() is { } v)
                    return v;
            }

            return new GameObject(nameof(SceneBoardStorage)).AddComponent<SceneBoardStorage>();
        }
        
        
        
        protected override IEnumerable<INode> Nodes()
        {
            _storage ??= FindStorage();
            foreach (var storable in _storage.objs)
                yield return (INode)storable;
        }
        
        

        void Refresh()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var typeName in _storage.AddedByDefault)
            {
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType(typeName, false))
                    .FirstOrDefault(x => x != null);
                
                if (type == null)
                    continue;

                foreach (var go in scene.GetRootGameObjects())
                {
                    foreach (var comp in go.GetComponentsInChildren(type))
                    {
                        if (_storage.objs.Find(x => x is ObjectNode on && on.UnityObject == comp) != null)
                            continue;
                        _storage.objs.Add(new ObjectNode{ UnityObject = comp });
                    }
                }
            }
            
            foreach (var storable in _storage.objs)
            {
                if(storable is ObjectNode on)
                    on.Refresh();
            }
        }
        
        

        protected override void OnGUIDraw()
        {
            _storage ??= FindStorage();

            var e = Event.current;
            {
                if (e.button == 0 && e.isMouse && e.type == EventType.MouseDown && NodeUnderMouse is ObjectNode objNode)
                {
                    if(SelectGO && objNode.UnityObject is Component c)
                        UnityEditor.Selection.objects = new Object[]{ c.gameObject };
                    else
                        UnityEditor.Selection.objects = new []{ objNode.UnityObject };
                    
                    if(PingOnClick)
                        EditorGUIUtility.PingObject(objNode.UnityObject);
                }
            }
            
            base.OnGUIDraw();

            if (DragAndDrop.objectReferences.Length > 0)
            {
                if (e.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                    Event.current.Use();
                }
                else if (e.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var o in DragAndDrop.objectReferences)
                        _storage.objs.Add(new ObjectNode{ UnityObject = o, Pos = ViewportToWorld(Event.current.mousePosition) });
                }
            }

            if (GUIUtility.keyboardControl == 0 && e.keyCode == KeyCode.Delete && NodeUnderMouse is SceneBoardStorage.IStorable storable)
            {
                _storage.objs.Remove(storable);
                this.Repaint();
            }

            if (e.type == EventType.ContextClick)
            {
                var mousePosWorld = ViewportToWorld(e.mousePosition);
                var deleteNodeContent = new GUIContent("Delete Node");
                var addReferencedObjContent = new GUIContent("Bring References to the board", 
                    "Any references to other objects the currently selected object has will be added to the board");
                
                var contextMenu = new GenericMenu();
                contextMenu.AddItem(new GUIContent("New Note"), false, () => _storage.objs.Add(new NoteNode{ Pos = mousePosWorld }));
                contextMenu.AddItem(new GUIContent("New Header"), false, () => _storage.objs.Add(new HeaderNode{ Pos = mousePosWorld }));
                if (this.NodeUnderMouse is {} node)
                    contextMenu.AddItem(deleteNodeContent, false, () => _storage.objs.Remove((SceneBoardStorage.IStorable)node));
                if (this.NodeUnderMouse is ObjectNode objNode)
                {
                    contextMenu.AddItem(addReferencedObjContent, false, () =>
                    {
                        foreach (var reference in ExtractReferences(objNode.UnityObject))
                        {
                            var v = this._storage.objs.Find(x => x is ObjectNode on && ReferenceEquals(on.UnityObject, reference));
                            if (v == null)
                                _storage.objs.Add(new ObjectNode{ UnityObject = reference, Pos = mousePosWorld });
                        }
                    });
                }
                contextMenu.DropDown(new Rect(e.mousePosition, default));
                e.Use();
            }
        }
        
        
        
        protected override void PrepareToolbar(List<(GUIContent, Action)> buttons)
        {
            base.PrepareToolbar(buttons);
            var goContent = new GUIContent(SelectGO ? "Select GameObject Enabled" : "Select GameObject Disabled", "Shows GameObject instead of component in inspector when clicking on nodes");
            var pingContent = new GUIContent(PingOnClick ? "Ping Enabled" : "Ping Disabled", "Ping object location in inspector and project browser when clicking on nodes");
            var defaultTypesContent = new GUIContent("Default Types...");
            var refreshContent = new GUIContent("Refresh", "Rebuilds links, displayed names and icons");
            
            buttons.Add((refreshContent, Refresh));
            buttons.Add((defaultTypesContent, () =>
            {
                var contextMenu = new GenericMenu();

                var types = new HashSet<Type>();
                foreach (var storable in _storage.objs)
                {
                    if (storable is not ObjectNode on || on.UnityObject is not Component)
                        continue;
                    
                    for (var t = on.UnityObject.GetType(); t != null && t != typeof(Component); t = t.BaseType)
                    {
                        types.Add(t);
                    }
                }

                foreach (var type in from t in _storage.AddedByDefault orderby t select t)
                    contextMenu.AddItem(new GUIContent(type), true, () => { _storage.AddedByDefault.Remove(type); });

                foreach (var type in from t in types orderby t.FullName select t)
                {
                    if (_storage.AddedByDefault.Contains(type.FullName))
                        continue;
                    contextMenu.AddItem(new GUIContent(type.FullName), false, () => { _storage.AddedByDefault.Add(type.FullName); });
                }

                contextMenu.DropDown(new Rect(default, default));
            }));
            buttons.Add((goContent, () =>
            {
                SelectGO = !SelectGO;
                goContent.text = SelectGO ? "Select GameObject Enabled" : "Select GameObject Disabled";
            }));
            buttons.Add((pingContent, () =>
            {
                PingOnClick = !PingOnClick;
                pingContent.text = PingOnClick ? "Ping Enabled" : "Ping Disabled";
            }));
        }
        
        

        static HashSet<Object> ExtractReferences(Object obj)
        {
            var output = new HashSet<Object>();
            using var sO = new SerializedObject(obj);
            bool into = true;
            var itt = sO.GetIterator();
            itt.Next(true); // Skip self
            while(itt.Next(into))
            {
                if (itt.type == "PPtr<EditorExtension>")
                {
                    into = false;
                    continue;
                }

                into = true;
                Object objRef;
                switch (itt.propertyType)
                {
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.Boolean:
                    case SerializedPropertyType.Float:
                    case SerializedPropertyType.String:
                    case SerializedPropertyType.Color:
                    case SerializedPropertyType.Enum:
                    case SerializedPropertyType.Vector2:
                    case SerializedPropertyType.Vector3:
                    case SerializedPropertyType.Vector4:
                    case SerializedPropertyType.Rect:
                    case SerializedPropertyType.LayerMask:
                    case SerializedPropertyType.ArraySize:
                    case SerializedPropertyType.Character:
                    case SerializedPropertyType.AnimationCurve:
                    case SerializedPropertyType.Bounds:
                    case SerializedPropertyType.Gradient:
                    case SerializedPropertyType.Quaternion:
                    case SerializedPropertyType.FixedBufferSize:
                    case SerializedPropertyType.Vector2Int:
                    case SerializedPropertyType.Vector3Int:
                    case SerializedPropertyType.RectInt:
                    case SerializedPropertyType.BoundsInt:
                    case SerializedPropertyType.Hash128:
                        into = false;
                        continue;
                    case SerializedPropertyType.ObjectReference:
                        objRef = itt.objectReferenceValue;
                        into = false;
                        break;
                    case SerializedPropertyType.ExposedReference:
                        objRef = itt.exposedReferenceValue;
                        into = false;
                        break;
                    case SerializedPropertyType.ManagedReference:
                        if (itt.managedReferenceValue is Object temp)
                        {
                            objRef = temp;
                            into = false;
                            break;
                        }
                        continue;
                    case SerializedPropertyType.Generic:
                    default:
                        continue;
                }
                if (objRef is Object o && o != null && (o is Component or GameObject || AssetDatabase.Contains(o)))
                    output.Add(o);
            }

            return output;
        }
        
        

        [Serializable] public class ObjectNode : INode, SceneBoardStorage.IStorable
        {
            public Object UnityObject;
            public Vector2 Pos;
            
            HashSet<Object> _references;
            GUIContent _objUnityContent;
            Vector2 IDraggable.Pos
            {
                get => Pos;
                set => Pos = value;
            }

            public float? Width { get; set; } = 100f;

            public void Refresh()
            {
                _objUnityContent = null;
                _references = null;
            }

            public void DrawBackgroundAndTitleBar(ref Rect fieldRect, Rect background, bool isSelected, NodeEditorWindow editor)
            {
                foreach (var o in UnityEditor.Selection.objects)
                {
                    if ((o is GameObject go && UnityObject is Component c && ReferenceEquals(c.gameObject, go))
                        || ReferenceEquals(o, UnityObject))
                    {
                        isSelected = true;
                    }
                }
                INode.DrawBackgroundAndTitleBarDefault(this, ref fieldRect, background, isSelected, editor);
            }

            public void OnDraw(NodeDrawer drawer)
            {
                if (UnityObject == null)
                {
                    if (drawer.IsInView(out var rect, 3f))
                    {
                        var color = GUI.color;
                        GUI.color = Color.red;
                        GUI.Label(rect, "NULL, most likely removed");
                        GUI.color = color;
                    }
                    return;
                }
                
                if (_objUnityContent == null)
                {
                    var tempContent = EditorGUIUtility.ObjectContent(UnityObject, UnityObject.GetType());
                    _objUnityContent = new GUIContent(tempContent.text, tempContent.image, tempContent.tooltip);
                }

                _references ??= ExtractReferences(UnityObject);

                if(drawer.IsInView(out var r, 3f))
                    GUI.Box(r, _objUnityContent.image);

                if (drawer.IsInView(out r, 1f, false))
                {
                    var fntSize = drawer.Editor.GUIStyleCentered.fontSize;
                    drawer.Editor.GUIStyleCentered.fontSize = drawer.Editor.GUIStyleFields.fontSize;
                    GUI.Label(r, _objUnityContent.text, drawer.Editor.GUIStyleCentered);
                    drawer.Editor.GUIStyleCentered.fontSize = fntSize;
                }

                if (Event.current.type == EventType.Layout)
                {
                    // Draw link
                    var color = Color.white;
                    color.a = 0.1f;
                    var thisCenter = drawer.SurfaceCovered.center;
                    var editor = (SceneBoard)drawer.Editor;
                    foreach (var storable in editor._storage.objs)
                    {
                        if (storable is ObjectNode n && _references.Contains(n.UnityObject))
                        {
                            var otherCenter = editor.Drawers[n].SurfaceCovered.center;
                            editor.Lines.Add((thisCenter, otherCenter, color));
                        }
                    }
                }
            }
        }



        [Serializable] public abstract class TextNode : INode, SceneBoardStorage.IStorable
        {
            (float width, float outputSize) _cachedHeight;
            
            
            public string Content = "A Text Node";
            public Vector2 Pos;
            public Color Color = Color.black;
            
            
            
            Vector2 IDraggable.Pos
            {
                get => Pos;
                set => Pos = value;
            }
            public float? Width { get; set; } = null;


            public void DrawBackgroundAndTitleBar(ref Rect fieldRect, Rect background, bool isSelected, NodeEditorWindow editor)
            {
                if (isSelected)
                {
                    var rect = fieldRect;
                    rect.y -= rect.height;
                    Color = EditorGUI.ColorField(rect, Color);
                }
                
                fieldRect.position += fieldRect.height * 0.5f * Vector2.up;
                var color = isSelected ? Color.Lerp(Color.blue, Color, 0.5f) : Color;
                color.a = 0.1f;
                EditorGUI.DrawRect(background, color);
            }

            public void OnDraw(NodeDrawer drawer)
            {
                var rect = drawer.NextRect;
                var offset = rect.width * 0.05f;
                rect.xMin += offset;
                rect.xMax -= offset;
                var style = Style;
                style.fontSize = drawer.GetRelativeFontSize(FontSize);
                if (_cachedHeight.width != rect.width)
                {
                    _tempGUIContent.text = Content;
                    _cachedHeight = (rect.width, style.CalcHeight(_tempGUIContent, rect.width));
                }
                
                rect.height = _cachedHeight.outputSize;
                drawer.MoveNextRectRaw(rect.height);
                if (drawer.IsInView(rect))
                {
                    if (GUI.GetNameOfFocusedControl() == Content)
                    {
                        var color = Color.black;
                        color.a = 0.1f;
                        EditorGUI.DrawRect(rect, color);
                    }
                    GUI.SetNextControlName(Content);
                    Content = GUI.TextArea(rect, Content, style);
                    if (GUI.changed)
                        _cachedHeight = default;
                }
            }

            protected abstract GUIStyle Style { get; }
            protected abstract int FontSize { get; }
        }



        public class NoteNode : TextNode
        {
            static GUIStyle GUIStyle;
            public NoteNode() => Content = "A note";
            protected override GUIStyle Style => GUIStyle ??= new ( EditorStyles.whiteLabel ) { fontSize = 16, wordWrap = true };
            protected override int FontSize => 16;
        }
        
        
        
        public class HeaderNode : TextNode
        {
            static GUIStyle GUIStyle;
            public HeaderNode() => Content = "A Header";
            protected override GUIStyle Style => GUIStyle ??= new ( EditorStyles.whiteLabel ) { fontSize = 16, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
            protected override int FontSize => 20;
        }
    }
}