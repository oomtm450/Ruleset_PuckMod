using oomtm450PuckMod_Ruleset.SystemFunc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace oomtm450PuckMod_Ruleset {
    internal class RefSignals : MonoBehaviour {
        private const string IMAGES_FOLDER_PATH = "images/refsignals";
        private const string IMAGE_EXTENSION = ".png";

        internal const string SHOW_SIGNAL = "showrefsignal";
        internal const string STOP_SIGNAL = "stoprefsignal";

        internal const string ALL = "all";
        internal const string OFFSIDE_LINESMAN = "offside_linesman";

        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

        internal void Start() {
            LoadImages();
        }

        private void LoadImages() {
            try {
                if (_sprites.Count != 0)
                    return;

                string fullPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), IMAGES_FOLDER_PATH);

                if (!Directory.Exists(fullPath)) {
                    Logging.LogError($"Images not found at: {fullPath}");
                    return;
                }

                GetSprites(fullPath);
            }
            catch (Exception ex) {
                Logging.LogError($"Error loading Images.\n{ex}");
            }
        }

        private void GetSprites(string path) {
            foreach (string file in Directory.GetFiles(path, "*" + IMAGE_EXTENSION, SearchOption.AllDirectories)) {
                string filePath = new Uri(Path.GetFullPath(file)).LocalPath;

                string fileName = filePath.Substring(filePath.LastIndexOf('\\') + 1, filePath.Length - filePath.LastIndexOf('\\') - 1).Replace(IMAGE_EXTENSION, "");
                _sprites.Add(fileName, Resources.Load<Sprite>(filePath));

                Component component = gameObject.AddComponent(typeof(Image));
                component.name = fileName;
            }
        }

        internal void ShowSignal(string signal) {
            gameObject.GetComponents<Image>().First(x => x.name == signal).sprite = _sprites[signal];
        }

        internal void StopSignal(string signal) {
            gameObject.GetComponents<Image>().First(x => x.name == signal).sprite = null;
        }

        internal void StopAllSignals() {
            foreach (Image component in gameObject.GetComponents<Image>())
                component.sprite = null;
        }
    }
}
