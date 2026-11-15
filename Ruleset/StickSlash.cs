using Codebase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    internal class StickSlash {
        internal class StickSample { public Vector3 TipPos; public float Time; }

        internal static class StickSwingTracker {
            static readonly LockDictionary<Stick, LockList<StickSample>> _samples = new LockDictionary<Stick, LockList<StickSample>>();

            // Call from FixedUpdate for each spawned stick (blade tip or contact point).
            internal static void Record(Stick stick, Vector3 tipPos) {
                if (!_samples.TryGetValue(stick, out var list))
                    _samples[stick] = list = new LockList<StickSample>();

                list.Add(new StickSample {
                    TipPos = tipPos,
                    Time = Time.time,
                });
                list.RemoveAll(s => Time.time - s.Time > 0.25f);
            }

            internal static (float PeakSpeed, float PeakY, float DownRatio) Analyze(Stick stick, float window) {
                if (!_samples.TryGetValue(stick, out var l) || l.Count < 2) return (0, 0, 0);
                float peakSpeed = 0, peakY = float.MinValue, down = 0;
                for (int i = 1; i < l.Count; i++) {
                    if (Time.time - l[i].Time > window) continue;
                    Vector3 v = (l[i].TipPos - l[i - 1].TipPos) / Mathf.Max(l[i].Time - l[i - 1].Time, 0.0001f);
                    float spd = v.magnitude;
                    if (spd > peakSpeed) {
                        peakSpeed = spd;
                        down = spd > 0 ? -v.y / spd : 0;
                    }
                    peakY = Mathf.Max(peakY, l[i].TipPos.y);
                }
                return (peakSpeed, peakY, down);
            }
        }
    }
}
