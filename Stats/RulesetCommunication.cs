using Codebase;
using System;
using System.IO.Pipes;
using System.Threading;
using UnityEngine;
using static Codebase.SystemFunc;

namespace oomtm450PuckMod_Stats {
    internal class RulesetCommunication : MonoBehaviour {
        private static NamedPipeServerStream _stream;
        private static StreamString _pipeServer = null;

        internal void Start() {
            try {
                Logging.Log("Opening NamedPipeServerStream for inner server mods communication.", Stats.ServerConfig, true);

                _stream = new NamedPipeServerStream(Codebase.Constants.STATS_MOD_NAMED_PIPE_SERVER, PipeDirection.InOut, 1);
                _stream.WaitForConnectionAsync().ContinueWith(_ => ProcessInnerModCommunication());
            }
            catch (Exception ex) {
                Logging.LogError(ex.ToString(), Stats.ServerConfig);
            }
        }

        private static void ProcessInnerModCommunication() {
            Logging.Log($"Client connected to NamedPipeServerStream.", Stats.ServerConfig, true);
            try {
                _pipeServer = new StreamString(_stream);
                while (_pipeServer.IsConnected) {
                    Thread.Sleep(200);
                    string str = _pipeServer.ReadString();
                    if (string.IsNullOrEmpty(str))
                        continue;

                    try {
                        string[] splittedStr = str.Split(';');

                        if (!NetworkCommunication.GetDataNamesToIgnore().Contains(splittedStr[0]))
                            Logging.Log($"Received data {splittedStr[0]} from {Codebase.Constants.STATS_MOD_NAMED_PIPE_SERVER}. Content : {splittedStr[1]}", Stats.ServerConfig);

                        if (splittedStr[0] == Codebase.Constants.SOG) {
                            Stats.SendSavePercDuringGoalNextFrame_Player = PlayerManager.Instance.GetPlayerBySteamId(splittedStr[1]);
                            if (Stats.SendSavePercDuringGoalNextFrame_Player == null || !Stats.SendSavePercDuringGoalNextFrame_Player)
                                Logging.LogError($"{nameof(Stats.SendSavePercDuringGoalNextFrame_Player)} is null.", Stats.ServerConfig);
                            else
                                Stats.SendSavePercDuringGoalNextFrame = true;
                        }
                        else if (splittedStr[0] == Codebase.Constants.PAUSED)
                            Stats.Paused = bool.Parse(splittedStr[1]);
                    }
                    catch (Exception ex) {
                        Logging.LogError(ex.ToString(), Stats.ServerConfig);
                    }
                }
            }
            catch (Exception ex) {
                Logging.LogError(ex.ToString(), Stats.ServerConfig);
            }
        }

        internal void Close() {
            _pipeServer.Close();
        }
    }
}
