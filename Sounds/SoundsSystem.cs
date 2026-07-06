using Codebase;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using static oomtm450PuckMod_Sounds.SoundsSystem;

namespace oomtm450PuckMod_Sounds {
    internal class SoundsSystem : MonoBehaviour {
        #region Constants
        private const string SOUND_EXTENSION = ".ogg";

        private const string SOUNDS_SETTINGS_FILENAME = "settings.json";

        private const float DEFAULT_SOUND_VOLUME = 1f;

        private static float DEFAULT_HORN_VOLUME = DEFAULT_SOUND_VOLUME;

        private const int DEFAULT_SOUND_WEIGHT = 60;

        private const float DEFAULT_SOUND_DELAY = 0;
        #endregion

        #region Fields
        private readonly LockDictionary<string, GameObject> _soundObjects = new LockDictionary<string, GameObject>();
        private readonly LockList<AudioClip> _audioClips = new LockList<AudioClip>();
        private readonly LockDictionary<string, SoundSettings> _soundSettings = new LockDictionary<string, SoundSettings>();

        private AudioSource _currentAudioSource = null;

        private static string _lastRandomSound = "";

        private static Dictionary<SoundType, string> _lastRandomSoundPerType = new Dictionary<SoundType, string>();

        private int _isLoadingValue = 0;
        #endregion

        #region Properties
        internal LockWeightedList<string> FaceoffMusicList { get; set; } = new LockWeightedList<string>();
        internal LockWeightedList<string> BlueGoalMusicList { get; set; } = new LockWeightedList<string>();
        internal LockWeightedList<string> RedGoalMusicList { get; set; } = new LockWeightedList<string>();
        internal LockWeightedList<string> BetweenPeriodsMusicList { get; set; } = new LockWeightedList<string>();
        internal LockWeightedList<string> WarmupMusicList { get; set; } = new LockWeightedList<string>();
        internal LockWeightedList<string> LastMinuteMusicList { get; set; } = new LockWeightedList<string>();
        internal LockWeightedList<string> FirstFaceoffMusicList { get; set; } = new LockWeightedList<string>();
        internal LockWeightedList<string> SecondFaceoffMusicList { get; set; } = new LockWeightedList<string>();
        internal LockWeightedList<string> GameOverMusicList { get; set; } = new LockWeightedList<string>();
        
        internal LockList<string> Errors { get; } = new LockList<string>();

        internal LockList<string> Warnings { get; } = new LockList<string>();

        private bool IsLoading {
            get { return Interlocked.CompareExchange(ref _isLoadingValue, 1, 1) == 1; }
            set {
                if (value)
                    Interlocked.CompareExchange(ref _isLoadingValue, 1, 0);
                else
                    Interlocked.CompareExchange(ref _isLoadingValue, 0, 1);
            }
        }
        #endregion

        #region Methods/Functions
        internal bool LoadSounds(bool setCustomGoalHorns, string path) {
            try {
                if (IsLoading)
                    return false;

                IsLoading = true;

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
                    IsLoading = false;
                    return true;
                }

                Logging.Log($"{nameof(LoadSounds)} launching {nameof(GetAudioClips)}. ({fullPath})", Sounds.ClientConfig);
                StartCoroutine(GetAudioClips(fullPath, setCustomGoalHorns));
            }
            catch (Exception ex) {
                Logging.LogError($"Error loading Sounds {path}.\n{ex}", Sounds.ClientConfig);
                IsLoading = false;
            }

            return true;
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
            string[] files = Array.Empty<string>();

