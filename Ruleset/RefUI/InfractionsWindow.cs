using System.Collections.Generic;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset.RefUI {
    internal class InfractionsWindow {
        private Rect _windowRect;
        private readonly int _windowId;

        private static readonly string[] Infractions = { "Offside", "High Stick", "Icing", "GINT" };

        private static readonly Dictionary<string, string> CommandMap = new Dictionary<string, string> {
            { "Offside_Red",     "/offred" },
            { "Offside_Blue",    "/offblue" },
            { "High Stick_Red",  "/hsred" },
            { "High Stick_Blue", "/hsblue" },
            { "Icing_Red",       "/icred" },
            { "Icing_Blue",      "/icblue" },
            { "GINT_Red",        "/gintred" },
            { "GINT_Blue",       "/gintblue" },
        };

        internal InfractionsWindow(int windowId, Rect defaultRect) {
            _windowId = windowId;
            _windowRect = defaultRect;
        }

        internal void Draw() {
            _windowRect = GUI.Window(_windowId, _windowRect, DrawWindowInternal, "Team Calls", RefUIStyles.WindowStyle);
        }

        private void DrawWindowInternal(int id) {
            GUILayout.BeginHorizontal();

            foreach (string infraction in Infractions) {
                if (GUILayout.Button(infraction, RefUIStyles.BlueButtonStyle, GUILayout.Height(30))) {
                    string key = $"{infraction}_Blue";
                    if (CommandMap.TryGetValue(key, out string command))
                        ChatService.Send(command);
                }
            }

            GUILayout.Space(16);

            foreach (string infraction in Infractions) {
                if (GUILayout.Button(infraction, RefUIStyles.RedButtonStyle, GUILayout.Height(30))) {
                    string key = $"{infraction}_Red";
                    if (CommandMap.TryGetValue(key, out string command))
                        ChatService.Send(command);
                }
            }

            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 20));
        }
    }
}
