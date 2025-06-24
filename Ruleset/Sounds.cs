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
    public class Sounds : MonoBehaviour {
        public const string SOUND_FOLDER_PATH = "sounds";
        public const string WHISTLE = "whistle.ogg";
        public Dictionary<string, GameObject> _soundObjects = new Dictionary<string, GameObject>();
        public readonly List<AudioClip> _audioClips = new List<AudioClip>();
        public List<string> _errors = new List<string>();

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

                StartCoroutine(GetAudioClip(new Uri(Path.GetFullPath(fullPath)), WHISTLE)); // TODO : Send list and not only WHISTLE.
            }
            catch (Exception ex) {
                Logging.LogError($"Error loading AssetBundle.\n{ex}");
            }
        }

        private IEnumerator GetAudioClip(Uri uri, string name) {
            UnityWebRequest webRequest = UnityWebRequestMultimedia.GetAudioClip(uri.AbsoluteUri + "/" + name, AudioType.OGGVORBIS);
            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
                _errors.Add(webRequest.error);
            else {
                try {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(webRequest);
                    clip.name = name;
                    _audioClips.Add(clip);
                }
                catch (Exception ex) {
                    _errors.Add(ex.ToString());
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
            soundObject.GetComponent<AudioSource>().Play();
        }
    }
}
