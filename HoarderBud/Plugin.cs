using BepInEx;
using BepInEx.Logging;
using HoarderBud.Patches;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HoarderBud
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class HoarderBudPlugin : BaseUnityPlugin
    {
        public static ManualLogSource mls;

        internal static CustomMessagingManager MessageManager => NetworkManager.Singleton.CustomMessagingManager;
        internal static bool IsClient => NetworkManager.Singleton.IsClient;
        internal static bool IsHost => NetworkManager.Singleton.IsHost;
        internal static bool IsSynced = false;

        internal static GameObject LootBugModel = null;
        public static bool HasLethalEscape = false;

        private void Awake()
        {
            // Plugin startup logic
            mls = base.Logger;

            HoarderBugPatches.Apply();

            On.GameNetcodeStuff.PlayerControllerB.PerformEmote += PlayerControllerB_PerformEmote; //debug coords
            
            //config syncing
            On.GameNetworkManager.StartDisconnect += LoadConfigOnDisconnect;
            On.GameNetcodeStuff.PlayerControllerB.ConnectClientToPlayerObject += AttachNetworkManager;

            BoomboxPatches.Apply();

            RemoveOutsideMobsPatches.Apply();
            RemoveInsideMobsPatches.Apply();

            OpenAllDoorsPatches.Apply();

            HoarderBudSpawnerPatches.Apply();

            ApplyLocalSettings();

            mls.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

        }

        private void LoadConfigOnDisconnect(On.GameNetworkManager.orig_StartDisconnect orig, GameNetworkManager self)
        {
            orig(self);

            ApplyLocalSettings();
            IsSynced = false;
            MessageManager.UnregisterNamedMessageHandler(PluginInfo.PLUGIN_GUID + "_OnRequestConfigSync");
            MessageManager.UnregisterNamedMessageHandler(PluginInfo.PLUGIN_GUID + "_OnReceiveConfigSync");
        }
        private void AttachNetworkManager(On.GameNetcodeStuff.PlayerControllerB.orig_ConnectClientToPlayerObject orig, GameNetcodeStuff.PlayerControllerB self)
        {
            orig(self);

            HasLethalEscape = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("xCeezy.LethalEscape");

            if (IsHost)
            {
                MessageManager.RegisterNamedMessageHandler(PluginInfo.PLUGIN_GUID + "_OnRequestConfigSync", OnRequestSync);
                IsSynced = true;

                return;
            }

            IsSynced = false;
            MessageManager.RegisterNamedMessageHandler(PluginInfo.PLUGIN_GUID + "_OnReceiveConfigSync", OnReceiveSync);
            RequestSync();
        }
        public static void RequestSync()
        {
            if (!IsClient) return;

            using FastBufferWriter stream = new(sizeof(int), Allocator.Temp);
            MessageManager.SendNamedMessage(PluginInfo.PLUGIN_GUID + "_OnRequestConfigSync", 0uL, stream);
        }

        public static void OnRequestSync(ulong clientId, FastBufferReader _)
        {
            if (!IsHost) return;

            mls.LogInfo($"Config sync request received from client: {clientId}");

            byte[] array = SerializeToBytes();
            int value = array.Length;

            using FastBufferWriter stream = new(value + sizeof(int), Allocator.Temp);

            try
            {
                stream.WriteValueSafe(in value, default);
                stream.WriteBytesSafe(array);

                MessageManager.SendNamedMessage(PluginInfo.PLUGIN_GUID + "_OnReceiveConfigSync", clientId, stream);
            }
            catch (Exception e)
            {
                mls.LogInfo($"Error occurred syncing config with client: {clientId}\n{e}");
            }
        }
        public static void OnReceiveSync(ulong _, FastBufferReader reader)
        {
            if (!reader.TryBeginRead(sizeof(int)))
            {
                mls.LogError("Config sync error: Could not begin reading buffer.");
                return;
            }

            reader.ReadValueSafe(out int val, default);
            if (!reader.TryBeginRead(val))
            {
                mls.LogError("Config sync error: Host could not sync.");
                return;
            }

            byte[] data = new byte[val];
            reader.ReadBytesSafe(ref data, val);

            ParsePacket(data);

            mls.LogInfo("Successfully synced config with host.");
        }

        [Serializable]
        internal class ConfigPayload
        {
            public bool ShouldBugsDance = true;
            public float DanceAmplitude = 0.7f;
            public float DanceSpeed = 4f;

            public bool MakeBugsFriendly = true;
            public bool MakeBugsGatherAtMainEntrance = true;

            public bool DisableOutsideEnemies = false;
            public bool DisableInsideEnemies = true;

            public bool OpenAllDoors = true;

            public bool AddSpawnerItem = true;
            public int SpawnerItemPrice = 30;
        }

        private static byte[] SerializeToBytes()
        {
            ConfigPayload toSerialise = new()
            {
                ShouldBugsDance = BoomboxPatches.enabled,
                DanceAmplitude = BoomboxPatches.amplitude,
                DanceSpeed = BoomboxPatches.danceSpeed,

                MakeBugsFriendly = HoarderBugPatches.MakeBugsFriendly,
                MakeBugsGatherAtMainEntrance = HoarderBugPatches.MakeBugsGatherAtMainEntrance,

                DisableOutsideEnemies = RemoveOutsideMobsPatches.enabled,
                DisableInsideEnemies = RemoveInsideMobsPatches.enabled,

                OpenAllDoors = OpenAllDoorsPatches.enabled,

                AddSpawnerItem = HoarderBudSpawnerPatches.enabled,
                SpawnerItemPrice = HoarderBudSpawnerPatches.SpawnerItemPrice
            };

            BinaryFormatter bf = new();
            using MemoryStream ms = new();

            bf.Serialize(ms, toSerialise);

            return ms.ToArray();
        }
        private static void ParsePacket(byte[] data)
        {
            using var memStream = new MemoryStream();
            var binForm = new BinaryFormatter();
            memStream.Write(data, 0, data.Length);
            memStream.Seek(0, SeekOrigin.Begin);
            ConfigPayload cfg = (ConfigPayload)binForm.Deserialize(memStream);

            BoomboxPatches.enabled = cfg.ShouldBugsDance;
            BoomboxPatches.amplitude = cfg.DanceAmplitude;
            BoomboxPatches.danceSpeed = cfg.DanceSpeed;

            HoarderBugPatches.MakeBugsFriendly = cfg.MakeBugsFriendly;
            HoarderBugPatches.MakeBugsGatherAtMainEntrance = cfg.MakeBugsGatherAtMainEntrance;

            RemoveOutsideMobsPatches.enabled = cfg.DisableOutsideEnemies;
            RemoveInsideMobsPatches.enabled = cfg.DisableInsideEnemies;
            OpenAllDoorsPatches.enabled = cfg.OpenAllDoors;
            HoarderBudSpawnerPatches.enabled = cfg.AddSpawnerItem;
            HoarderBudSpawnerPatches.SpawnerItemPrice = cfg.SpawnerItemPrice;
            HoarderBudSpawnerPatches.UpdateItem();

            IsSynced = true;
            mls.LogInfo("Config: Loaded remote config");
            DumpConfig();
        }

        private void ApplyLocalSettings()
        {
            BoomboxPatches.enabled = Config.Bind<bool>("Boombox", "ShouldBugsDance", true, "Should hoarder bugs dance when music is playing?").Value;
            BoomboxPatches.amplitude = Config.Bind<float>("Boombox", "DanceAmplitude", 0.7f, "Dance amplitude").Value;
            BoomboxPatches.danceSpeed = Config.Bind<float>("Boombox", "DanceSpeed", 4f, "Dance speed").Value;

            HoarderBugPatches.MakeBugsFriendly = Config.Bind<bool>("General", "MakeBugsFriendly", true, "Makes hoarder bugs not attack you").Value;
            HoarderBugPatches.MakeBugsGatherAtMainEntrance = Config.Bind<bool>("General", "MakeBugsGatherAtMainEntrance", true, "Makes hoarder bugs nest near the main entrance").Value;
            
            RemoveOutsideMobsPatches.enabled = Config.Bind<bool>("General", "DisableOutsideEnemies", false, "Disables all outside enemies").Value;
            RemoveInsideMobsPatches.enabled = Config.Bind<bool>("General", "DisableInsideEnemies", true, "Disables all inside enemies except for Hoarder Buddy, also increases his spawnrate").Value;
            OpenAllDoorsPatches.enabled = Config.Bind<bool>("General", "OpenAllDoors", true, "Starts game with all the inside doors open").Value;
            HoarderBudSpawnerPatches.enabled = Config.Bind<bool>("General", "AddSpawnerItem", true, "Adds a throwable HoarderBug egg").Value;
            HoarderBudSpawnerPatches.SpawnerItemPrice = Config.Bind<int>("General", "SpawnerItemPrice", 30, "Throwable HoarderBug egg price").Value;
            HoarderBudSpawnerPatches.UpdateItem();


            mls.LogInfo("Config: Loaded local config");
            DumpConfig();
        }
        private static void DumpConfig()
        {
            mls.LogInfo("Config: ShouldBugsDance - " + BoomboxPatches.enabled);
            mls.LogInfo("Config: DanceAmplitude - " + BoomboxPatches.amplitude);
            mls.LogInfo("Config: DanceSpeed - " + BoomboxPatches.danceSpeed);
            mls.LogInfo("Config: MakeBugsFriendly - " + HoarderBugPatches.MakeBugsFriendly);
            mls.LogInfo("Config: MakeBugsGatherAtMainEntrance - " + HoarderBugPatches.MakeBugsGatherAtMainEntrance);
            mls.LogInfo("Config: DisableOutsideEnemies - " + RemoveOutsideMobsPatches.enabled);
            mls.LogInfo("Config: DisableInsideEnemies - " + RemoveInsideMobsPatches.enabled);
            mls.LogInfo("Config: OpenAllDoors - " + OpenAllDoorsPatches.enabled);
            mls.LogInfo("Config: AddSpawnerItem - " + HoarderBudSpawnerPatches.enabled);
            mls.LogInfo("Config: SpawnerItemPrice - " + HoarderBudSpawnerPatches.SpawnerItemPrice);
        }

        private static void PlayerControllerB_PerformEmote(On.GameNetcodeStuff.PlayerControllerB.orig_PerformEmote orig, GameNetcodeStuff.PlayerControllerB self, UnityEngine.InputSystem.InputAction.CallbackContext context, int emoteID)
        {
            mls.LogDebug("Player pos: " + self.transform.position + "|" + self.serverPlayerPosition);
            orig(self, context, emoteID);
        }

    }
}