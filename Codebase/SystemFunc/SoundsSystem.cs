namespace Codebase {
    internal class SoundsSystem {
        #region Constants
        internal const string LOAD_SOUNDS = "loadsounds";
        internal const string PLAY_SOUND = "playsound";
        internal const string STOP_SOUND = "stopsound";

        internal const string LOAD_EXTRA_SOUNDS = "loadextrasounds";

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

        #region Methods/Functions
        internal static string FormatSoundStrForCommunication(string sound) {
            return sound + $";{new System.Random().Next(0, 100000)}";
        }
        #endregion
    }
}
