using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace oomtm450PuckMod_Ruleset.RefUI {
    internal class RefUIManager : MonoBehaviour {
        private MouseComponent _mouseComponent;
        private bool _mouseRegistered = false;

        private TeamPlayerWindow _redWindow;
        private TeamPlayerWindow _blueWindow;
        private InfractionsWindow _infractionsWindow;

        private static GameObject _instance;

        internal static void Init() {
            if (_instance != null)
                return;

            _instance = new GameObject("Ruleset_RefUI");
            _instance.AddComponent<RefUIManager>();
            DontDestroyOnLoad(_instance);
        }

        internal static void Shutdown() {
            if (_instance != null) {
                Destroy(_instance);
                _instance = null;
            }
        }

        private void Start() {
            float margin = 20f;

            _redWindow = new TeamPlayerWindow("Red", PlayerTeam.Red, 98234,
                new Rect(Screen.width - 520 - margin, Screen.height - 420, 520, 400));

            _blueWindow = new TeamPlayerWindow("Blue", PlayerTeam.Blue, 98235,
                new Rect(margin, Screen.height - 420, 520, 400));

            float infWidth = 700f;
            _infractionsWindow = new InfractionsWindow(98236,
                new Rect((Screen.width - infWidth) / 2f, Screen.height - 100, infWidth, 70));

            _mouseComponent = new MouseComponent {
                VisibilityRequiresMouse = true
            };
        }

        private bool ShouldShowUI() {
            if (!Ruleset.IsRefmodeActive || Ruleset.RefSteamIds.Count == 0)
                return false;

            try {
                Player localPlayer = PlayerManager.Instance?.GetLocalPlayer();
                if (localPlayer == null || !localPlayer)
                    return false;

                if (localPlayer.Team.Value != PlayerTeam.Spectator)
                    return false;

                string localSteamId = localPlayer.SteamId.Value.ToString();
                return Ruleset.RefSteamIds.Contains(localSteamId);
            }
            catch {
                return false;
            }
        }

        private void Update() {
            bool visible = ShouldShowUI();

            if (!visible) {
                if (_mouseComponent != null && _mouseComponent.IsVisible)
                    _mouseComponent.Hide();
                return;
            }

            if (!_mouseRegistered)
                RegisterMouseComponent();

            bool shouldAcquire = Mouse.current != null && Mouse.current.rightButton.isPressed;

            if (shouldAcquire)
                _mouseComponent.Show();
            else
                _mouseComponent.Hide();
        }

        private void RegisterMouseComponent() {
            try {
                var uiManager = NetworkBehaviourSingleton<UIManager>.Instance;
                if (uiManager == null)
                    return;

                var componentsField = typeof(UIManager).GetField("components",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (componentsField == null)
                    return;

                var components = (List<UIComponent>)componentsField.GetValue(uiManager);
                components.Add(_mouseComponent);

                var onVisibility = typeof(UIManager).GetMethod("OnMouseRequiredComponentChangedVisibility",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                var onFocus = typeof(UIManager).GetMethod("OnMouseRequiredComponentChangedFocus",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (onVisibility != null)
                    _mouseComponent.OnVisibilityChanged += (EventHandler)Delegate.CreateDelegate(
                        typeof(EventHandler), uiManager, onVisibility);

                if (onFocus != null)
                    _mouseComponent.OnFocusChanged += (EventHandler)Delegate.CreateDelegate(
                        typeof(EventHandler), uiManager, onFocus);

                _mouseRegistered = true;
            }
            catch (Exception ex) {
                Debug.LogWarning($"[Ruleset RefUI] Failed to register mouse component: {ex.Message}");
            }
        }

        private void OnGUI() {
            if (!ShouldShowUI())
                return;

            RefUIStyles.EnsureInitialized();

            _redWindow.Draw();
            _blueWindow.Draw();
            _infractionsWindow.Draw();
        }

        internal void Cleanup() {
            try {
                if (_mouseComponent != null && _mouseRegistered) {
                    var uiManager = NetworkBehaviourSingleton<UIManager>.Instance;
                    if (uiManager != null) {
                        var componentsField = typeof(UIManager).GetField("components",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (componentsField != null) {
                            var components = (List<UIComponent>)componentsField.GetValue(uiManager);
                            components.Remove(_mouseComponent);
                        }
                    }
                }
            }
            catch {
                // Cleanup is best-effort
            }

            _mouseRegistered = false;
        }

        private void OnDestroy() {
            Cleanup();
        }
    }
}
