using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
// using System.Reflection;
using UnityEngine;


namespace AutoSails {

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class AutoSailsMain : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        internal static ConfigEntry<KeyboardShortcut> hoistSails;
        internal static ConfigEntry<KeyboardShortcut> trimSails;

        public void Awake()
        {
            // Plugin startup logic
            Logger = base.Logger;
            hoistSails = Config.Bind("Hotkeys", "Hoist Sails Key", new KeyboardShortcut(KeyCode.I));
            trimSails = Config.Bind("Hotkeys", "Trim Sails Key", new KeyboardShortcut(KeyCode.J));

            //PATCHES INFO
            Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

        }
    }
}
