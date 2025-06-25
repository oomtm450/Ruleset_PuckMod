using oomtm450PuckMod_Ruleset.SystemFunc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace oomtm450PuckMod_Ruleset {
    internal class Sounds : MonoBehaviour {
        private const string SOUND_FOLDER_PATH = "sounds";
        private const string SOUND_EXTENSION = ".ogg";

        internal const string LOAD_SOUNDS = "loadsounds";
        internal const string PLAY_SOUND = "playsound";
        internal const string STOP_SOUND = "stopsound";

        internal const string WHISTLE = "whistle";
        internal const string FACEOFF_MUSIC = "faceoffmusic";
        internal const string FACEOFF_MUSIC_DELAYED = "faceoffmusicdelayed";

        internal static List<string> faceoffMusicList = new List<string>();

        private readonly Dictionary<string, GameObject> _soundObjects = new Dictionary<string, GameObject>();
        private readonly List<AudioClip> _audioClips = new List<AudioClip>();
        internal List<string> _errors = new List<string>();

        internal void LoadSounds() {
            try {
                if (_audioClips.Count != 0)
                    return;

                // You'll need to figure out the actual path to your asset bundle.
                // It could be alongside your DLL, or in a specific mod data folder.
                string fullPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), SOUND_FOLDER_PATH);

                if (!Directory.Exists(fullPath)) {
                    Logging.LogError($"Sounds not found at: {fullPath}");
                    return;
                }

                foreach (string file in Directory.GetFiles(fullPath, "*" + SOUND_EXTENSION, SearchOption.AllDirectories)) {
                    string filePath = new Uri(Path.GetFullPath(file)).LocalPath;
                    Logging.Log($"{filePath}", Ruleset._clientConfig, true);
                    Logging.Log($"{filePath.Substring(filePath.LastIndexOf('\\') + 1, filePath.Length - filePath.LastIndexOf('\\') - 1).Replace(SOUND_EXTENSION, "")}", Ruleset._clientConfig, true);
                }

                StartCoroutine(GetAudioClips(fullPath));
            }
            catch (Exception ex) {
                Logging.LogError($"Error loading AssetBundle.\n{ex}");
            }
        }

        private IEnumerator GetAudioClips(string path) {
            foreach (string file in Directory.GetFiles(path, "*" + SOUND_EXTENSION, SearchOption.AllDirectories)) {
                string filePath = new Uri(Path.GetFullPath(file)).LocalPath;
                UnityWebRequest webRequest = UnityWebRequestMultimedia.GetAudioClip(filePath, AudioType.OGGVORBIS);
                yield return webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                    _errors.Add(webRequest.error);
                else {
                    try {
                        AudioClip clip = DownloadHandlerAudioClip.GetContent(webRequest);
                        clip.name = filePath.Substring(filePath.LastIndexOf('\\') + 1, filePath.Length - filePath.LastIndexOf('\\') - 1).Replace(SOUND_EXTENSION, "");
                        _audioClips.Add(clip);
                        if (clip.name.Contains(FACEOFF_MUSIC))
                            faceoffMusicList.Add(clip.name);
                    }
                    catch (Exception ex) {
                        _errors.Add(ex.ToString());
                    }
                }
            }
        }

        internal void Play(string name, float delay = 0) {
            if (string.IsNullOrEmpty(name))
                return;

            if (!_soundObjects.TryGetValue(name, out GameObject soundObject)) {
                AudioClip clip = _audioClips.FirstOrDefault(x => x.name == name);
                if (clip == null)
                    return;

                soundObject = new GameObject(name);
                soundObject.AddComponent<AudioSource>();
                soundObject.GetComponent<AudioSource>().clip = clip;
                _soundObjects.Add(name, soundObject);
            }
            soundObject.GetComponent<AudioSource>().volume = SettingsManager.Instance.GlobalVolume;
            if (delay == 0)
                soundObject.GetComponent<AudioSource>().Play();
            else
                soundObject.GetComponent<AudioSource>().PlayDelayed(delay);
        }

        internal void Stop(string name) {
            if (string.IsNullOrEmpty(name) || !_soundObjects.TryGetValue(name, out GameObject soundObject))
                return;
            soundObject.GetComponent<AudioSource>().Stop();
        }

        internal static string GetRandomFaceoffSound() {
            if (faceoffMusicList.Count != 0)
                return faceoffMusicList[new System.Random().Next(0, faceoffMusicList.Count - 1)];

            return "";
        }
    }
}
