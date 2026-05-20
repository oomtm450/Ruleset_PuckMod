using UnityEngine;

namespace oomtm450PuckMod_Ruleset.RefUI {
    internal static class RefUIStyles {
        private static bool _initialized = false;

        private static Texture2D _windowBackground;
        private static Texture2D _redRowBackground;
        private static Texture2D _blueRowBackground;
        private static Texture2D _redHeaderBackground;
        private static Texture2D _blueHeaderBackground;
        private static Texture2D _redButtonBackground;
        private static Texture2D _blueButtonBackground;
        private static Texture2D _redButtonHoverBackground;
        private static Texture2D _blueButtonHoverBackground;

        private static GUIStyle _windowStyle;
        private static GUIStyle _redRowStyle;
        private static GUIStyle _blueRowStyle;
        private static GUIStyle _redHeaderStyle;
        private static GUIStyle _blueHeaderStyle;
        private static GUIStyle _playerLabelStyle;
        private static GUIStyle _redButtonStyle;
        private static GUIStyle _blueButtonStyle;

        internal static GUIStyle WindowStyle => _windowStyle;
        internal static GUIStyle PlayerLabelStyle => _playerLabelStyle;
        internal static GUIStyle RedButtonStyle => _redButtonStyle;
        internal static GUIStyle BlueButtonStyle => _blueButtonStyle;

        internal static GUIStyle GetHeaderStyle(string teamName) {
            return teamName == "Red" ? _redHeaderStyle : _blueHeaderStyle;
        }

        internal static GUIStyle GetRowStyle(string teamName) {
            return teamName == "Red" ? _redRowStyle : _blueRowStyle;
        }

        internal static void EnsureInitialized() {
            if (_initialized)
                return;

            _windowBackground = MakeSolidTexture(new Color(0.15f, 0.15f, 0.15f, 0.95f));
            _redRowBackground = MakeSolidTexture(new Color(0.45f, 0.12f, 0.12f, 0.5f));
            _blueRowBackground = MakeSolidTexture(new Color(0.12f, 0.18f, 0.45f, 0.5f));
            _redHeaderBackground = MakeSolidTexture(new Color(0.6f, 0.15f, 0.15f, 0.8f));
            _blueHeaderBackground = MakeSolidTexture(new Color(0.15f, 0.22f, 0.6f, 0.8f));
            _redButtonBackground = MakeSolidTexture(new Color(0.55f, 0.12f, 0.12f, 0.9f));
            _blueButtonBackground = MakeSolidTexture(new Color(0.12f, 0.2f, 0.55f, 0.9f));
            _redButtonHoverBackground = MakeSolidTexture(new Color(0.7f, 0.2f, 0.2f, 0.95f));
            _blueButtonHoverBackground = MakeSolidTexture(new Color(0.2f, 0.3f, 0.7f, 0.95f));

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _windowBackground;
            _windowStyle.onNormal.background = _windowBackground;

            _redRowStyle = new GUIStyle(GUI.skin.box);
            _redRowStyle.normal.background = _redRowBackground;
            _redRowStyle.margin = new RectOffset(0, 0, 1, 1);
            _redRowStyle.padding = new RectOffset(4, 4, 2, 2);

            _blueRowStyle = new GUIStyle(GUI.skin.box);
            _blueRowStyle.normal.background = _blueRowBackground;
            _blueRowStyle.margin = new RectOffset(0, 0, 1, 1);
            _blueRowStyle.padding = new RectOffset(4, 4, 2, 2);

            _redHeaderStyle = new GUIStyle(GUI.skin.box);
            _redHeaderStyle.normal.background = _redHeaderBackground;
            _redHeaderStyle.normal.textColor = Color.white;
            _redHeaderStyle.fontStyle = FontStyle.Bold;
            _redHeaderStyle.alignment = TextAnchor.MiddleCenter;
            _redHeaderStyle.fontSize = 14;

            _blueHeaderStyle = new GUIStyle(GUI.skin.box);
            _blueHeaderStyle.normal.background = _blueHeaderBackground;
            _blueHeaderStyle.normal.textColor = Color.white;
            _blueHeaderStyle.fontStyle = FontStyle.Bold;
            _blueHeaderStyle.alignment = TextAnchor.MiddleCenter;
            _blueHeaderStyle.fontSize = 14;

            _playerLabelStyle = new GUIStyle(GUI.skin.label);
            _playerLabelStyle.alignment = TextAnchor.MiddleLeft;
            _playerLabelStyle.normal.textColor = Color.white;
            _playerLabelStyle.fontStyle = FontStyle.Bold;
            _playerLabelStyle.fontSize = 16;
            _playerLabelStyle.clipping = TextClipping.Clip;

            _redButtonStyle = new GUIStyle(GUI.skin.button);
            _redButtonStyle.normal.background = _redButtonBackground;
            _redButtonStyle.normal.textColor = Color.white;
            _redButtonStyle.hover.background = _redButtonHoverBackground;
            _redButtonStyle.hover.textColor = Color.white;
            _redButtonStyle.active.background = _redButtonHoverBackground;
            _redButtonStyle.active.textColor = Color.white;
            _redButtonStyle.fontStyle = FontStyle.Bold;
            _redButtonStyle.fontSize = 13;

            _blueButtonStyle = new GUIStyle(GUI.skin.button);
            _blueButtonStyle.normal.background = _blueButtonBackground;
            _blueButtonStyle.normal.textColor = Color.white;
            _blueButtonStyle.hover.background = _blueButtonHoverBackground;
            _blueButtonStyle.hover.textColor = Color.white;
            _blueButtonStyle.active.background = _blueButtonHoverBackground;
            _blueButtonStyle.active.textColor = Color.white;
            _blueButtonStyle.fontStyle = FontStyle.Bold;
            _blueButtonStyle.fontSize = 13;

            _initialized = true;
        }

        private static Texture2D MakeSolidTexture(Color color) {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }
    }
}
