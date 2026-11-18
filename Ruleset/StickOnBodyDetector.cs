using Codebase;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    internal struct StickBodyResult {
        internal bool IsHooking;
        internal bool IsHighStick;
        internal Player Attacker;
        internal Player Victim;
        internal string Reason;
    }

    /// <summary>
    /// Detects stick-on-body penalties : hooking (holding / slowing a player with the stick)
    /// and high stick (hard hits to the face, 2 in a short span).
    /// </summary>
    internal static class StickOnBodyDetector {
        #region Tunables (TODO : move to PenaltyConfig once values are settled)
        // Hooking.
        private const float HOOK_CONTACT_GAP = 0.15f;          // Contact interrupted for less than this = same session.
        private const float HOOK_HOLD_SECONDS = 1.0f;          // Stick on the body this long = hold, whatever the effect.
        private const float HOOK_EFFECT_SECONDS = 0.35f;       // Minimum contact before the slowdown rule can trigger.
        private const float HOOK_MIN_VICTIM_SPEED = 3f;        // Victim must have been moving at least this fast during the session.
        private const float HOOK_SLOWDOWN_RATIO = 0.35f;       // Victim lost this fraction of his best speed during the session.
        private const float HOOK_COOLDOWN = 3f;                // Seconds between 2 hooking calls on the same pair.
        private const int HOOK_OTHER_COLLISION_EXEMPTION_MS = 300; // Victim was hit by another player recently = not the stick's fault.
        private const int HOOK_PUCK_PLAY_EXEMPTION_MS = 150;   // Attacker touched the puck this recently = contesting the puck.

        private const float HOOK_STICK_SHARE = 0.5f;          // stick must explain at least this share of the speed loss
        private const float HOOK_MIN_OBSTRUCTION_DV = 0.8f;   // m/s the stick actually took away
        private const float HOOK_MIN_HOLD_DV = 1.0f;          // a hold needs the stick to really press on the body

        // High stick (on a player).
        private const float HEAD_HEIGHT = Codebase.Constants.SHOULDERS_HEIGHT; // Contact above this, relative to the victim's body origin, = head/face. TODO : check the player's real height.
        private const float HIGH_STICK_MIN_SWING_SPEED = 5f;   // Relative swing speed (SlashDetector tracker) for a hit to be "hard".
        private const float HIGH_STICK_HIT_GAP = 0.25f;        // Two contacts closer than this are the same swing.
        private const float HIGH_STICK_WINDOW = 5f;            // Seconds : 2 hard hits inside this = penalty.
        private const int HIGH_STICK_HITS_NEEDED = 2;
        private const float HIGH_STICK_COOLDOWN = 3f;
        #endregion

        private class HookSession {
            internal float Start;
            internal float Last;
            internal float MaxVictimSpeed;
            internal bool Called;

            internal float ObstructionDv;  // speed the stick took away from the victim (m/s)
            internal float PressDv;        // total push of the stick on the victim, any direction (m/s)
        }

        private static readonly LockDictionary<string, HookSession> _hooks = new LockDictionary<string, HookSession>();
        private static readonly LockDictionary<string, float> _hookCooldown = new LockDictionary<string, float>();
        private static readonly LockDictionary<string, LockList<float>> _highStickHits = new LockDictionary<string, LockList<float>>();
        private static readonly LockDictionary<string, float> _highStickCooldown = new LockDictionary<string, float>();

        /// <summary>
        /// Call on faceoff / goal / intermission, along with the other Reset functions.
        /// </summary>
        internal static void Reset() {
            _hooks.Clear();
            _hookCooldown.Clear();
            _highStickHits.Clear();
            _highStickCooldown.Clear();
        }

        /// <summary>
        /// Call from the stick's OnCollisionEnter when the other collider is a player body.
        /// </summary>
        internal static StickBodyResult OnEnter(Stick stick, Collision c) {
            StickBodyResult result = new StickBodyResult();

            try {
                if (!Resolve(stick, c, out Player attacker, out Player victim, out PlayerBody victimBody, ref result))
                    return result;

                string attackerId = attacker.SteamId.Value.ToString();
                string victimId = victim.SteamId.Value.ToString();
                string pairKey = attackerId + ">" + victimId;
                float now = Time.time;

                // ---------- High stick ----------
                if (Ruleset.ServerConfig.Penalty.HighStick) {
                    float contactHeight = c.GetContact(0).point.y - victimBody.transform.position.y;

                    bool victimIsDown = victimBody.HasFallen.Value || victimBody.HasSlipped || victimBody.IsSlipping || victimBody.IsSideways;
                    bool onHead = contactHeight > HEAD_HEIGHT && !victimIsDown;

                    if (onHead) {
                        float swing = StickOnStickDetector.GetPeakSwingSpeed(stick);
                        float impulse = c.impulse.magnitude;
                        bool hard = swing >= HIGH_STICK_MIN_SWING_SPEED || impulse >= Ruleset.ServerConfig.Penalty.HighStickForceThreshold;

                        Logging.LogDebug($"HIGHSTICK candidate {pairKey} height={contactHeight:F2} swing={swing:F2} impulse={impulse:F2} hard={hard}", Ruleset.ServerConfig);

                        if (hard) {
                            if (!_highStickHits.TryGetValue(pairKey, out LockList<float> hits)) {
                                hits = new LockList<float>();
                                _highStickHits[pairKey] = hits;
                            }

                            hits.RemoveAll(t => now - t > HIGH_STICK_WINDOW);

                            // Same swing = same hit.
                            if (hits.Count == 0 || now - hits[hits.Count - 1] > HIGH_STICK_HIT_GAP)
                                hits.Add(now);

                            if (hits.Count >= HIGH_STICK_HITS_NEEDED &&
                                (!_highStickCooldown.TryGetValue(pairKey, out float lastCall) || now - lastCall > HIGH_STICK_COOLDOWN)) {
                                hits.Clear();
                                _highStickCooldown[pairKey] = now;

                                result.IsHighStick = true;
                                result.Reason = "2 hard hits to the head";
                                return result;
                            }
                        }
                    }
                }

                // ---------- Hooking : start/refresh the contact session ----------
                if (Ruleset.ServerConfig.Penalty.Hooking)
                    TouchHookSession(pairKey, victimBody, now);
            }
            catch (Exception ex) {
                Logging.LogError($"Error in {nameof(StickOnBodyDetector)}.{nameof(OnEnter)}.\n{ex}", Ruleset.ServerConfig);
            }

            return result;
        }

        /// <summary>
        /// Call from the stick's OnCollisionStay when the other collider is a player body.
        /// </summary>
        internal static StickBodyResult OnStay(Stick stick, Collision c) {
            StickBodyResult result = new StickBodyResult();

            try {
                if (!Ruleset.ServerConfig.Penalty.Hooking)
                    return result;

                if (!Resolve(stick, c, out Player attacker, out Player victim, out PlayerBody victimBody, ref result))
                    return result;

                string attackerId = attacker.SteamId.Value.ToString();
                string victimId = victim.SteamId.Value.ToString();
                string pairKey = attackerId + ">" + victimId;
                float now = Time.time;

                if (_hookCooldown.TryGetValue(pairKey, out float lastCall) && now - lastCall < HOOK_COOLDOWN)
                    return result;

                HookSession session = TouchHookSession(pairKey, victimBody, now);
                if (session.Called)
                    return result;

                Vector3 v = victimBody.Rigidbody.linearVelocity;
                v.y = 0f;

                float dv = c.impulse.magnitude / Mathf.Max(victimBody.Rigidbody.mass, 0.01f);
                session.PressDv += dv;

                if (v.magnitude > 0.5f) {
                    Vector3 vDir = v.normalized;
                    Vector3 pushDir = victimBody.transform.position - c.GetContact(0).point; // stick -> victim
                    pushDir.y = 0f;
                    pushDir.Normalize();

                    float against = Vector3.Dot(pushDir, -vDir); // 1 = pushing straight against his motion
                    if (against > 0f)
                        session.ObstructionDv += dv * against;
                }

                // Exemptions.
                if (Ruleset.PlayerWasCollidedRecently(victimId, HOOK_OTHER_COLLISION_EXEMPTION_MS))
                    return result;

                if (Ruleset.PlayerTouchedPuckRecently(attackerId, HOOK_PUCK_PLAY_EXEMPTION_MS))
                    return result;

                float duration = session.Last - session.Start;
                float victimSpeed = HorizontalSpeed(victimBody);

                float speedLoss = session.MaxVictimSpeed - victimSpeed;

                bool hold = duration >= HOOK_HOLD_SECONDS && session.PressDv >= HOOK_MIN_HOLD_DV;

                bool slowed = duration >= HOOK_EFFECT_SECONDS &&
                    session.MaxVictimSpeed >= HOOK_MIN_VICTIM_SPEED &&
                    victimSpeed <= session.MaxVictimSpeed * (1f - HOOK_SLOWDOWN_RATIO) &&
                    session.ObstructionDv >= HOOK_MIN_OBSTRUCTION_DV &&
                    session.ObstructionDv >= speedLoss * HOOK_STICK_SHARE;

                if (hold || slowed) {
                    session.Called = true;
                    _hookCooldown[pairKey] = now;

                    result.IsHooking = true;
                    result.Reason = hold ? $"Stick held on body for {duration:F2}s" : $"Victim slowed {session.MaxVictimSpeed:F1} -> {victimSpeed:F1} during contact";
                    Logging.LogDebug($"HOOKING {pairKey} {result.Reason}", Ruleset.ServerConfig);
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in {nameof(StickOnBodyDetector)}.{nameof(OnStay)}.\n{ex}", Ruleset.ServerConfig);
            }

            return result;
        }

        private static HookSession TouchHookSession(string pairKey, PlayerBody victimBody, float now) {
            float victimSpeed = HorizontalSpeed(victimBody);

            if (!_hooks.TryGetValue(pairKey, out HookSession session) || now - session.Last > HOOK_CONTACT_GAP) {
                session = new HookSession { Start = now, Last = now, MaxVictimSpeed = victimSpeed, Called = false };
                _hooks[pairKey] = session;
                return session;
            }

            session.Last = now;
            session.MaxVictimSpeed = Mathf.Max(session.MaxVictimSpeed, victimSpeed);
            return session;
        }

        private static float HorizontalSpeed(PlayerBody body) {
            Vector3 v = body.Rigidbody.linearVelocity;
            v.y = 0f;
            return v.magnitude;
        }

        private static bool Resolve(Stick stick, Collision c, out Player attacker, out Player victim, out PlayerBody victimBody, ref StickBodyResult result) {
            attacker = null;
            victim = null;
            victimBody = null;

            if (!Ruleset.Logic || Ruleset.Paused || GameManager.Instance.Phase != GamePhase.Play)
                return false;

            if (!stick || !stick.Player || c.contactCount == 0)
                return false;

            // The other collider is a stick : not our business, SlashDetector handles it.
            if (c.collider.GetComponentInParent<Stick>())
                return false;

            victimBody = c.collider.GetComponentInParent<PlayerBody>();
            if (!victimBody || !victimBody.Player)
                return false;

            attacker = stick.Player;
            victim = victimBody.Player;

            if (!Codebase.PlayerFunc.IsPlayerPlaying(attacker) || !Codebase.PlayerFunc.IsPlayerPlaying(victim))
                return false;

            if (attacker.Team == victim.Team)
                return false;

            if (Codebase.PlayerFunc.IsGoalie(attacker))
                return false;

            result.Attacker = attacker;
            result.Victim = victim;
            return true;
        }
    }
}
