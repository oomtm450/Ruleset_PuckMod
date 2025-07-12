using oomtm450PuckMod_Ruleset.Configs;
using System;
using System.Text;
using Unity.Collections;
using Unity.Netcode;

namespace oomtm450PuckMod_Ruleset {
    internal static class NetworkCommunication {
        /// <summary>
        /// Method that sends data to the listener.
        /// </summary>
        /// <param name="dataName">String, header of the data.</param>
        /// <param name="dataStr">String, content of the data.</param>
        /// <param name="clientId">Ulong, Id of the client that is sending the data.</param>
        /// <param name="listener">String, listener where to send the data.</param>
        /// <param name="config">IConfig, config for the logs.</param>
        public static void SendData(string dataName, string dataStr, ulong clientId, string listener, IConfig config = null) {
            try {
                byte[] data = Encoding.UTF8.GetBytes(dataStr);

                int size = Encoding.UTF8.GetByteCount(dataName) + sizeof(ulong) + data.Length;

                FastBufferWriter writer = new FastBufferWriter(size, Allocator.TempJob);
                writer.WriteValue(dataName);
                writer.WriteBytes(data);

                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(listener, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);

                writer.Dispose();

                if (config != null)
                    Logging.Log($"Sent data \"{dataName}\" ({data.Length} bytes - {size} total bytes) to {clientId}.", config);
            }
            catch (Exception ex) {
                Logging.LogError($"Error when writing streamed data: {ex}");
            }
        }

        /// <summary>
        /// Method that sends data to the listener.
        /// </summary>
        /// <param name="dataName">String, header of the data.</param>
        /// <param name="dataStr">String, content of the data.</param>
        /// <param name="listener">String, listener where to send the data.</param>
        /// <param name="config">IConfig, config for the logs.</param>
        public static void SendDataToAll(string dataName, string dataStr, string listener, IConfig config = null) {
            try {
                byte[] data = Encoding.UTF8.GetBytes(dataStr);

                int size = Encoding.UTF8.GetByteCount(dataName) + sizeof(ulong) + data.Length;

                FastBufferWriter writer = new FastBufferWriter(size, Allocator.TempJob);
                writer.WriteValue(dataName);
                writer.WriteBytes(data);

                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(listener, writer, NetworkDelivery.ReliableFragmentedSequenced);

                writer.Dispose();

                if (config != null)
                    Logging.Log($"Sent data \"{dataName}\" ({data.Length} bytes - {size} total bytes) to all clients.", config);
            }
            catch (Exception ex) {
                Logging.LogError($"Error when writing streamed data: {ex}");
            }
        }

        /// <summary>
        /// Function that reads data from the reader and returns it.
        /// </summary>
        /// <param name="clientId">Ulong, Id of the client that sent the data.</param>
        /// <param name="reader">FastBufferReader, reader containing the data.</param>
        /// <param name="config">IConfig, config for the logs.</param>
        /// <returns>(string DataName, string DataStr), header of the data and the content of the data.</returns>
        public static (string DataName, string DataStr) GetData(ulong clientId, FastBufferReader reader, IConfig config = null) {
            try {
                reader.ReadValue(out string dataName);

                int length = reader.Length - reader.Position;
                int totalLength = length + sizeof(ulong) + Encoding.UTF8.GetByteCount(dataName);
                byte[] data = new byte[length];
                for (int i = 0; i < length; i++)
                    reader.ReadByte(out data[i]);

                string dataStr = Encoding.UTF8.GetString(data).Trim();

                if (config != null)
                    Logging.Log($"Received data {dataName.Trim()} ({length} bytes - {totalLength} total bytes) from {clientId}. Content : {dataStr}", config);

                return (dataName.Trim(), dataStr);
            }
            catch (Exception ex)  {
                Logging.LogError($"Error when reading streamed data: {ex}");
            }

            return ("", "");
        }
    }
}
