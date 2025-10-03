using Codebase;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace oomtm450PuckMod_Sounds {
    internal class SoundsSystem : MonoBehaviour {
        #region Constants
        private const string SOUNDS_FOLDER_PATH = "sounds";
        private const string SOUND_EXTENSION = ".ogg";
        #endregion

        #region Fields
        private readonly LockDictionary<string, GameObject> _soundObjects = new LockDictionary<string, GameObject>();
        private readonly List<AudioClip> _audioClips = new List<AudioClip>();

        private AudioSource _currentAudioSource = null;

        private static string _lastRandomSound = "";

        private static bool _isLoading = false;
        #endregion

        #region Properties
        internal List<string> FaceoffMusicList { get; set; } = new List<string>();
        internal List<string> BlueGoalMusicList { get; set; } = new List<string>();
        internal List<string> RedGoalMusicList { get; set; } = new List<string>();
        internal List<string> BetweenPeriodsMusicList { get; set; } = new List<string>();
        internal List<string> WarmupMusicList { get; set; } = new List<string>();
        internal List<string> LastMinuteMusicList { get; set; } = new List<string>();
        internal List<string> FirstFaceoffMusicList { get; set; } = new List<string>();
        internal List<string> SecondFaceoffMusicList { get; set; } = new List<string>();
        internal List<string> GameOverMusicList { get; set; } = new List<string>();
        
        internal List<string> Errors { get; } = new List<string>();
        #endregion

        #region Methods/Functions
        internal void LoadSounds(bool setCustomGoalHorns, string path) {
            try {
                if (_isLoading)
                    return;

                _isLoading = true;

                if (_audioClips.Count == 0)
                    DontDestroyOnLoad(gameObject);

                string[] splittedPath = new string[] { path };
                if (path.Contains('/')) // Linux path
                    splittedPath = path.Split('/');
                else // Windows path
                    splittedPath = path.Split('\\');

                string rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                int lastIndexOf = rootPath.LastIndexOf('/');
                if (lastIndexOf == -1)
                    lastIndexOf = rootPath.LastIndexOf('\\');
                rootPath = rootPath.Substring(0, lastIndexOf);
                string fullPath = Path.Combine(Path.Combine(rootPath, splittedPath[splittedPath.Count() - 2]), splittedPath.Last());

                if (!Directory.Exists(fullPath)) {
                    Logging.LogError($"Sounds not found at: {fullPath}", Sounds.ClientConfig);
                    _isLoading = false;
                    return;
                }

                Logging.Log("LoadSounds launching GetAudioClips.", Sounds.ClientConfig);
                StartCoroutine(GetAudioClips(fullPath, setCustomGoalHorns));
            }
            catch (Exception ex) {
                Logging.LogError($"Error loading Sounds.\n{ex}", Sounds.ClientConfig);
            }
        }

        internal void DestroyGameObjects() {
            while (_soundObjects.Count != 0) {
                var soundObject = _soundObjects.First();
                _soundObjects.Remove(soundObject.Key);
                Destroy(soundObject.Value);
            }

            while (_audioClips.Count != 0) {
                var audioClip = _audioClips.First();
                _audioClips.Remove(audioClip);
                Destroy(audioClip);
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// Function that downloads the streamed audio clips from the mods' folder using WebRequest locally.
        /// </summary>
        /// <param name="path">String, full path to the directory containing the sounds to load.</param>
        /// <param name="setCustomGoalHorns">Bool, true if the custom goal horns has to be set.</param>
        /// <returns>IEnumerator, enumerator used by the Coroutine to load the audio clips.</returns>
        private IEnumerator GetAudioClips(string path, bool setCustomGoalHorns) {
            foreach (string file in Directory.GetFiles(path, "*" + SOUND_EXTENSION, SearchOption.AllDirectories)) {
                string filePath = new Uri(Path.GetFullPath(file)).LocalPath;
                UnityWebRequest webRequest = UnityWebRequestMultimedia.GetAudioClip(filePath, AudioType.OGGVORBIS);
                yield return webRequest.SendWebRequest();
                yield return null;

                if (webRequest.result != UnityWebRequest.Result.Success)
                    Errors.Add(webRequest.error);
                else {
                    try {
                        AudioClip clip = DownloadHandlerAudioClip.GetContent(webRequest);
                        if (!clip) {
                            Errors.Add($"Sounds.{nameof(GetAudioClips)} clip null.");
                            continue;
                        }

                        clip.name = filePath.Substring(filePath.LastIndexOf('\\') + 1, filePath.Length - filePath.LastIndexOf('\\') - 1).Replace(SOUND_EXTENSION, "");
                        DontDestroyOnLoad(clip);
                        _audioClips.Add(clip);

                        AddClipNameToCorrectList(clip.name);

                        // Add a faceoff music twice to the list to double the chance of playing if it's a not a multi part music.
                        // This is going to help music with one ogg to play more.
                        if (!char.IsDigit(clip.name[clip.name.Length - 1]))
                            AddClipNameToCorrectList(clip.name);

                    }
                    catch (Exception ex) {
                        Errors.Add("GetAudioClips 1 : " + ex.ToString());
                    }
                }

                yield return null;
            }

            yield return null;

            try {
                if (setCustomGoalHorns)
                    SetGoalHorns();

                // Reorder all lists to get the same index values for all players.
                ReorderAllLists();
            }
            catch (Exception ex) {
                Errors.Add("GetAudioClips 2 : " + ex.ToString());
            }

            _isLoading = false;
        }

        private void AddClipNameToCorrectList(string clipName) {
            if (clipName.Contains(Codebase.SoundsSystem.FACEOFF_MUSIC))
                FaceoffMusicList.Add(clipName);
            if (clipName.Contains(Codebase.SoundsSystem.BLUE_GOAL_MUSIC))
                BlueGoalMusicList.Add(clipName);
            if (clipName.Contains(Codebase.SoundsSystem.RED_GOAL_MUSIC))
                RedGoalMusicList.Add(clipName);
            if (clipName.Contains(Codebase.SoundsSystem.BETWEEN_PERIODS_MUSIC))
                BetweenPeriodsMusicList.Add(clipName);
            if (clipName.Contains(Codebase.SoundsSystem.WARMUP_MUSIC))
                WarmupMusicList.Add(clipName);
            if (clipName.Contains(Codebase.SoundsSystem.LAST_MINUTE_MUSIC))
                LastMinuteMusicList.Add(clipName);
            if (clipName.Contains(Codebase.SoundsSystem.FIRST_FACEOFF_MUSIC))
                FirstFaceoffMusicList.Add(clipName);
            if (clipName.Contains(Codebase.SoundsSystem.SECOND_FACEOFF_MUSIC))
                SecondFaceoffMusicList.Add(clipName);
            if (clipName.Contains(Codebase.SoundsSystem.GAMEOVER_MUSIC))
                GameOverMusicList.Add(clipName);
        }

        internal void Play(string name, string type, float delay = 0, bool loop = false) {
            if (string.IsNullOrEmpty(name))
                return;

            if (type == Codebase.SoundsSystem.MUSIC && !Sounds.ClientConfig.Music)
                return;

            if (!_soundObjects.TryGetValue(name, out GameObject soundObject)) {
                AudioClip clip = _audioClips.FirstOrDefault(x => x.name == name);
                if (clip == null)
                    return;

                soundObject = new GameObject(name);
                DontDestroyOnLoad(soundObject);
                soundObject.AddComponent<AudioSource>();
                soundObject.GetComponent<AudioSource>().clip = clip;
                DontDestroyOnLoad(soundObject.GetComponent<AudioSource>());
                _soundObjects.Add(name, soundObject);
            }

            AudioSource audioSource = soundObject.GetComponent<AudioSource>();
            audioSource.loop = loop;

            if (type == Codebase.SoundsSystem.MUSIC) {
                _currentAudioSource = audioSource;
                ChangeMusicVolume(Sounds.ClientConfig.MusicVolume);
            }
            else {
                _currentAudioSource = null;
                audioSource.volume = SettingsManager.Instance.GlobalVolume * SettingsManager.Instance.GameVolume;
            }

            if (delay == 0)
                audioSource.Play();
            else
                audioSource.PlayDelayed(delay);
        }

        internal void Stop(string name) {
            if (string.IsNullOrEmpty(name) || !_soundObjects.TryGetValue(name, out GameObject soundObject))
                return;
            soundObject.GetComponent<AudioSource>().Stop();
        }

        /// <summary>
        /// Method that stops all sound and music.
        /// </summary>
        internal void StopAll() {
            foreach (GameObject soundObject in _soundObjects.Values)
                soundObject.GetComponent<AudioSource>().Stop();
        }

        internal void ChangeMusicVolume(float musicVol) {
            if (_currentAudioSource == null)
                return;

            _currentAudioSource.volume = SettingsManager.Instance.GlobalVolume * musicVol;
        }

        internal static string GetRandomSound(List<string> soundList, int? seed = null) {
            string sound = "";
            if (soundList.Count != 0) {
                if (seed == null)
                    sound = soundList[new System.Random().Next(0, soundList.Count)];
                else
                    sound = soundList[new System.Random((int)seed).Next(0, soundList.Count)];

                if (sound == _lastRandomSound) {
                    int soundIndex = soundList.FindIndex(x => x == sound);
                    if (soundIndex == soundList.Count - 1)
                        soundIndex = 0;
                    else
                        soundIndex++;

                    sound = soundList[soundIndex];
                }

                _lastRandomSound = sound;
            }

            return sound;
        }

        /// <summary>
        /// Method that sets the custom goal horns.
        /// </summary>
        private void SetGoalHorns() {
            try {
                if (GameObject.Find("Changing Room"))
                    return;

                GameObject levelGameObj = GameObject.Find("Level");
                if (!levelGameObj) {
                    Errors.Add("Cant't find GameObject \"Level\" !");
                    return;
                }

                GameObject soundsGameObj = levelGameObj.transform.Find("Sounds").gameObject;

                if (!soundsGameObj) {
                    Errors.Add("Cant't find GameObject \"Sounds\" !");
                    return;
                }

                GameObject blueGoalObj = soundsGameObj.transform.Find("Blue Goal").gameObject;

                if (!blueGoalObj) {
                    Errors.Add("Cant't find GameObject \"Blue Goal\" !");
                    return;
                }

                AudioSource blueGoalAudioSource = blueGoalObj.GetComponent<AudioSource>();
                blueGoalAudioSource.clip = _audioClips.FirstOrDefault(x => x.name == Codebase.SoundsSystem.REDGOALHORN);
                blueGoalAudioSource.maxDistance = 400f;

                GameObject redGoalObj = soundsGameObj.transform.Find("Red Goal").gameObject;

                if (!redGoalObj) {
                    Errors.Add("Cant't find GameObject \"Red Goal\" !");
                    return;
                }

                AudioSource redGoalAudioSource = redGoalObj.GetComponent<AudioSource>();
                redGoalAudioSource.clip = _audioClips.FirstOrDefault(x => x.name == Codebase.SoundsSystem.BLUEGOALHORN);
                redGoalAudioSource.maxDistance = 400f;
            }
            catch (Exception ex) {
                Errors.Add(ex.ToString());
            }
        }

        internal static string FormatSoundStrForCommunication(string sound) {
            return sound + $";{new System.Random().Next(0, 100000)}";
        }

        private void ReorderAllLists() {
            FaceoffMusicList = FaceoffMusicList.OrderBy(x => x).ToList();
            BlueGoalMusicList = BlueGoalMusicList.OrderBy(x => x).ToList();
            RedGoalMusicList = RedGoalMusicList.OrderBy(x => x).ToList();
            BetweenPeriodsMusicList = BetweenPeriodsMusicList.OrderBy(x => x).ToList();
            WarmupMusicList = WarmupMusicList.OrderBy(x => x).ToList();
            LastMinuteMusicList = LastMinuteMusicList.OrderBy(x => x).ToList();
            FirstFaceoffMusicList = FirstFaceoffMusicList.OrderBy(x => x).ToList();
            SecondFaceoffMusicList = SecondFaceoffMusicList.OrderBy(x => x).ToList();
            GameOverMusicList = GameOverMusicList.OrderBy(x => x).ToList();
        }
        #endregion
    }
}