            bool tryGetFiles = true;
            string jsonPath = "";
            Dictionary<string, SoundSettings> currentConfig = new Dictionary<string, SoundSettings>();
            bool currentConfigWasEmpty = false;
            while (tryGetFiles) {
                tryGetFiles = false;

                try {
                    files = Directory.GetFiles(path, "*" + SOUND_EXTENSION, SearchOption.AllDirectories);
                }
                catch (Exception ex) {
                    tryGetFiles = true;
                    Warnings.Add($"Sounds.{nameof(GetAudioClips)} 1 : {ex}");
                }

                try {
                    jsonPath = Path.Combine(path, SOUNDS_SETTINGS_FILENAME);

                    if (File.Exists(jsonPath)) {
                        string settingsFileContent = File.ReadAllText(jsonPath);
                        currentConfig = settingsFileContent.ToSoundSettings();
                        if (currentConfig.Count == 0)
                            currentConfigWasEmpty = true;
                        else {
                            foreach (string key in new List<string>(currentConfig.Keys)) {
                                SoundSettings soundSetting = currentConfig[key];
                                currentConfig[key] = new SoundSettings {
                                    Weight = soundSetting.Weight ?? DEFAULT_SOUND_WEIGHT,
                                    Volume = soundSetting.Volume ?? DEFAULT_SOUND_VOLUME,
                                    Delay = soundSetting.Delay ?? DEFAULT_SOUND_DELAY,
                                };
                            }
                        }
                    }
                }
                catch (Exception ex) {
                    Warnings.Add($"Sounds.{nameof(GetAudioClips)} 2 : {ex}");
                }

                if (tryGetFiles)
                    yield return null;
            }

            foreach (string file in files) {
                string filePath = new Uri(Path.GetFullPath(file)).LocalPath;
                if (Path.GetExtension(filePath).ToLowerInvariant() != SOUND_EXTENSION) {
                    yield return null;
                    continue;
                }

                UnityWebRequest webRequest = UnityWebRequestMultimedia.GetAudioClip(filePath, AudioType.OGGVORBIS);
                yield return webRequest.SendWebRequest();
                yield return null;

                if (webRequest.result != UnityWebRequest.Result.Success)
                    Warnings.Add(webRequest.error);
                else {
                    try {
                        AudioClip clip = DownloadHandlerAudioClip.GetContent(webRequest);
                        if (!clip) {
                            Errors.Add($"Sounds.{nameof(GetAudioClips)} clip null.");
                            continue;
                        }

                        clip.name = filePath.Substring(filePath.LastIndexOf('\\') + 1, filePath.Length - filePath.LastIndexOf('\\') - 1).Replace(SOUND_EXTENSION, "");

                        if (!currentConfig.TryGetValue(clip.name, out SoundSettings clipSettings)) {
                            clipSettings = new SoundSettings {
                                Weight = DEFAULT_SOUND_WEIGHT,
                                Volume = DEFAULT_SOUND_VOLUME,
                                Delay = DEFAULT_SOUND_DELAY,
                            };
                            currentConfig.Add(clip.name, clipSettings);
                        }

                        if (clipSettings.Weight <= 0)
                            continue;

                        DontDestroyOnLoad(clip);
                        _audioClips.Add(clip);

                        AddClipNameToCorrectList(clip.name, (int)clipSettings.Weight);
                    }
                    catch (Exception ex) {
                        Errors.Add($"Sounds.{nameof(GetAudioClips)} 3 : {ex}");
                    }
                }

                yield return null;
            }

            yield return null;

            try {
                foreach (string key in currentConfig.Keys)
                    _soundSettings.AddOrUpdate(key, currentConfig[key]);

                if (!string.IsNullOrEmpty(jsonPath) && !currentConfigWasEmpty)
                    File.WriteAllText(jsonPath, currentConfig.ToDictionary((x) => x.Key, (x) => x.Value).ToJSON());
            }
            catch (Exception ex) {
                Warnings.Add($"Sounds.{nameof(GetAudioClips)} 4 : {ex}");
            }

            yield return null;

            try {
                if (setCustomGoalHorns)
                    SetGoalHorns();
            }
            catch (Exception ex) {
                Errors.Add($"Sounds.{nameof(GetAudioClips)} 5 : {ex}");
            }

            try {
                // Reorder all lists to get the same index values for all players.
                ReorderAllLists();
            }
            catch (Exception ex) {
                Errors.Add($"Sounds.{nameof(GetAudioClips)} 6 : {ex}");
            }

