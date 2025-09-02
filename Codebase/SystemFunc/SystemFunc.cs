using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Codebase {
    public static class SystemFunc {
        public static T GetPrivateField<T>(Type typeContainingField, object instanceOfType, string fieldName) {
            return (T)typeContainingField.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instanceOfType);
        }

        /// <summary>
        /// Function that returns a Stick instance from a GameObject.
        /// </summary>
        /// <param name="gameObject">GameObject, GameObject to use.</param>
        /// <returns>Stick, found Stick object or null.</returns>
        public static Stick GetStick(GameObject gameObject) {
            return gameObject.GetComponent<Stick>();
        }

        /// <summary>
        /// Function that returns a PlayerBodyV2 instance from a GameObject.
        /// </summary>
        /// <param name="gameObject">GameObject, GameObject to use.</param>
        /// <returns>PlayerBodyV2, found PlayerBodyV2 object or null.</returns>
        public static PlayerBodyV2 GetPlayerBodyV2(GameObject gameObject) {
            return gameObject.GetComponent<PlayerBodyV2>();
        }

        public static string RemoveWhitespace(string input) {
            return new string(input
                .Where(c => !Char.IsWhiteSpace(c))
                .ToArray());
        }
    }
}
