using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn;
using Jotunn.Managers;
using System.Reflection;

namespace RuneStorage
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(Main.ModGuid)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.runestorage.valheim";
        public const string NAME = "RuneStorage";
        public const string VERSION = "0.1.10";

        internal static ManualLogSource Log;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony(GUID);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            PrefabManager.OnVanillaPrefabsAvailable += RegisterPrefabContent;
            GUIManager.OnCustomGUIAvailable += RuneStorageTerminalPanel.OnCustomGUIAvailable;
            Log.LogInfo($"{NAME} {VERSION} loaded; Core Container safeguards patched and waiting for verified vanilla prefabs.");
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