            IsLoading = false;
        }

        private void AddClipNameToCorrectList(string clipName, int weight = DEFAULT_SOUND_WEIGHT) {
            if (clipName.Contains(Codebase.SoundsSystem.FACEOFF_MUSIC))
                FaceoffMusicList.Add(clipName, weight);
            if (clipName.Contains(Codebase.SoundsSystem.BLUE_GOAL_MUSIC))
                BlueGoalMusicList.Add(clipName, weight);
            if (clipName.Contains(Codebase.SoundsSystem.RED_GOAL_MUSIC))
                RedGoalMusicList.Add(clipName, weight);
            if (clipName.Contains(Codebase.SoundsSystem.BETWEEN_PERIODS_MUSIC))
                BetweenPeriodsMusicList.Add(clipName, weight);
            if (clipName.Contains(Codebase.SoundsSystem.WARMUP_MUSIC))
                WarmupMusicList.Add(clipName, weight);
            if (clipName.Contains(Codebase.SoundsSystem.LAST_MINUTE_MUSIC))
                LastMinuteMusicList.Add(clipName, weight);
            if (clipName.Contains(Codebase.SoundsSystem.FIRST_FACEOFF_MUSIC))
                FirstFaceoffMusicList.Add(clipName, weight);
            if (clipName.Contains(Codebase.SoundsSystem.SECOND_FACEOFF_MUSIC))
                SecondFaceoffMusicList.Add(clipName, weight);
            if (clipName.Contains(Codebase.SoundsSystem.GAMEOVER_MUSIC))
                GameOverMusicList.Add(clipName, weight);
        }

