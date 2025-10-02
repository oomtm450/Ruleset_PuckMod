namespace oomtm450PuckMod_SoundsPack {
    /// <summary>
    /// Class containing some constants linked to the mod in general.
    /// </summary>
    internal static class Constants {
        /// <summary>
        /// Const string, name of the mod on the workshop.
        /// </summary>
        internal const string WORKSHOP_MOD_NAME = "SoundsPack";

        /// <summary>
        /// Const string, name of the mod.
        /// </summary>
        internal const string MOD_NAME = Codebase.Constants.SOUNDSPACK_MOD_NAME;

        /// <summary>
        /// Const string, used for the communication from the server.
        /// </summary>
        internal const string FROM_SERVER_TO_CLIENT = "_server";

        /// <summary>
        /// Const string, used for the communication from the server.
        /// </summary>
        internal const string FROM_CLIENT_TO_SERVER = "_client";

        /// <summary>
        /// Const string, tag to ask the server for the startup data.
        /// </summary>
        internal const string ASK_SERVER_FOR_STARTUP_DATA = "ASKDATA";
    }
}
