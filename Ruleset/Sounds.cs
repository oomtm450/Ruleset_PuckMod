using oomtm450PuckMod_Ruleset.SystemFunc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        internal static List<string> faceoffMusicList = new List<string>();

        private readonly Dictionary<string, GameObject> _soundObjects = new Dictionary<string, GameObject>();
        private readonly List<AudioClip> _audioClips = new List<AudioClip>();
        internal List<string> _errors = new List<string>();

        internal void LoadWhistlePrefab() {
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

                Uri uri = new Uri(Path.GetFullPath(fullPath));
                StartCoroutine(GetAudioClips(uri));
            }
            catch (Exception ex) {
                Logging.LogError($"Error loading AssetBundle.\n{ex}");
            }
        }

        private IEnumerator GetAudioClips(Uri uri) {
            foreach (string name in Directory.GetFiles(uri.AbsolutePath, "*" + SOUND_EXTENSION, SearchOption.AllDirectories)) {
                UnityWebRequest webRequest = UnityWebRequestMultimedia.GetAudioClip(name + SOUND_EXTENSION, AudioType.OGGVORBIS);
                yield return webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                    _errors.Add(webRequest.error);
                else {
                    try {
                        AudioClip clip = DownloadHandlerAudioClip.GetContent(webRequest);
                        clip.name = name;
                        _audioClips.Add(clip);
                        if (name.Contains(FACEOFF_MUSIC))
                            faceoffMusicList.Add(name);
                    }
                    catch (Exception ex) {
                        _errors.Add(ex.ToString());
                    }
                }
            }
        }

        internal void Play(string name) {
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
            soundObject.GetComponent<AudioSource>().Play();
        }

        internal void Stop(string name) {
            if (!_soundObjects.TryGetValue(name, out GameObject soundObject))
                return;
            soundObject.GetComponent<AudioSource>().Stop();
        }

        internal static string GetRandomFaceoffSound() {
            return faceoffMusicList[new System.Random().Next(0, faceoffMusicList.Count - 1)];
        }
    }
}
