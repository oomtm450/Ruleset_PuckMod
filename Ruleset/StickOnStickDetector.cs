using Codebase;
using System;
using System.Linq;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    internal enum SlashContext {
        Neutral,
        VictimHasPuck,
        VictimHasPuckFromBehind,
    }

    internal struct SlashResult {
        internal bool IsSlash;
        internal bool IsAxe;
        internal SlashContext Context;
        internal Player Attacker;
        internal Player Victim;
        internal string Reason;
    }

    internal static class StickOnStickDetector {
        #region Tunables (TODO : move to PenaltyConfig once values are settled)
        private const float MIN_ATTACKER_SWING_SPEED = 29f;   // Peak tip speed of the attacker's stick.
        private const float MAX_VICTIM_SWING_SPEED = 15f;     // Above this on a blade hit = mutual stick battle.
        private const float AGGRESSOR_RATIO = 1.9f;              // Attacker must be at least this many times faster than the victim stick.
        private const float MIN_APPROACH_DOT = 0.55f;          // How head-on the strike is (abs of dot between relative velocity and contact normal).
        private const float SWING_WINDOW = 0.15f;              // Seconds of history used for peak speed / height.

        private const float AXE_MIN_DROP = 1f;               // Vertical drop (in ice-relative units) between peak height and contact.
        private const float AXE_MIN_DOWN_RATIO = 0.6f;         // -v.y / |v| at the fastest sample.

        private const float BEHIND_DOT = -0.3f;                // dot(victimFacing, toAttacker) below this = attacker is behind.
        private const float FACING_MIN_SPEED = 1f;             // Under this speed, use transform.forward as facing.

        private const int PUCK_PLAY_EXEMPTION_MS = 150;        // Attacker touched the puck this recently = stick check, not slash.
        private const int VICTIM_PUCK_RECENT_MS = 250;         // Victim touched the puck this recently = treated as having the puck.

        private const float SCRUM_RADIUS = 2f;
        private const int SCRUM_PLAYER_COUNT = 2;              // Other players (besides attacker/victim) near the contact.
        private const float SCRUM_SPEED_MULTIPLIER = 1.5f;     // In a scrum, swing must be this much harder than the minimum.

        private const float LIFT_MIN_UP_RATIO = 0.08f;
        private const float LIFT_MAX_DOWN_RATIO = -0.08f;

        private const int PAIR_COOLDOWN_MS = 500;
        private const float SAMPLE_HISTORY = 0.3f;

        private const float CONTACT_GAP = 0.15f;   // Contacts closer than this belong to the same session.
        private const float STUCK_AFTER = 0.2f;    // A session older than this = hooked/stuck, not a swing.
        #endregion

        private struct Sample {
            internal Vector3 TipPos;
            internal Vector3 RelPos;
            internal float Time;
        }

        private struct ContactSession {
            internal float Start;
            internal float Last;
        }

        private static readonly LockDictionary<string, ContactSession> _contacts = new LockDictionary<string, ContactSession>();
        private static readonly LockDictionary<Stick, LockList<Sample>> _samples = new LockDictionary<Stick, LockList<Sample>>();
        private static readonly LockDictionary<string, DateTime> _pairCooldown = new LockDictionary<string, DateTime>();

        #region Tracking
        /// <summary>
        /// Call every frame on the server (from PhysicsManager_Update_Patch)
        /// </summary>
        internal static void RecordAll() {
            foreach (Player player in PlayerManager.Instance.GetSpawnedPlayers()) {
                if (!Codebase.PlayerFunc.IsPlayerPlaying(player) || !player.Stick || !player.PlayerBody)
                    continue;

                Record(player.Stick, GetTipPosition(player.Stick), player.PlayerBody);
            }

            foreach (Stick dead in _samples.Keys.Where(x => !x).ToList())
                _samples.Remove(dead);
        }

        /// <summary>
        /// Call on faceoff / goal / intermission, along with the other Reset functions.
        /// </summary>
        internal static void Reset() {
            _samples.Clear();
            _pairCooldown.Clear();
            _contacts.Clear();
        }

        // TODO : Replace with the real blade tip point if you have it. The Rigidbody position is only a placeholder.
        private static Vector3 GetTipPosition(Stick stick) {
            return stick.Rigidbody.position;
        }

        private static void Record(Stick stick, Vector3 tipPos, PlayerBody body) {
            if (!_samples.TryGetValue(stick, out LockList<Sample> list)) {
                list = new LockList<Sample>();
                _samples[stick] = list;
            }

            float now = Time.time;
            list.Add(new Sample {
                TipPos = tipPos,
                RelPos = body.transform.InverseTransformPoint(tipPos),
                Time = now,
            });
            list.RemoveAll(s => now - s.Time > SAMPLE_HISTORY);
        }

        private static (float PeakSpeed, float PeakY, float DownRatio, float RecentUpRatio) Analyze(Stick stick, float window) {
            if (!stick || !_samples.TryGetValue(stick, out LockList<Sample> list) || list.Count < 2)
                return (0f, float.MinValue, 0f, 0f);

            list = new LockList<Sample>(list);

            float now = Time.time;
            float peakSpeed = 0f, peakY = float.MinValue, downRatio = 0f;
            Vector3 recentDelta = Vector3.zero;
            float recentTime = 0f;

            for (int i = 1; i < list.Count; i++) {
                if (now - list[i].Time > window)
                    continue;

                float dt = Mathf.Max(list[i].Time - list[i - 1].Time, 0.0001f);
                Vector3 v = (list[i].RelPos - list[i - 1].RelPos) / dt;
                float speed = v.magnitude;

                if (speed > peakSpeed) {
                    peakSpeed = speed;
                    downRatio = speed > 0f ? -v.y / speed : 0f;
                }

                peakY = Mathf.Max(peakY, list[i].TipPos.y);

                // Last ~60 ms of motion, i.e. what the stick was doing at contact.
                if (now - list[i].Time <= 0.06f) {
                    recentDelta += list[i].RelPos - list[i - 1].RelPos;
                    recentTime += dt;
                }
            }

            float recentUp = 0f;
            if (recentTime > 0f && recentDelta.sqrMagnitude > 0.0001f)
                recentUp = recentDelta.normalized.y; // +1 = straight up, -1 = straight down

            return (peakSpeed, peakY, downRatio, recentUp);
        }
        #endregion

        #region Decision
        internal static float GetPeakSwingSpeed(Stick stick) => Analyze(stick, SWING_WINDOW).PeakSpeed;

        /// <summary>
        /// Call from the stick's OnCollisionEnter patch, once the blade/shaft detection is done.
        /// Safe to call from both sticks of the same collision (pair cooldown prevents double calls).
        /// </summary>
        /// <param name="selfStick">Stick, the stick that received OnCollisionEnter.</param>
        /// <param name="c">Collision, the collision.</param>
        internal static SlashResult Evaluate(Stick selfStick, Collision c) {
            SlashResult result = new SlashResult {
                IsSlash = false,
                Context = SlashContext.Neutral,
            };

            try {
                if (!selfStick || !selfStick.Player || c.contactCount == 0)
                    return Fail(result, "Invalid stick/contact");

                // Who is hit ? Another stick, or an opposing player's body.
                Stick otherStick = c.GetContact(0).otherCollider.GetComponentInParent<Stick>();
                //PlayerBody otherBody = otherStick ? null : SystemFunc.GetPlayerBody(c.gameObject);

                Stick attackerStick;
                Stick victimStick;
                Player victimPlayer;

                (float PeakSpeed, float PeakY, float DownRatio, float RecentUpRatio) attackerAnalyze, victimAnalyze;

                if (otherStick && otherStick.Player) {
                    // Aggressor = the stick with the highest recent swing speed.
                    var self = Analyze(selfStick, SWING_WINDOW);
                    var other = Analyze(otherStick, SWING_WINDOW);

                    if (self.PeakSpeed >= other.PeakSpeed) {
                        attackerStick = selfStick;
                        attackerAnalyze = self;

                        victimStick = otherStick;
                        victimAnalyze = other;
                    }
                    else {
                        attackerStick = otherStick;
                        attackerAnalyze = other;

                        victimStick = selfStick;
                        victimAnalyze = self;
                    }
                    victimPlayer = victimStick.Player;
                }
                /*else if (otherBody && otherBody.Player) {
                    attackerStick = selfStick;
                    victimStick = null;
                    victimPlayer = otherBody.Player;
                }*/
                else
                    return Fail(result, "Target is not a stick");

                if (!victimStick)
                    return Fail(result, "No victim stick");

                Player attacker = attackerStick.Player;
                result.Attacker = attacker;
                result.Victim = victimPlayer;

                if (!Codebase.PlayerFunc.IsPlayerPlaying(attacker) || !Codebase.PlayerFunc.IsPlayerPlaying(victimPlayer))
                    return Fail(result, "Player not playing");

                if (attacker.Team == victimPlayer.Team)
                    return Fail(result, $"Same team ({attacker.Username.Value} {victimPlayer.Username.Value})");

                if (Codebase.PlayerFunc.IsGoalie(attacker))
                    return Fail(result, "Goalie attacker");

                if (Codebase.PlayerFunc.IsGoalie(victimPlayer))
                    return Fail(result, "Goalie victim");

                if (!victimPlayer.PlayerBody.IsUpright)
                    return Fail(result, "Victim is on the ground");

                string attackerId = attacker.SteamId.Value.ToString();
                string victimId = victimPlayer.SteamId.Value.ToString();

                string contactKey = string.CompareOrdinal(attackerId, victimId) < 0 ? attackerId + "|" + victimId : victimId + "|" + attackerId;
                float t = Time.time;

                if (!_contacts.TryGetValue(contactKey, out ContactSession session) || t - session.Last > CONTACT_GAP)
                    session = new ContactSession { Start = t, Last = t };
                else
                    session.Last = t;
                _contacts[contactKey] = session;

                if (t - session.Start > STUCK_AFTER)
                    return Fail(result, $"Stuck contact ({t - session.Start:F2}s)");

                // Cooldown, so one swing is one call.
                string pairKey = attackerId + ">" + victimId;
                DateTime now = DateTime.UtcNow;
                if (_pairCooldown.TryGetValue(pairKey, out DateTime lastCall) && (now - lastCall).TotalMilliseconds < PAIR_COOLDOWN_MS)
                    return Fail(result, "Pair cooldown");

                // Swing data.
                float victimSpeed = victimAnalyze.PeakSpeed;

                Vector3 contactPoint = c.GetContact(0).point;
                float contactY = contactPoint.y - Ruleset.ArenaOffsetY;
                float peakY = attackerAnalyze.PeakY - Ruleset.ArenaOffsetY;
                float drop = peakY - contactY;

                // Axe : high tip, big vertical drop, mostly downward swing, on the shaft (or on a player).
                float downAtContact = -attackerAnalyze.RecentUpRatio;   // +1 = straight down at contact
                float downScore = Mathf.Max(attackerAnalyze.DownRatio, downAtContact);
                bool isAxe = drop > AXE_MIN_DROP && downScore > AXE_MIN_DOWN_RATIO;
                result.IsAxe = isAxe;

                // Context : puck and position of the attacker relative to the victim.
                string carrierId = Ruleset.GetPuckCarrierSteamId(false);
                bool victimHasPuck = carrierId == victimId || Ruleset.PlayerTouchedPuckRecently(victimId, VICTIM_PUCK_RECENT_MS);

                bool fromBehind = false;
                float behind = 0f;
                PlayerBody victimBody = victimPlayer.PlayerBody;
                PlayerBody attackerBody = attacker.PlayerBody;
                if (victimBody && attackerBody) {
                    Vector3 toAttacker = attackerBody.transform.position - victimBody.transform.position;
                    toAttacker.y = 0f; toAttacker.Normalize();

                    Vector3 fwd = victimBody.Rigidbody.rotation * Vector3.forward;
                    fwd.y = 0f; fwd.Normalize();
                    float behindFwd = Vector3.Dot(fwd, toAttacker);

                    Vector3 vel = victimBody.Rigidbody.linearVelocity; vel.y = 0f;
                    float bodySpeed = vel.magnitude;
                    float behindVel = bodySpeed >= FACING_MIN_SPEED ? Vector3.Dot(vel.normalized, toAttacker) : behindFwd;

                    behind = Mathf.Max(behindFwd, behindVel);   // behind only if BOTH say so
                    fromBehind = behind < BEHIND_DOT;
                }

                SlashContext ctx = victimHasPuck ? (fromBehind ? SlashContext.VictimHasPuckFromBehind : SlashContext.VictimHasPuck) : SlashContext.Neutral;
                result.Context = ctx;
                float m = GetThresholdMultiplier(ctx);

                float impulse = ImpactUtil.GetImpact(c);
                float approach = Mathf.Abs(Vector3.Dot(c.relativeVelocity.normalized, c.GetContact(0).normal));

                // Tuning log for near-misses and hits.
                if (attackerAnalyze.PeakSpeed >= MIN_ATTACKER_SWING_SPEED * m * 0.5f) {
                    Logging.LogDebug($"SLASH candidate {pairKey} ctx={ctx} axe={isAxe} up={attackerAnalyze.RecentUpRatio:F2} atkSpeed={attackerAnalyze.PeakSpeed:F2} vicSpeed={victimSpeed:F2} impulse={impulse:F2} approach={approach:F2} drop={drop:F2} down={attackerAnalyze.DownRatio:F2} behind={behind:F2}", Ruleset.ServerConfig);
                }

                // 1. Clear aggressor with enough swing speed.
                if (attackerAnalyze.PeakSpeed < MIN_ATTACKER_SWING_SPEED * m)
                    return Fail(result, $"Swing too slow (attackerAnalyze.PeakSpeed:{attackerAnalyze.PeakSpeed} < MIN_ATTACKER_SWING_SPEED:{MIN_ATTACKER_SWING_SPEED} * m:{m} ({MIN_ATTACKER_SWING_SPEED * m}))");

                // 2. Attacker playing the puck.
                if (Ruleset.PlayerTouchedPuckRecently(attackerId, PUCK_PLAY_EXEMPTION_MS))
                    return Fail(result, "Attacker playing the puck");

                if (!isAxe) {
                    // 3.1. Impact.
                    if (impulse < Ruleset.ServerConfig.Penalty.SlashingForceThreshold * m)
                        return Fail(result, $"Impact too light (impulse:{impulse} < MIN_IMPULSE:{Ruleset.ServerConfig.Penalty.SlashingForceThreshold} * m:{m} ({Ruleset.ServerConfig.Penalty.SlashingForceThreshold * m}))");

                    if (attackerAnalyze.PeakSpeed < victimSpeed * AGGRESSOR_RATIO)
                        return Fail(result, $"No clear aggressor (attackerAnalyze.PeakSpeed:{attackerAnalyze.PeakSpeed} < victimSpeed:{victimSpeed} * AGGRESSOR_RATIO:{AGGRESSOR_RATIO} ({victimSpeed * AGGRESSOR_RATIO}))");

                    // 4. Mutual blade battle.
                    if (ctx != SlashContext.VictimHasPuckFromBehind && victimSpeed > MAX_VICTIM_SWING_SPEED)
                        return Fail(result, $"Mutual blade battle (victimSpeed:{victimSpeed} > MAX_VICTIM_SWING_SPEED:{MAX_VICTIM_SWING_SPEED})");

                    // 5. Angle.
                    if (approach < MIN_APPROACH_DOT)
                        return Fail(result, $"Glancing hit (approach:{approach} < MIN_APPROACH_DOT:{MIN_APPROACH_DOT})");

                    // 6. Stick lift: attacker's blade comes up under the victim's stick.
                    if (attackerAnalyze.RecentUpRatio > LIFT_MIN_UP_RATIO || attackerAnalyze.DownRatio < LIFT_MAX_DOWN_RATIO)
                        return Fail(result, $"Stick lift (attackerAnalyze.RecentUpRatio:{attackerAnalyze.RecentUpRatio} > LIFT_MIN_UP_RATIO:{LIFT_MIN_UP_RATIO} || attackerAnalyze.DownRatio:{attackerAnalyze.DownRatio} < LIFT_MAX_DOWN_RATIO:{LIFT_MAX_DOWN_RATIO})");

                    /*// 2. Attacker playing the puck = stick check. An axe is never legal.
                    if (Ruleset.PlayerTouchedPuckRecently(attackerId, PUCK_PLAY_EXEMPTION_MS))
                        return Fail(result, "Attacker playing the puck");*/

                    // 7. Scrum.
                    if (ctx != SlashContext.VictimHasPuckFromBehind &&
                        CountPlayersNear(contactPoint, SCRUM_RADIUS, attackerId, victimId) >= SCRUM_PLAYER_COUNT &&
                        attackerAnalyze.PeakSpeed < MIN_ATTACKER_SWING_SPEED * SCRUM_SPEED_MULTIPLIER)
                        return Fail(result, "Scrum");
                }
                else {
                    // 3.2. Impact.
                    if (impulse < Ruleset.ServerConfig.Penalty.SlashingAxeForceThreshold * m)
                        return Fail(result, $"Impact too light (impulse:{impulse} < MIN_IMPULSE:{Ruleset.ServerConfig.Penalty.SlashingAxeForceThreshold} * m:{m} ({Ruleset.ServerConfig.Penalty.SlashingAxeForceThreshold * m}))");
                }

                _pairCooldown[pairKey] = now;
                result.IsSlash = true;
                result.Reason = isAxe ? "Axe" : (ctx == SlashContext.VictimHasPuck ? "Slash on puck carrier" : (ctx == SlashContext.VictimHasPuckFromBehind ? "Slash from behind on puck carrier" : "Slash"));
                return result;
            }
            catch (Exception ex) {
                Logging.LogError($"Error in {nameof(StickOnStickDetector)}.{nameof(Evaluate)}.\n{ex}", Ruleset.ServerConfig);
                return Fail(result, "Exception");
            }
        }

        private static float GetThresholdMultiplier(SlashContext ctx) {
            switch (ctx) {
                case SlashContext.VictimHasPuckFromBehind:
                    return 0.6f; // Much more sensitive.
                case SlashContext.VictimHasPuck:
                    return 0.8f;
                default:
                    return 1f;   // No puck : still caught, but needs a harder swing.
            }
        }

        private static int CountPlayersNear(Vector3 point, float radius, string excludeSteamId1, string excludeSteamId2) {
            int count = 0;
            foreach (Player p in PlayerManager.Instance.GetSpawnedPlayers()) {
                if (!Codebase.PlayerFunc.IsPlayerPlaying(p))
                    continue;

                string id = p.SteamId.Value.ToString();
                if (id == excludeSteamId1 || id == excludeSteamId2)
                    continue;

                if (SystemFunc.GetDistance(point.x, point.z, p.PlayerBody.transform.position.x, p.PlayerBody.transform.position.z) <= radius)
                    count++;
            }
            return count;
        }

        private static SlashResult Fail(SlashResult result, string reason) {
            result.IsSlash = false;
            result.Reason = reason;
            return result;
        }
        #endregion
    }
}
