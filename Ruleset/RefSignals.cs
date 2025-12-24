using Codebase;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace oomtm450PuckMod_Ruleset {
    /// <summary>
    /// Class containing code for the 2D UI ref.
    /// </summary>
    internal class RefSignals : MonoBehaviour {
        #region Constants
        private const string IMAGES_FOLDER_PATH = "images\\refsignals";
        private const string IMAGE_EXTENSION = ".png";

        private const string REF_SIGNAL = "refsignal";
        private const string SHOW_SIGNAL = "show" + REF_SIGNAL;
        internal const string STOP_SIGNAL = "stop" + REF_SIGNAL;
        internal const string RED = "r";
        internal const string BLUE = "b";
        internal const string SHOW_SIGNAL_BLUE = SHOW_SIGNAL + BLUE;
        internal const string STOP_SIGNAL_BLUE = STOP_SIGNAL + BLUE;
        internal const string SHOW_SIGNAL_RED = SHOW_SIGNAL + RED;
        internal const string STOP_SIGNAL_RED = STOP_SIGNAL + RED;

        private const string LINESMAN = "linesman";
        private const string REF = "ref";
        internal const string ALL = "all";

        internal const string OFFSIDE_LINESMAN = "offside_" + LINESMAN;
        internal const string ICING_LINESMAN = "icing_" + LINESMAN;
        internal const string HIGHSTICK_LINESMAN = "highstick_" + LINESMAN;
        internal const string HIGHSTICK_REF = "highstick_" + REF;
        internal const string INTERFERENCE_REF = "interference_" + REF;
        #endregion

        #region Fields
        private readonly Dictionary<string, Image> _images = new Dictionary<string, Image>();
        private readonly List<GameObject> _imageGameObjects = new List<GameObject>();
        private Canvas _canvas;
        #endregion

        #region Properties
        internal List<string> Errors { get; } = new List<string>();
        #endregion

        #region Methods/Functions
        internal void DestroyGameObjects() {
            while (_images.Count != 0) {
                var imageObject = _images.First();
                _images.Remove(imageObject.Key);
                Destroy(imageObject.Value);
            }

            while (_imageGameObjects.Count != 0) {
                var imageObject = _imageGameObjects.First();
                _imageGameObjects.Remove(imageObject);
                Destroy(imageObject);
            }

            Destroy(gameObject);
        }

        internal void LoadImages(PlayerTeam team) {
            try {
                if (_images.Count != 0)
                    return;

                DontDestroyOnLoad(gameObject);

                string fullPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), IMAGES_FOLDER_PATH);

                if (!Directory.Exists(fullPath)) {
                    Logging.LogError($"Images not found at: {fullPath}", Ruleset.ClientConfig);
                    return;
                }

                CanvasRenderer _canvasRenderer = gameObject.AddComponent<CanvasRenderer>();

                _canvas = gameObject.AddComponent<Canvas>();
                _canvas.name = $"RefSignals_{team}Team_Canvas";
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 0;

                StartCoroutine(GetSprites(fullPath, team));
            }
            catch (Exception ex) {
                Logging.LogError($"Error loading Images.\n{ex}", Ruleset.ClientConfig);
            }
        }

        private IEnumerator GetSprites(string path, PlayerTeam team) {
            foreach (string file in Directory.GetFiles(path, "*" + IMAGE_EXTENSION, SearchOption.AllDirectories)) {
                string filePath = new Uri(Path.GetFullPath(file)).LocalPath;
                UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(filePath);
                yield return webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                    Errors.Add(webRequest.error);
                else {
                    try {
                        string fileName = filePath.Substring(filePath.LastIndexOf('\\') + 1, filePath.Length - filePath.LastIndexOf('\\') - 1).Replace(IMAGE_EXTENSION, "");
                        Texture2D texture = DownloadHandlerTexture.GetContent(webRequest);

                        GameObject _gameObject = new GameObject($"RefSignals_{team}Team_Images");
                        DontDestroyOnLoad(_gameObject);
                        _imageGameObjects.Add(_gameObject);

                        Image image = _gameObject.AddComponent<Image>();
                        DontDestroyOnLoad(image);
                        image.name = fileName + "_Image";
                        image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        image.preserveAspect = true;
                        image.transform.SetParent(_canvas.transform, false);
                        image.enabled = false;

                        RectTransform rectTransform = image.GetComponent<RectTransform>();
                        rectTransform.sizeDelta = new Vector2(300, 300) * Ruleset.ClientConfig.TwoDRefsScale;
                        rectTransform.pivot = new Vector2(0.5f, 0.5f);

                        if (team == PlayerTeam.Red) {
                            rectTransform.anchorMin = new Vector2(0.925f, 0.37f);
                            rectTransform.anchorMax = new Vector2(0.925f, 0.37f);
                        }
                        else {
                            rectTransform.anchorMin = new Vector2(0.075f, 0.37f);
                            rectTransform.anchorMax = new Vector2(0.075f, 0.37f);
                        }

                        _images.Add(fileName, image);
                    }
                    catch (Exception ex) {
                        Errors.Add(ex.ToString());
                    }
                }
            }
        }

        internal void ShowSignal(string signal) {
            _images[signal].enabled = true;
        }

        internal void StopSignal(string signal) {
            _images[signal].enabled = false;
        }

        internal void StopAllSignals() {
            foreach (Image image in _images.Values)
                image.enabled = false;
        }

        internal void Change2DRefsScale(float scale) {
            foreach (Image image in _images.Values) {
                RectTransform rectTransform = image.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(300, 300) * scale;
            }
        }

        internal static string GetSignalConstant(bool showSignal, PlayerTeam team) {
            if (showSignal) {
                if (team == PlayerTeam.Blue)
                    return SHOW_SIGNAL_BLUE;
                return SHOW_SIGNAL_RED;
            }

            if (team == PlayerTeam.Blue)
                return STOP_SIGNAL_BLUE;
            return STOP_SIGNAL_RED;
        }
        #endregion
    }
}
