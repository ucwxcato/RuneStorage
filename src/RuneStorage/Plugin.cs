using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn;
using Jotunn.Managers;
using Jotunn.Utils;
using System.Reflection;

namespace RuneStorage
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(Main.ModGuid)]
    // Every player must run the same RuneStorage build: deposit/withdraw/craft RPCs and the Core inventory
    // layout change between builds, so a mismatched client is refused at connection time.
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.runestorage.valheim";
        public const string NAME = "RuneStorage";
        public const string VERSION = "0.1.1";

        internal static ManualLogSource Log;
        internal static ConfigEntry<float> CoreRangeMeters;
        internal static ConfigEntry<bool> EnableDebugLogging;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            BindConfig();
            _harmony = new Harmony(GUID);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            PrefabManager.OnVanillaPrefabsAvailable += RegisterPrefabContent;
            GUIManager.OnCustomGUIAvailable += RuneStorageTerminalPanel.OnCustomGUIAvailable;
            Log.LogInfo($"{NAME} {VERSION} loaded; Core Container safeguards patched and waiting for verified vanilla prefabs.");
        }

        private void BindConfig()
        {
            // Admin-only entries are pushed from the server to clients by Jotunn, so the server's range is the
            // one the authoritative Core and terminal checks use on every machine.
            CoreRangeMeters = Config.Bind(
                "Storage",
                "CoreRangeMeters",
                12f,
                new ConfigDescription(
                    "Maximum distance in meters between a RuneStorage Terminal and its Core. Server-controlled.",
                    new AcceptableValueRange<float>(1f, 50f),
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            EnableDebugLogging = Config.Bind(
                "Storage",
                "EnableDebugLogging",
                false,
                "Log refused RuneStorage terminal and Core requests with their reason. Local to this machine.");
        }

        internal static void DebugLog(string message)
        {
            if (EnableDebugLogging != null && EnableDebugLogging.Value)
            {
                Log.LogInfo($"[debug] {message}");
            }
        }

        private void RegisterPrefabContent()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterPrefabContent;

            try
            {
                TerminalPiece.Register();
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Terminal registration failed: {ex}");
            }

            try
            {
                CorePiece.Register();
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Core registration failed: {ex}");
            }
        }

        private void OnDestroy()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterPrefabContent;
            GUIManager.OnCustomGUIAvailable -= RuneStorageTerminalPanel.OnCustomGUIAvailable;
            RuneStorageTerminalPanel.Shutdown();
            _harmony?.UnpatchSelf();
        }
    }
}
