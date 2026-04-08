using UnityEngine;

namespace oomtm450PuckMod_Ruleset.RefUI {
    internal static class ChatService {
        internal static void Send(string message) {
            try {
                UIChat chat = Object.FindFirstObjectByType<UIChat>();
                if (chat == null) {
                    Debug.LogWarning("[Ruleset RefUI] UIChat not found.");
                    return;
                }

                chat.Client_SendClientChatMessage(message, false);
            }
            catch (System.Exception ex) {
                Debug.LogWarning($"[Ruleset RefUI] Failed to send chat: {ex.Message}");
            }
        }
    }
}
