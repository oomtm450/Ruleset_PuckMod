namespace oomtm450PuckMod_Sounds {
    /// <summary>
    /// Class containing some constants linked to the mod in general.
    /// </summary>
    internal static class Constants {
        /// <summary>
        /// Const string, name of the mod on the workshop.
        /// </summary>
        internal const string WORKSHOP_MOD_NAME = "Sounds";

        /// <summary>
        /// Const string, name of the mod.
        /// </summary>
        internal const string MOD_NAME = Codebase.Constants.SOUNDS_MOD_NAME;

        /// <summary>
        /// Const string, used for the communication from the server.
        /// </summary>
        internal const string FROM_SERVER_TO_CLIENT = MOD_NAME + "_server";

        /// <summary>
        /// Const string, used for the communication from the server.
        /// </summary>
        internal const string FROM_CLIENT_TO_SERVER = MOD_NAME + "_client";

        /// <summary>
        /// Const string, tag to ask the server for the startup data.
        /// </summary>
        internal const string ASK_SERVER_FOR_STARTUP_DATA = MOD_NAME + "ASKDATA";
    }
}
