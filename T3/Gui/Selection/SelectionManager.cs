﻿using System;
using System.Collections.Generic;
using ImGuiNET;
using T3.Core;
using T3.Core.Logging;
using T3.Core.Operator;
using T3.Core.Operator.Interfaces;
using T3.Gui.Graph.Interaction;
using T3.Gui.Windows;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace T3.Gui.Selection
{
    public static class SelectionManager
    {
        public static void Clear()
        {
            Selection.ForEach(RemoveTransformCallback);
            Selection.Clear();
        }

        public static void SetSelectionToParent(Instance instance)
        {
            Clear();
            _parent = instance;
        }

        public static void SetSelection(ISelectableNode node)
        {
            if (node is SymbolChildUi)
            {
                Log.Warning("Setting selection to a SymbolChildUi without providing instance will lead to problems.");
            }

            Clear();
            AddSelection(node);
        }

        public static void SetSelection(SymbolChildUi node, Instance instance)
        {
            Clear();
            AddSelection(node, instance);
        }

        public static void AddSelection(ISelectableNode node)
        {
            _parent = null;
            if (Selection.Contains(node))
                return;

            Selection.Add(node);
        }

        public static void AddSelection(SymbolChildUi node, Instance instance)
        {
            _parent = null;
            if (Selection.Contains(node))
                return;

            Selection.Add(node);
            if (instance != null)
            {
                ChildUiInstanceIdPaths[node] = NodeOperations.BuildIdPathForInstance(instance);
                if (instance is ITransformable transformable)
                {
                    transformable.TransformCallback = TransformCallback;
                    RegisteredTransformCallbacks[node] = transformable;
                }
            }
        }

        private static Vector2 ObjectPosToScreenPos(SharpDX.Vector4 posInObject, SharpDX.Matrix objectToClipSpace)
        {
            SharpDX.Vector4 originInClipSpace = SharpDX.Vector4.Transform(posInObject, objectToClipSpace);
            Vector3 posInNdc = new Vector3(originInClipSpace.X, originInClipSpace.Y, originInClipSpace.Z) / originInClipSpace.W;
            var viewports = ResourceManager.Instance().Device.ImmediateContext.Rasterizer.GetViewports<SharpDX.Mathematics.Interop.RawViewportF>();
            var viewport = viewports[0];
            var originInViewport = new Vector2(viewport.Width * (posInNdc.X * 0.5f + 0.5f),
                                               viewport.Height * (1.0f - (posInNdc.Y * 0.5f + 0.5f)));

            var canvas = ImageOutputCanvas.Current;
            var posInCanvas = canvas.TransformDirection(originInViewport);
            var topLeftOnScreen = ImageOutputCanvas.Current.TransformPosition(System.Numerics.Vector2.Zero);
            var posInScreen = topLeftOnScreen + posInCanvas;

            return posInScreen;
        }

        // todo: move this to the right place when drawing is clear
        private static void TransformCallback(ITransformable transform, EvaluationContext context)
        {
            if (!IsDrawListValid)
            {
                Log.Warning("can't draw gizmo without initialized draw list");
                return;
            }

            // terminology of the matrices:
            // objectToClipSpace means in this context the transform without application of the ITransformable values. These are
            // named 'local'. So localToObject is the matrix of applying the ITransformable values and localToClipSpace to transform
            // points from the local system (including trans/rot of ITransformable) to the projected space. Scale is ignored for
            // local here as the local values are only used for drawing and therefore we don't want to draw anything scaled by this values.
            var objectToClipSpace = context.ObjectToWorld * context.WorldToCamera * context.CameraToClipSpace;

            var s = transform.Scale;
            var r = transform.Rotation;
            float yaw = SharpDX.MathUtil.DegreesToRadians(r.Y);
            float pitch = SharpDX.MathUtil.DegreesToRadians(r.X);
            float roll = SharpDX.MathUtil.DegreesToRadians(r.Z);
            var t = transform.Translation;
            var localToObject = SharpDX.Matrix.Transformation(SharpDX.Vector3.Zero, SharpDX.Quaternion.Identity, SharpDX.Vector3.One,
                                                              SharpDX.Vector3.Zero, SharpDX.Quaternion.RotationYawPitchRoll(yaw, pitch, roll),
                                                              new SharpDX.Vector3(t.X, t.Y, t.Z));
            var localToClipSpace = localToObject * objectToClipSpace;

            SharpDX.Vector4 originInClipSpace = SharpDX.Vector4.Transform(new SharpDX.Vector4(t.X, t.Y, t.Z, 1), objectToClipSpace);
            Vector3 originInNdc = new Vector3(originInClipSpace.X, originInClipSpace.Y, originInClipSpace.Z) / originInClipSpace.W;
            var viewports = ResourceManager.Instance().Device.ImmediateContext.Rasterizer.GetViewports<SharpDX.Mathematics.Interop.RawViewportF>();
            var viewport = viewports[0];
            var originInViewport = new Vector2(viewport.Width * (originInNdc.X * 0.5f + 0.5f),
                                               viewport.Height * (1.0f - (originInNdc.Y * 0.5f + 0.5f)));

            var canvas = ImageOutputCanvas.Current;
            var originInCanvas = canvas.TransformDirection(originInViewport);
            var topLeftOnScreen = ImageOutputCanvas.Current.TransformPosition(System.Numerics.Vector2.Zero);
            var originInScreen = topLeftOnScreen + originInCanvas;

            // ImGui.GetWindowDrawList().AddCircleFilled(textPos, 6.0f, 0xFFFFFFFF);
            // need foreground draw list atm as texture is drawn afterwards to output view

            var scale = CalcGizmoScale(context, localToObject, viewport.Width, viewport.Height, 45f, SettingsWindow.GizmoSize);
            var centerPadding = 0.2f * scale / canvas.Scale.X;
            var length = 1f * scale / canvas.Scale.Y;
            var lineThickness = 2;

            // draw the gizmo axis
            Vector2 xAxisStartInScreen = ObjectPosToScreenPos(new SharpDX.Vector4(centerPadding, 0.0f, 0.0f, 1.0f), localToClipSpace);
            Vector2 xAxisEndInScreen = ObjectPosToScreenPos(new SharpDX.Vector4(length, 0.0f, 0.0f, 1.0f), localToClipSpace);
            _drawList.AddLine(xAxisStartInScreen, xAxisEndInScreen, 0x7F0000FF, lineThickness);

            Vector2 yAxisStartInScreen = ObjectPosToScreenPos(new SharpDX.Vector4(0.0f, centerPadding, 0.0f, 1.0f), localToClipSpace);
            Vector2 yAxisEndInScreen = ObjectPosToScreenPos(new SharpDX.Vector4(0.0f, length, 0.0f, 1.0f), localToClipSpace);
            _drawList.AddLine(yAxisStartInScreen, yAxisEndInScreen, 0x7F00FF00, lineThickness);

            Vector2 zAxisStartInScreen = ObjectPosToScreenPos(new SharpDX.Vector4(0.0f, 0.0f, centerPadding, 1.0f), localToClipSpace);
            Vector2 zAxisEndInScreen = ObjectPosToScreenPos(new SharpDX.Vector4(0.0f, 0.0f, length, 1.0f), localToClipSpace);
            _drawList.AddLine(zAxisStartInScreen, zAxisEndInScreen, 0x7FFF0000, lineThickness);

            // example interaction for moving origin within plane parallel to cam
            var mousePosInScreen = ImGui.GetIO().MousePos;
            var screenSquaredMin = originInScreen - new Vector2(10.0f, 10.0f);
            var screenSquaredMax = originInScreen + new Vector2(10.0f, 10.0f);

            if (mousePosInScreen.X > screenSquaredMin.X && mousePosInScreen.X < screenSquaredMax.X &&
                mousePosInScreen.Y > screenSquaredMin.Y && mousePosInScreen.Y < screenSquaredMax.Y &&
                ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                _isGizmoDragging = true;
                _offsetToOriginAtDragStart = mousePosInScreen - originInScreen;
            }

            if (_isGizmoDragging)
            {
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    _isGizmoDragging = false;
                }
                else
                {
                    Vector2 newOriginInScreen = mousePosInScreen - _offsetToOriginAtDragStart;
                    // transform back to object space
                    var clipSpaceToObject = objectToClipSpace;
                    clipSpaceToObject.Invert();
                    var newOriginInCanvas = newOriginInScreen - topLeftOnScreen;
                    var newOriginInViewport = canvas.InverseTransformDirection(newOriginInCanvas);
                    var newOriginInClipSpace = new SharpDX.Vector4(2.0f * newOriginInViewport.X / viewport.Width - 1.0f,
                                                                   -(2.0f * newOriginInViewport.Y / viewport.Height - 1.0f),
                                                                   originInNdc.Z, 1);
                    var newOriginInObject = SharpDX.Vector4.Transform(newOriginInClipSpace, clipSpaceToObject);
                    Vector3 newTranslation = new Vector3(newOriginInObject.X, newOriginInObject.Y, newOriginInObject.Z) / newOriginInObject.W;
                    transform.Translation = newTranslation;
                }
            }
        }

        // Calculates the scale for a gizmo based on the distance to the cam
        private static float CalcGizmoScale(EvaluationContext context, SharpDX.Matrix localToObject, float width, float height, float fovInDegree,
                                            float gizmoSize)
        {
            var localToCamera = localToObject * context.ObjectToWorld * context.WorldToCamera;
            var distance = localToCamera.TranslationVector.Length(); // distance of local origin to cam
            var denom = Math.Sqrt(width * width + height * height) * Math.Tan(SharpDX.MathUtil.DegreesToRadians(fovInDegree));
            return (float)Math.Max(0.0001, (distance / denom) * gizmoSize);
        }

        public static void SetDrawList(ImDrawListPtr drawList)
        {
            _drawList = drawList;
            IsDrawListValid = true;
        }

        public static void StopDrawList()
        {
            IsDrawListValid = false;
        }

        private static ImDrawListPtr _drawList = null;
        private static bool IsDrawListValid;

        public static void RemoveSelection(ISelectableNode node)
        {
            Selection.Remove(node);
            RemoveTransformCallback(node);
        }

        private static void RemoveTransformCallback(ISelectableNode node)
        {
            if (RegisteredTransformCallbacks.TryGetValue(node, out var transformable))
            {
                transformable.TransformCallback = null;
            }
        }

        public static IEnumerable<T> GetSelectedNodes<T>() where T : ISelectableNode
        {
            foreach (var item in Selection)
            {
                if (item is T typedItem)
                    yield return typedItem;
            }
        }

        public static bool IsNodeSelected(ISelectableNode node)
        {
            return Selection.Contains(node);
        }

        public static bool IsAnythingSelected()
        {
            return Selection.Count > 0;
        }

        /// <summary>
        /// This is called at the beginning of each frame.
        /// 
        /// For some events we have to use a frame delay machanism to ui-elements can
        /// respond to updates in a controlled manner (I.e. when rendering the next frame) 
        /// </summary>
        public static void ProcessNewFrame()
        {
            //NodesSelectedLastFrame.Clear();
            // foreach (var n in Selection)
            // {
            //     if (!_lastFrameSelection.Contains(n))
            //         NodesSelectedLastFrame.Add(n);
            // }

            //_lastFrameSelection = Selection;
            FitViewToSelectionRequested = _fitViewToSelectionTriggered;
            _fitViewToSelectionTriggered = false;
        }

        public static Instance GetSelectedInstance()
        {
            if (Selection.Count == 0)
                return _parent;

            if (Selection[0] is SymbolChildUi firstNode)
            {
                if (!ChildUiInstanceIdPaths.ContainsKey(firstNode))
                {
                    Log.Error("Failed to access id-path of selected childUi " + firstNode.SymbolChild.Name);
                    Clear();
                    return null;
                }

                var idPath = ChildUiInstanceIdPaths[firstNode];
                return NodeOperations.GetInstanceFromIdPath(idPath);
            }

            return null;
        }

        public static Instance GetCompositionForSelection()
        {
            if (Selection.Count == 0)
                return _parent;

            if (!(Selection[0] is SymbolChildUi firstNode))
                return null;

            var idPath = ChildUiInstanceIdPaths[firstNode];
            var instanceFromIdPath = NodeOperations.GetInstanceFromIdPath(idPath);
            return instanceFromIdPath?.Parent;
        }

        public static IEnumerable<SymbolChildUi> GetSelectedSymbolChildUis()
        {
            var result = new List<SymbolChildUi>();
            foreach (var s in Selection)
            {
                if (!(s is SymbolChildUi symbolChildUi))
                    continue;

                result.Add(symbolChildUi);
            }

            return result;
        }

        public static Instance GetInstanceForSymbolChildUi(SymbolChildUi symbolChildUi)
        {
            var idPath = ChildUiInstanceIdPaths[symbolChildUi];
            return (NodeOperations.GetInstanceFromIdPath(idPath));
        }

        public static void FitViewToSelection()
        {
            _fitViewToSelectionTriggered = true;
        }

        public static bool FitViewToSelectionRequested { get; private set; }
        private static bool _fitViewToSelectionTriggered = false;
        private static Instance _parent;

        private static readonly List<ISelectableNode> Selection = new List<ISelectableNode>();

        //private static readonly List<ISelectableNode> NodesSelectedLastFrame = new List<ISelectableNode>();
        //private static List<ISelectableNode> _lastFrameSelection = new List<ISelectableNode>();
        private static readonly Dictionary<SymbolChildUi, List<Guid>> ChildUiInstanceIdPaths = new Dictionary<SymbolChildUi, List<Guid>>();
        private static readonly Dictionary<ISelectableNode, ITransformable> RegisteredTransformCallbacks = new Dictionary<ISelectableNode, ITransformable>(10);
        private static Vector2 _offsetToOriginAtDragStart;
        public static bool _isGizmoDragging;
    }
}