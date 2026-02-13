using System.Reflection;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;
using System.Collections;

namespace ContentWarningOffline
{
    [BepInPlugin("photonLAN.contentwarning.photonserversettings", "CW-PhotonServerSettings", "1.0.0.0")]
    public class PhotonServerSettings : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> PluginEnabled;
        internal static ConfigEntry<string> PhotonServerAddress;
        internal static ConfigEntry<int> PhotonServerPort;
        internal static ConfigEntry<int> PhotonServerVersion;
        internal static ConfigEntry<string> PhotonAppIdRealtime;
        internal static ConfigEntry<string> PhotonAppIdVoice;
        internal static ConfigEntry<string> PhotonConnectionProtocol;
        internal static ConfigEntry<bool> PhotonAlternativeUdpPorts;

        private void Awake()
        {
            Logger.LogInfo("[CW-PhotonServerSettings] CW-PhotonServerSettings initialized!");
            Logger.LogInfo("[CW-PhotonServerSettings] Based on REPO-PhotonServerSettings by 1A3Dev");
            Logger.LogInfo("[CW-PhotonServerSettings] Also based on ContentWarningOffline by Kirigiri, made with <3 \nhttps://discord.gg/TBs8Te5nwn");

            // Initialize config entries
            PluginEnabled = Config.Bind("General", "Enable Plugin", true, new ConfigDescription("Enable or disable the plugin. If disabled, official Photon servers will be used."));
            
            PhotonAppIdRealtime = Config.Bind("Photon", "AppId Realtime", "", new ConfigDescription("Photon Realtime App ID"));
            PhotonAppIdVoice = Config.Bind("Photon", "AppId Voice", "", new ConfigDescription("Photon Voice App ID"));
            
            PhotonServerAddress = Config.Bind("Photon", "Server", "", new ConfigDescription("Photon Server Address"));
            PhotonServerPort = Config.Bind("Photon", "Server Port", 5058, new ConfigDescription("Photon Server Port", new AcceptableValueRange<int>(0, 65535)));
            PhotonServerVersion = Config.Bind("Photon", "Server Version", 5, new ConfigDescription("Photon Server Version", new AcceptableValueRange<int>(4, 5)));
            
            PhotonConnectionProtocol = Config.Bind("Photon", "Protocol", "Udp", new ConfigDescription("Photon Protocol", new AcceptableValueList<string>(System.Enum.GetNames(typeof(ConnectionProtocol)))));
            PhotonAlternativeUdpPorts = Config.Bind("Photon", "Alternative Udp Ports", true, new ConfigDescription("Photon Alternative Ports (Udp)"));

            var harmony = new Harmony("photonLAN.contentwarning.photonserversettings");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(MainMenuHandler), "ConnectToPhoton")]
        public class ConnectToPhotonPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(MainMenuHandler __instance)
            {
                // Check if plugin is disabled
                if (!PluginEnabled.Value)
                {
                    Debug.Log("[CW-PhotonServerSettings] Plugin is disabled, using official Photon servers");
                    return true; // Continue with original method
                }
                
                // Check if both server address AND AppId Realtime are blank, then enter offline mode
                bool shouldEnterOfflineMode = string.IsNullOrEmpty(PhotonServerAddress.Value) && string.IsNullOrEmpty(PhotonAppIdRealtime.Value);
                
                if (shouldEnterOfflineMode)
                {
                    PhotonNetwork.OfflineMode = true;
                    Debug.Log("[CW-PhotonServerSettings] Both server address and AppId Realtime are blank, entering offline mode");
                    return false;
                }
                
                // Apply server settings if we have a valid server address
                PhotonNetwork.PhotonServerSettings.AppSettings.Server = PhotonServerAddress.Value;
                Debug.Log($"[CW-PhotonServerSettings] Changed Server Address: {PhotonNetwork.PhotonServerSettings.AppSettings.Server}");
                
                if (PhotonServerVersion.Value == 4)
                {
                    PhotonNetwork.PhotonServerSettings.AppSettings.UseNameServer = false;
                    PhotonNetwork.NetworkingClient.SerializationProtocol = SerializationProtocol.GpBinaryV16;
                }
                
                if (PhotonServerPort.Value > 0)
                {
                    PhotonNetwork.PhotonServerSettings.AppSettings.Port = PhotonServerPort.Value;
                    Debug.Log($"[CW-PhotonServerSettings] Changed Server Port: {PhotonNetwork.PhotonServerSettings.AppSettings.Port}");
                }
                
                if (!string.IsNullOrEmpty(PhotonAppIdRealtime.Value))
                {
                    PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime = PhotonAppIdRealtime.Value;
                    Debug.Log($"[CW-PhotonServerSettings] Changed AppIdRealtime: {PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime}");
                }
                
                if (!string.IsNullOrEmpty(PhotonAppIdVoice.Value))
                {
                    PhotonNetwork.PhotonServerSettings.AppSettings.AppIdVoice = PhotonAppIdVoice.Value;
                    Debug.Log($"[CW-PhotonServerSettings] Changed AppIdVoice: {PhotonNetwork.PhotonServerSettings.AppSettings.AppIdVoice}");
                }
                
                if (System.Enum.TryParse<ConnectionProtocol>(PhotonConnectionProtocol.Value, out var protocol))
                {
                    PhotonNetwork.PhotonServerSettings.AppSettings.Protocol = protocol;
                    Debug.Log($"[CW-PhotonServerSettings] Changed Protocol: {protocol}");
                }
                else
                {
                    PhotonNetwork.PhotonServerSettings.AppSettings.Protocol = ConnectionProtocol.Udp;
                }
                
                if (PhotonAlternativeUdpPorts.Value && PhotonNetwork.PhotonServerSettings.AppSettings.Protocol == ConnectionProtocol.Udp)
                {
                    PhotonNetwork.ServerPortOverrides = PhotonPortDefinition.AlternativeUdpPorts;
                }
                else
                {
                    PhotonNetwork.ServerPortOverrides = new PhotonPortDefinition();
                }
                
                // Continue with original method to connect to online mode
                return true;
            }
        }

        [HarmonyPatch(typeof(CheckVersionHandler), "CheckVersionCoroutine")]
        public class CheckVersionPatcher
        {
            [HarmonyPrefix]
            public static bool Prefix(CheckVersionHandler __instance, ref IEnumerator __result)
            {
                // Check if plugin is disabled
                if (!PluginEnabled.Value)
                {
                    Debug.Log("[CW-PhotonServerSettings] Plugin is disabled, skipping CheckVersionCoroutine patch");
                    return true; // Continue with original method
                }
                
                __result = ForcedCoroutine(__instance);
                return false;
            }

            private static IEnumerator ForcedCoroutine(CheckVersionHandler instance)
            {
                string KirigiriResponse = "VersionOK";

                MethodInfo checkResultMethod = typeof(CheckVersionHandler)
                    .GetMethod("CheckResult", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (checkResultMethod != null)
                {
                    checkResultMethod.Invoke(instance, new object[] { KirigiriResponse });
                }
                else
                {
                    Debug.LogError("Could not find 'CheckResult' method via reflection.");
                }

                yield break;
            }
        }

        //[HarmonyPatch(typeof(MainMenuHandler), nameof(MainMenuHandler.JoinRandom))]
        //public class JoinRandomPatch
        //{
        //    [HarmonyPrefix]
        //    public static bool Prefix()
        //    {
        //        // Check if plugin is disabled
        //        if (!PluginEnabled.Value)
        //        {
        //            Debug.Log("[CW-PhotonServerSettings] Plugin is disabled, allowing JoinRandom to function normally");
        //            return true; // Continue with original method
        //        }
                
        //        Modal.Show(
        //            "<color=purple>This cannot be used in this mod</color>",
        //            "This feature is not available in the current version of the application.",
        //            new ModalOption[]
        //            {
        //        new ModalOption("OK", null)
        //            },
        //            () => { }
        //        );

        //        return false;
        //    }
        //}
    }
}
