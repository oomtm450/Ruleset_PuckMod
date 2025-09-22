using Codebase;
using oomtm450PuckMod_Sounds;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace oomtm450PuckMod_Ruleset {
    internal class SoundsSystem : MonoBehaviour {
        #region Constants
        private const string SOUNDS_FOLDER_PATH = "sounds";
        private const string SOUND_EXTENSION = ".ogg";

        internal const string LOAD_SOUNDS = "loadsounds";
        internal const string PLAY_SOUND = "playsound";
        internal const string STOP_SOUND = "stopsound";

        internal const string ALL = "all";
        internal const string MUSIC = "music";
        internal const string WHISTLE = "whistle";
        internal const string BLUEGOALHORN = "bluegoalhorn";
        internal const string REDGOALHORN = "redgoalhorn";
        internal const string FACEOFF_MUSIC = "faceoffmusic";
        internal const string FACEOFF_MUSIC_DELAYED = FACEOFF_MUSIC + "d";

        internal const string BLUE_GOAL_MUSIC = "bluegoalmusic";
        internal const string RED_GOAL_MUSIC = "redgoalmusic";
        internal const string BETWEEN_PERIODS_MUSIC = "betweenperiodsmusic";
        internal const string WARMUP_MUSIC = "warmupmusic";

        internal const string LAST_MINUTE_MUSIC = "lastminutemusic";
        internal const string LAST_MINUTE_MUSIC_DELAYED = LAST_MINUTE_MUSIC + "d";

        internal const string FIRST_FACEOFF_MUSIC = "faceofffirstmusic";
        internal const string FIRST_FACEOFF_MUSIC_DELAYED = FIRST_FACEOFF_MUSIC + "d";

        internal const string SECOND_FACEOFF_MUSIC = "faceoffsecondmusic";
        internal const string SECOND_FACEOFF_MUSIC_DELAYED = SECOND_FACEOFF_MUSIC + "d";

        internal const string GAMEOVER_MUSIC = "gameovermusic";
        #endregion

        #region Fields
        private readonly LockDictionary<string, GameObject> _soundObjects = new LockDictionary<string, GameObject>();
        private readonly List<AudioClip> _audioClips = new List<AudioClip>();

        private AudioSource _currentAudioSource = null;

        private static string _lastRandomSound = "";
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
        internal void LoadSounds(bool loadMusics, bool setCustomGoalHorns) {
            try {
                string fullPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), SOUNDS_FOLDER_PATH);

                if (_audioClips.Count == 0 && loadMusics) {
                    DontDestroyOnLoad(gameObject);

                    if (!Directory.Exists(fullPath)) {
                        Logging.LogError($"Sounds not found at: {fullPath}", Sounds.ClientConfig);
                        return;
                    }
                }

                Logging.Log("LoadSounds launching GetAudioClips.", Sounds.ClientConfig);
                StartCoroutine(GetAudioClips(fullPath, loadMusics, setCustomGoalHorns));
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
        /// <param name="loadMusics">Bool, true if the music has to be loaded with the other sounds.</param>
        /// <param name="setCustomGoalHorns">Bool, true if the custom goal horns has to be set.</param>
        /// <returns>IEnumerator, enumerator used by the Coroutine to load the audio clips.</returns>
        private IEnumerator GetAudioClips(string path, bool loadMusics, bool setCustomGoalHorns) {
            if (_audioClips.Count == 0 && loadMusics) {
                foreach (string file in Directory.GetFiles(path, "*" + SOUND_EXTENSION, SearchOption.AllDirectories)) {
                    string filePath = new Uri(Path.GetFullPath(file)).LocalPath;
                    UnityWebRequest webRequest = UnityWebRequestMultimedia.GetAudioClip(filePath, AudioType.OGGVORBIS);
                    yield return webRequest.SendWebRequest();

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
                            Errors.Add(ex.ToString());
                        }
                    }
                }
            }

            if (setCustomGoalHorns)
                SetGoalHorns();
        }

        private void AddClipNameToCorrectList(string clipName) {
            if (clipName.Contains(FACEOFF_MUSIC))
                FaceoffMusicList.Add(clipName);
            if (clipName.Contains(BLUE_GOAL_MUSIC))
                BlueGoalMusicList.Add(clipName);
            if (clipName.Contains(RED_GOAL_MUSIC))
                RedGoalMusicList.Add(clipName);
            if (clipName.Contains(BETWEEN_PERIODS_MUSIC))
                BetweenPeriodsMusicList.Add(clipName);
            if (clipName.Contains(WARMUP_MUSIC))
                WarmupMusicList.Add(clipName);
            if (clipName.Contains(LAST_MINUTE_MUSIC))
                LastMinuteMusicList.Add(clipName);
            if (clipName.Contains(FIRST_FACEOFF_MUSIC))
                FirstFaceoffMusicList.Add(clipName);
            if (clipName.Contains(SECOND_FACEOFF_MUSIC))
                SecondFaceoffMusicList.Add(clipName);
            if (clipName.Contains(GAMEOVER_MUSIC))
                GameOverMusicList.Add(clipName);
        }

        internal void Play(string name, string type, float delay = 0, bool loop = false) {
            if (string.IsNullOrEmpty(name))
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

            if (type == MUSIC) {
                _currentAudioSource = audioSource;
                ChangeMusicVolume(Ruleset._clientConfig.MusicVolume);
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
                blueGoalAudioSource.clip = _audioClips.FirstOrDefault(x => x.name == REDGOALHORN);
                blueGoalAudioSource.maxDistance = 400f;

                GameObject redGoalObj = soundsGameObj.transform.Find("Red Goal").gameObject;

                if (!redGoalObj) {
                    Errors.Add("Cant't find GameObject \"Red Goal\" !");
                    return;
                }

                AudioSource redGoalAudioSource = redGoalObj.GetComponent<AudioSource>();
                redGoalAudioSource.clip = _audioClips.FirstOrDefault(x => x.name == BLUEGOALHORN);
                redGoalAudioSource.maxDistance = 400f;
            }
            catch (Exception ex) {
                Errors.Add(ex.ToString());
            }
        }

        internal static string FormatSoundStrForCommunication(string sound) {
            return sound + $";{new System.Random().Next(0, 100000)}";
        }
        #endregion
    }
}
