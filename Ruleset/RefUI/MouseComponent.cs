using System;

namespace oomtm450PuckMod_Ruleset.RefUI {
    internal class MouseComponent : UIComponent {
        public bool IsVisible { get; private set; }
        public bool IsFocused { get; set; }
        public bool VisibilityRequiresMouse { get; set; }
        public bool FocusRequiresMouse { get; set; }
        public bool AlwaysVisible { get; set; }

        public event EventHandler OnVisibilityChanged;
#pragma warning disable CS0067
        public event EventHandler OnFocusChanged;
#pragma warning restore CS0067

        public void Show() {
            if (IsVisible)
                return;

            IsVisible = true;
            OnVisibilityChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Hide() {
            if (!IsVisible)
                return;

            IsVisible = false;
            OnVisibilityChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Hide(bool unfocus) {
            Hide();
            if (unfocus)
                IsFocused = false;
        }

        public void Toggle() {
            if (IsVisible)
                Hide();
            else
                Show();
        }
    }
}
