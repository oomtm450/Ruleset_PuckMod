using oomtm450PuckMod_Ruleset.SystemFunc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace oomtm450PuckMod_Ruleset {
    internal class RefSignals : MonoBehaviour {
        private const string IMAGES_FOLDER_PATH = "images\\refsignals";
        private const string IMAGE_EXTENSION = ".png";

        internal const string SHOW_SIGNAL = "showrefsignal";
        internal const string STOP_SIGNAL = "stoprefsignal";

        internal const string ALL = "all";
        internal const string OFFSIDE_LINESMAN = "offside_linesman";
        internal const string ICING_LINESMAN = "icing_linesman";
        internal const string HIGHSTICK_LINESMAN = "highstick_linesman";
        internal const string HIGHSTICK_REF = "highstick_ref";
        internal const string INTERFERENCE_REF = "interference_ref";

        private readonly Dictionary<string, Image> _images = new Dictionary<string, Image>();
        internal List<string> _errors = new List<string>();
        private Canvas _canvas;

        internal void Update() {
            Player localPlayer = PlayerManager.Instance.GetLocalPlayer();
            if (!localPlayer || !localPlayer.IsCharacterFullySpawned)
                return;

            //_canvas.transform.position = new Vector3(localPlayer.PlayerCamera.transform.position.x, localPlayer.PlayerCamera.transform.position.y,
                //localPlayer.PlayerCamera.transform.position.z + 1);
            //_canvas.transform.rotation = Quaternion.LookRotation(transform.position - localPlayer.PlayerCamera.transform.position);
        }

        internal void LoadImages() {
            try {
                if (_images.Count != 0)
                    return;

                DontDestroyOnLoad(gameObject);

                string fullPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), IMAGES_FOLDER_PATH);

                if (!Directory.Exists(fullPath)) {
                    Logging.LogError($"Images not found at: {fullPath}");
                    return;
                }

                CanvasRenderer _canvasRenderer = gameObject.AddComponent<CanvasRenderer>();

                _canvas = gameObject.AddComponent<Canvas>();
                _canvas.name = "RefSignalsCanvas";
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 0;

                StartCoroutine(GetSprites(fullPath));
            }
            catch (Exception ex) {
                Logging.LogError($"Error loading Images.\n{ex}");
            }
        }

        private IEnumerator GetSprites(string path) {
            foreach (string file in Directory.GetFiles(path, "*" + IMAGE_EXTENSION, SearchOption.AllDirectories)) {
                string filePath = new Uri(Path.GetFullPath(file)).LocalPath;
                UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(filePath);
                yield return webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                    _errors.Add(webRequest.error);
                else {
                    try {
                        string fileName = filePath.Substring(filePath.LastIndexOf('\\') + 1, filePath.Length - filePath.LastIndexOf('\\') - 1).Replace(IMAGE_EXTENSION, "");
                        Texture2D texture = DownloadHandlerTexture.GetContent(webRequest);

                        GameObject _gameObject = new GameObject("ImagesTest");
                        DontDestroyOnLoad(_gameObject);

                        Image image = _gameObject.AddComponent<Image>();
                        image.name = fileName + "_Image";
                        image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        image.preserveAspect = true;
                        image.transform.SetParent(_canvas.transform, false);
                        image.enabled = false;

                        RectTransform rectTransform = image.GetComponent<RectTransform>();
                        rectTransform.sizeDelta = new Vector2(300, 300);
                        rectTransform.pivot = new Vector2(0.5f, 0.5f);
                        rectTransform.anchorMin = new Vector2(0.925f, 0.37f);
                        rectTransform.anchorMax = new Vector2(0.925f, 0.37f);

                        _images.Add(fileName, image);
                    }
                    catch (Exception ex) {
                        _errors.Add(ex.ToString());
                    }
                }
            }
        }

        internal void ShowSignal(string signal) {
            _images[signal].enabled = true;
            Logging.Log($"Image name : {_images[signal].name}", Ruleset._serverConfig, true); // TODO : Remove debug log.
        }

        internal void StopSignal(string signal) {
            _images[signal].enabled = false;
        }

        internal void StopAllSignals() {
            foreach (Image image in _images.Values)
                image.enabled = false;
        }
    }
}
