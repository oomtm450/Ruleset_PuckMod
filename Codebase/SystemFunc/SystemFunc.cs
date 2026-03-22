using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Codebase {
    public static class SystemFunc {
        public static T GetPrivateField<T>(Type typeContainingField, object instanceOfType, string fieldName) {
            if (instanceOfType == null)
                return (T)typeContainingField.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static).GetValue(instanceOfType);
            else
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
        /// Function that returns a PlayerBody instance from a GameObject.
        /// </summary>
        /// <param name="gameObject">GameObject, GameObject to use.</param>
        /// <returns>PlayerBody, found PlayerBody object or null.</returns>
        public static PlayerBody GetPlayerBody(GameObject gameObject) {
            return gameObject.GetComponent<PlayerBody>();
        }

        public static string RemoveWhitespace(string input) {
            return new string(input
                .Where(c => !Char.IsWhiteSpace(c))
                .ToArray());
        }
    }
}
