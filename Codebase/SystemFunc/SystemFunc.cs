using Codebase.Configs;
using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
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

        public class StreamString {
            private readonly Stream _ioStream;
            private readonly UnicodeEncoding _streamEncoding;

            public bool IsConnected {
                get {
                    if (_ioStream is NamedPipeClientStream clientIOStream)
                        return clientIOStream.IsConnected;
                    if (_ioStream is NamedPipeServerStream serverIOStream)
                        return serverIOStream.IsConnected;

                    return false;
                }
            }

            public StreamString(Stream ioStream) {
                _ioStream = ioStream;
                _streamEncoding = new UnicodeEncoding();
            }

            public string ReadString() {
                int len = _ioStream.ReadByte() * 256;
                len += _ioStream.ReadByte();
                byte[] inBuffer = new byte[len];
                _ioStream.Read(inBuffer, 0, len);

                return _streamEncoding.GetString(inBuffer);
            }

            public int WriteString(string outString) {
                byte[] outBuffer = _streamEncoding.GetBytes(outString);
                int len = outBuffer.Length;
                if (len > UInt16.MaxValue)
                    len = (int)UInt16.MaxValue;

                _ioStream.WriteByte((byte)(len / 256));
                _ioStream.WriteByte((byte)(len & 255));
                _ioStream.Write(outBuffer, 0, len);
                _ioStream.Flush();

                return outBuffer.Length + 2;
            }

            public void Close() {
                _ioStream.Close();
            }
        }
    }
}