        internal void Play(string name, string type, float vol = float.MaxValue, float delay = 0, bool loop = false) {
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

            SoundSettings soundSettings = null;
            float volModifier = DEFAULT_SOUND_VOLUME;
            float delayModifier = DEFAULT_SOUND_DELAY;
            if (_soundSettings.TryGetValue(name, out soundSettings)) {
                volModifier = (float)soundSettings.Volume;
                delayModifier = (float)soundSettings.Delay;
            }

            vol *= volModifier;
            audioSource.volume = vol;

            delay += delayModifier;

            if (type == Codebase.SoundsSystem.MUSIC) {
                _currentAudioSource = audioSource;
                if (vol != float.MaxValue)
                    ChangeVolume(vol);
                else
                    ChangeVolume(SettingsManager.GlobalVolume * SettingsManager.GameVolume * volModifier);
            }
            else {
                _currentAudioSource = null;
                audioSource.volume = SettingsManager.GlobalVolume * SettingsManager.GameVolume * volModifier;
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

        internal void ChangeVolume(float vol) {
            if (_currentAudioSource == null)
                return;

            _currentAudioSource.volume = SettingsManager.GlobalVolume * vol;
        }

        internal static void ChangeHornVolume(float hornVol, AudioSource hornAudioSource) {
            hornAudioSource.volume = DEFAULT_HORN_VOLUME * hornVol;
        }

        internal static void ChangeHornsVolume(float hornVol, List<AudioSource> hornsAudioSource) {
            foreach (AudioSource hornAudioSource in hornsAudioSource)
                ChangeHornVolume(hornVol, hornAudioSource);
        }

        internal static string GetRandomSound(LockWeightedList<string> soundList, SoundType type = SoundType.None, int? seed = null) {
            soundList = new LockWeightedList<string>(soundList, soundList.GetWeightOf);
            int soundListCount = soundList.Count();

            string lastRandomSoundOfType = "";
            if (type != SoundType.None)
                _lastRandomSoundPerType.TryGetValue(type, out lastRandomSoundOfType);

            if (soundListCount == 0)
                return "";
            else if (soundListCount == 1)
                return soundList.First();
            else if (soundListCount == 2) {
                string _sound = soundList.First();
                if (_sound != _lastRandomSound && _sound != lastRandomSoundOfType)
                    _lastRandomSound = _sound;
                else
                    _lastRandomSound = _sound = soundList.ElementAt(1);

                if (type != SoundType.None) {
                    if (!_lastRandomSoundPerType.TryAdd(type, _lastRandomSound))
                        _lastRandomSoundPerType[type] = _lastRandomSound;
                }

                return _sound;
            }

            string sound = _lastRandomSound;

            if (seed != null)
                soundList.SetRandomSeed((int)seed);

            while (string.IsNullOrEmpty(sound) || sound == _lastRandomSound || sound == lastRandomSoundOfType) {
                sound = soundList.Next();
                soundList.Remove(sound);
            }

            _lastRandomSound = sound;
            if (type != SoundType.None) {
                if (!_lastRandomSoundPerType.TryAdd(type, _lastRandomSound))
                    _lastRandomSoundPerType[type] = _lastRandomSound;
            }

            return sound;
        }

        /// <summary>
        /// Method that sets the custom goal horns.
        /// </summary>
        internal void SetGoalHorns() {
            try {
                (AudioSource blueGoalAudioSource, AudioSource redGoalAudioSource) = GetHornsAudioSource(Errors);
                if (blueGoalAudioSource == null || redGoalAudioSource == null)
                    return;

                blueGoalAudioSource.clip = _audioClips.FirstOrDefault(x => x.name.Contains(Codebase.SoundsSystem.REDGOALHORN));
                blueGoalAudioSource.maxDistance = 400f;
                DEFAULT_HORN_VOLUME = blueGoalAudioSource.volume;
                
                redGoalAudioSource.clip = _audioClips.FirstOrDefault(x => x.name.Contains(Codebase.SoundsSystem.BLUEGOALHORN));
                redGoalAudioSource.maxDistance = 400f;

                ChangeHornsVolume(Sounds.ClientConfig.HornVolume, new List<AudioSource> { blueGoalAudioSource, redGoalAudioSource, });
            }
            catch (Exception ex) {
                Errors.Add(ex.ToString());
            }
        }

        internal static (AudioSource BlueGoalAudioSource, AudioSource RedGoalAudioSource) GetHornsAudioSource(LockList<string> errors = null) {
            if (GameObject.Find("Changing Room"))
                return (null, null);

            GameObject levelGameObj = GameObject.Find("Level Default");
            if (!levelGameObj) {
                errors?.Add("Cant't find GameObject \"Level Default\" !");
                return (null, null);
            }

            GameObject soundsGameObj = levelGameObj.transform.Find("Sounds").gameObject;

            if (!soundsGameObj) {
                errors?.Add("Cant't find GameObject \"Sounds\" !");
                return (null, null);
            }

            GameObject blueGoalObj = soundsGameObj.transform.Find("Blue Goal").gameObject;

            if (!blueGoalObj) {
                errors?.Add("Cant't find GameObject \"Blue Goal\" !");
                return (null, null);
            }

            GameObject redGoalObj = soundsGameObj.transform.Find("Red Goal").gameObject;

            if (!redGoalObj) {
                errors?.Add("Cant't find GameObject \"Red Goal\" !");
                return (null, null);
            }

            return (blueGoalObj.GetComponent<AudioSource>(), redGoalObj.GetComponent<AudioSource>());
        }

        internal static string FormatSoundStrForCommunication(string sound, string chosenClipName = "") {
            if (!string.IsNullOrEmpty(chosenClipName))
                sound += $";{chosenClipName}";
            else
                sound += $";{new System.Random().Next(0, 100000)}";
            return sound;
        }

        /// <summary>
        /// Method that swaps the goal horn AudioSource clip to a donor's chosen horn for the next fire.
        /// The "Blue Goal" GameObject is the goal Blue defends, so it fires when Red scores — i.e.
        /// the scoring team and the firing AudioSource are CROSSED. No-op if the clip isn't loaded
        /// locally (client didn't subscribe to the donor pack), in which case the default scene-load
        /// clip plays unchanged.
        /// </summary>
        /// <param name="clipName">String, exact AudioClip name to swap in.</param>
        /// <param name="scoringTeam">PlayerTeam, team that just scored.</param>
        internal void SetGoalHornForNext(string clipName, PlayerTeam scoringTeam) {
            try {
                if (string.IsNullOrEmpty(clipName))
                    return;

                AudioClip clip = _audioClips.FirstOrDefault(x => x.name == clipName);
                if (clip == null)
                    return;

                GameObject levelGameObj = GameObject.Find("Level Default");
                if (!levelGameObj)
                    return;

                Transform soundsTransform = levelGameObj.transform.Find("Sounds");
                if (!soundsTransform)
                    return;

                string targetGoalName = scoringTeam == PlayerTeam.Blue ? "Red Goal" : "Blue Goal";
                Transform goalTransform = soundsTransform.Find(targetGoalName);
                if (!goalTransform)
                    return;

                AudioSource audioSource = goalTransform.GetComponent<AudioSource>();
                if (audioSource == null)
                    return;

                audioSource.clip = clip;
            }
            catch (Exception ex) {
                Errors.Add(ex.ToString());
            }
        }

        private void ReorderAllLists() {
            FaceoffMusicList = new LockWeightedList<string>(FaceoffMusicList.OrderBy(x => x), FaceoffMusicList.GetWeightOf);
            BlueGoalMusicList = new LockWeightedList<string>(BlueGoalMusicList.OrderBy(x => x), BlueGoalMusicList.GetWeightOf);
            RedGoalMusicList = new LockWeightedList<string>(RedGoalMusicList.OrderBy(x => x), RedGoalMusicList.GetWeightOf);
            BetweenPeriodsMusicList = new LockWeightedList<string>(BetweenPeriodsMusicList.OrderBy(x => x), BetweenPeriodsMusicList.GetWeightOf);
            WarmupMusicList = new LockWeightedList<string>(WarmupMusicList.OrderBy(x => x), WarmupMusicList.GetWeightOf);
            LastMinuteMusicList = new LockWeightedList<string>(LastMinuteMusicList.OrderBy(x => x), LastMinuteMusicList.GetWeightOf);
            FirstFaceoffMusicList = new LockWeightedList<string>(FirstFaceoffMusicList.OrderBy(x => x), FirstFaceoffMusicList.GetWeightOf);
            SecondFaceoffMusicList = new LockWeightedList<string>(SecondFaceoffMusicList.OrderBy(x => x), SecondFaceoffMusicList.GetWeightOf);
            GameOverMusicList = new LockWeightedList<string>(GameOverMusicList.OrderBy(x => x), GameOverMusicList.GetWeightOf);
        }
        #endregion

        internal class SoundSettings {
            public float? Volume { get; set; } = DEFAULT_SOUND_VOLUME;

            public int? Weight { get; set; } = DEFAULT_SOUND_WEIGHT;

            public float? Delay { get; set; } = DEFAULT_SOUND_DELAY;
        }

        internal enum SoundType {
            None = 0,
            Faceoff = 1,
            BetweenPeriods = 2,
            Warmup = 3,
            RedGoal = 4,
            BlueGoal = 5,
            FirstFaceoff = 6,
            SecondFaceoff = 7,
            LastMinuteFaceoff = 8,
        }
    }

    internal static class SoundSettingsExtension {
        /// <summary>
        /// Function that serialize the config object.
        /// </summary>
        /// <returns>String, serialized config.</returns>
        internal static string ToJSON(this Dictionary<string, SoundSettings> soundSettings) => JsonConvert.SerializeObject(soundSettings, Formatting.Indented);

        /// <summary>
        /// Function that unserialize a ServerConfig.
        /// </summary>
        /// <param name="json">String, JSON that is the serialized ServerConfig.</param>
        /// <returns>ServerConfig, unserialized ServerConfig.</returns>
        internal static Dictionary<string, SoundSettings> ToSoundSettings(this string json) => JsonConvert.DeserializeObject<Dictionary<string, SoundSettings>>(json);
    }
}
