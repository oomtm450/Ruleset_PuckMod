using UnityEngine;

namespace oomtm450PuckMod_Ruleset.RefUI {
    internal abstract class ResizableWindow {
        private Rect _windowRect;
        private readonly int _windowId;
        private readonly float _minWidth;
        private readonly float _minHeight;
        private readonly float _resizeHandleSize = 20f;

        private bool _isResizing = false;
        private Vector2 _resizeDragStart;
        private Vector2 _resizeOriginalSize;

        protected Vector2 ScrollPos = Vector2.zero;
        protected abstract string Title { get; }

        protected ResizableWindow(int windowId, Rect defaultRect, float minWidth = 400f, float minHeight = 200f) {
            _windowId = windowId;
            _windowRect = defaultRect;
            _minWidth = minWidth;
            _minHeight = minHeight;
        }

        internal void Draw() {
            _windowRect = GUI.Window(_windowId, _windowRect, DrawWindowInternal, Title, RefUIStyles.WindowStyle);
        }

        private void DrawWindowInternal(int id) {
            ScrollPos = GUILayout.BeginScrollView(ScrollPos);
            DrawContent();
            GUILayout.EndScrollView();

            Rect handleRect = new Rect(
                _windowRect.width - _resizeHandleSize,
                _windowRect.height - _resizeHandleSize,
                _resizeHandleSize,
                _resizeHandleSize
            );

            GUI.Label(handleRect, "◢");
            HandleResize(handleRect);

            if (!_isResizing)
                GUI.DragWindow(new Rect(0, 0, _windowRect.width, 20));
        }

        protected abstract void DrawContent();

        private void HandleResize(Rect handleRect) {
            Event current = Event.current;

            if (current.type == EventType.MouseDown && handleRect.Contains(current.mousePosition)) {
                _isResizing = true;
                _resizeDragStart = GUIUtility.GUIToScreenPoint(current.mousePosition);
                _resizeOriginalSize = new Vector2(_windowRect.width, _windowRect.height);
                current.Use();
                return;
            }

            if (!_isResizing)
                return;

            if (current.type == EventType.MouseDrag) {
                Vector2 screenPos = GUIUtility.GUIToScreenPoint(current.mousePosition);
                Vector2 delta = screenPos - _resizeDragStart;
                _windowRect.width = Mathf.Max(_minWidth, _resizeOriginalSize.x + delta.x);
                _windowRect.height = Mathf.Max(_minHeight, _resizeOriginalSize.y + delta.y);
                current.Use();
                return;
            }

            if (current.type == EventType.MouseUp) {
                _isResizing = false;
                current.Use();
            }
        }
    }
}
