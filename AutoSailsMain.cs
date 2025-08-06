using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
// using System.Reflection;
using UnityEngine;


namespace AutoSails {

    public enum ResumeHoistingBehavior
    {
        SameDirection,
        OppositeDirection
    }

    public enum DebugLoggingLevel
    {
        None,
        Hoist,
        Trim,
        State,
        All
    }

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class AutoSailsMain : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        internal static ConfigEntry<KeyboardShortcut> hoistSails;
        internal static ConfigEntry<KeyboardShortcut> trimSails;
        internal static ConfigEntry<bool> autoSailsUI;
        internal static ConfigEntry<bool> autoSailsAutoJibe;
        internal static ConfigEntry<ResumeHoistingBehavior> resumeHoisting;
        internal static ConfigEntry<DebugLoggingLevel> debugUI;
        internal static ConfigEntry<DebugLoggingLevel> debugLogging;
        public void Awake()
        {
            // Plugin startup logic
            Logger = base.Logger;
            hoistSails = Config.Bind("Hotkeys", "Hoist Sails Key", new KeyboardShortcut(KeyCode.I));
            trimSails = Config.Bind("Hotkeys", "Trim Sails Key", new KeyboardShortcut(KeyCode.J));
            autoSailsUI = Config.Bind("UI", "autoSailsUI", false, "Enables or disables the AutoSails UI. Requires restarting the game.");
            autoSailsAutoJibe = Config.Bind("Feature", "autoSailsAutoJibe", true, "Enables or disables the automatic jibing. Automatic jibing makes it hard to sail on a run and might not work on all ships. Requires restarting the game.");
            resumeHoisting = Config.Bind("Feature", "Resume Hoisting", ResumeHoistingBehavior.OppositeDirection, "Controls behavior when resuming from paused hoist. 'Same Direction' continues original movement (classic behavior), 'Opposite Direction' reverses like a garage door opener.");
            debugUI = Config.Bind("Debug", "Debug UI Level", DebugLoggingLevel.None, "Controls debug UI overlay level: None, Hoist (hoisting info), Trim (trimming info), State (state info), All (everything).");
            debugLogging = Config.Bind("Debug", "Debug File Logging Level", DebugLoggingLevel.None, "Controls debug file logging level: None, Hoist (hoisting operations), Trim (trimming operations), State (sail state changes), All (everything).");

            //PATCHES INFO
            Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

        }
    }
}
