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
        private const string SOUNDS_FOLDER_PATH = "sounds";
        private const string SOUND_EXTENSION = ".ogg";

        internal const string LOAD_SOUNDS = "loadsounds";
        internal const string PLAY_SOUND = "playsound";
        internal const string STOP_SOUND = "stopsound";

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

        internal List<string> FaceoffMusicList { get; set; } = new List<string>();
        internal List<string> BlueGoalMusicList { get; set; } = new List<string>();
        internal List<string> RedGoalMusicList { get; set; } = new List<string>();
        internal List<string> BetweenPeriodsMusicList { get; set; } = new List<string>();
        internal List<string> WarmupMusicList { get; set; } = new List<string>();
        internal List<string> LastMinuteMusicList { get; set; } = new List<string>();
        internal List<string> FirstFaceoffMusicList { get; set; } = new List<string>();
        internal List<string> SecondFaceoffMusicList { get; set; } = new List<string>();
        internal List<string> GameOverMusicList { get; set; } = new List<string>();

        private readonly Dictionary<string, GameObject> _soundObjects = new Dictionary<string, GameObject>();
        private readonly List<AudioClip> _audioClips = new List<AudioClip>();
        internal List<string> _errors = new List<string>();

        internal void LoadSounds() {
            try {
                string fullPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), SOUNDS_FOLDER_PATH);

                if (_audioClips.Count == 0 && Ruleset._clientConfig.Music) {
                    DontDestroyOnLoad(gameObject);

                    if (!Directory.Exists(fullPath)) {
                        Logging.LogError($"Sounds not found at: {fullPath}");
                        return;
                    }
                }

                Logging.Log("LoadSounds launching GetAudioClips.", Ruleset._clientConfig);
                StartCoroutine(GetAudioClips(fullPath));
            }
            catch (Exception ex) {
                Logging.LogError($"Error loading Sounds.\n{ex}");
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

        private IEnumerator GetAudioClips(string path) {
            if (_audioClips.Count == 0 && Ruleset._clientConfig.Music) {
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
                            DontDestroyOnLoad(clip);
                            _audioClips.Add(clip);
                            if (clip.name.Contains(FACEOFF_MUSIC))
                                FaceoffMusicList.Add(clip.name);
                            if (clip.name.Contains(BLUE_GOAL_MUSIC))
                                BlueGoalMusicList.Add(clip.name);
                            if (clip.name.Contains(RED_GOAL_MUSIC))
                                RedGoalMusicList.Add(clip.name);
                            if (clip.name.Contains(BETWEEN_PERIODS_MUSIC))
                                BetweenPeriodsMusicList.Add(clip.name);
                            if (clip.name.Contains(WARMUP_MUSIC))
                                WarmupMusicList.Add(clip.name);
                            if (clip.name.Contains(LAST_MINUTE_MUSIC))
                                LastMinuteMusicList.Add(clip.name);
                            if (clip.name.Contains(FIRST_FACEOFF_MUSIC))
                                FirstFaceoffMusicList.Add(clip.name);
                            if (clip.name.Contains(SECOND_FACEOFF_MUSIC))
                                SecondFaceoffMusicList.Add(clip.name);
                            if (clip.name.Contains(GAMEOVER_MUSIC))
                                GameOverMusicList.Add(clip.name);
                        }
                        catch (Exception ex) {
                            _errors.Add(ex.ToString());
                        }
                    }
                }
            }

            if (Ruleset._clientConfig.CustomGoalHorns)
                SetGoalHorns();
        }

        internal void Play(string name, float delay = 0, bool loop = false) {
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
            audioSource.volume = SettingsManager.Instance.GlobalVolume;
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

        internal static string GetRandomSound(List<string> musicList) {
            if (musicList.Count != 0)
                return musicList[new System.Random().Next(0, musicList.Count)];

            return "";
        }

        /// <summary>
        /// Method that sets the custom goal horns.
        /// </summary>
        private void SetGoalHorns() {
            GameObject levelGameObj = GameObject.Find("Level");
            if (!levelGameObj)
                return;

            GameObject soundsGameObj = levelGameObj.transform.Find("Sounds").gameObject;

            if (!soundsGameObj)
                return;

            GameObject blueGoalObj = soundsGameObj.transform.Find("Blue Goal").gameObject;

            if (!blueGoalObj)
                return;

            AudioSource blueGoalAudioSource = blueGoalObj.GetComponent<AudioSource>();
            blueGoalAudioSource.clip = _audioClips.FirstOrDefault(x => x.name == REDGOALHORN);
            blueGoalAudioSource.maxDistance = 400f;

            GameObject redGoalObj = soundsGameObj.transform.Find("Red Goal").gameObject;

            if (!redGoalObj)
                return;

            AudioSource redGoalAudioSource = redGoalObj.GetComponent<AudioSource>();
            redGoalAudioSource.clip = _audioClips.FirstOrDefault(x => x.name == BLUEGOALHORN);
            redGoalAudioSource.maxDistance = 400f;
        }
    }
}
