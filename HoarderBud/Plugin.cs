using BepInEx;
using BepInEx.Logging;
using HoarderBud.Patches;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Collections;
using Unity.Netcode;

namespace HoarderBud
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class HoarderBudPlugin : BaseUnityPlugin
    {
        //private readonly Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        public static ManualLogSource mls;

        internal static CustomMessagingManager MessageManager => NetworkManager.Singleton.CustomMessagingManager;
        internal static bool IsClient => NetworkManager.Singleton.IsClient;
        internal static bool IsHost => NetworkManager.Singleton.IsHost;
        internal static bool IsSynced = false;

        private void Awake()
        {
            // Plugin startup logic
            mls = BepInEx.Logging.Logger.CreateLogSource(PluginInfo.PLUGIN_GUID);

            HoarderBugPatches.Load();
            On.GameNetcodeStuff.PlayerControllerB.PerformEmote += PlayerControllerB_PerformEmote;
            On.GameNetworkManager.StartDisconnect += LoadConfigOnDisconnect;
            On.GameNetcodeStuff.PlayerControllerB.ConnectClientToPlayerObject += AttachNetworkManager;

            BoomboxPatches.Load();
            RemoveOutsideMobsPatches.Load();
            RemoveInsideMobsPatches.Load();
            OpenAllDoorsPatches.Load();
            SpawnOnlyGiftsPatches.Load();

            ApplyLocalSettings();


            mls.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        private void LoadConfigOnDisconnect(On.GameNetworkManager.orig_StartDisconnect orig, GameNetworkManager self)
        {
            orig(self);

            ApplyLocalSettings();
            IsSynced = false;
        }
        private void AttachNetworkManager(On.GameNetcodeStuff.PlayerControllerB.orig_ConnectClientToPlayerObject orig, GameNetcodeStuff.PlayerControllerB self)
        {
            orig(self);

            if (IsHost)
            {
                MessageManager.RegisterNamedMessageHandler("ModName_OnRequestConfigSync", OnRequestSync);
                IsSynced = true;

                return;
            }

            IsSynced = false;
            MessageManager.RegisterNamedMessageHandler("ModName_OnReceiveConfigSync", OnReceiveSync);
            RequestSync();
        }
        public static void RequestSync()
        {
            if (!IsClient) return;

            using FastBufferWriter stream = new(sizeof(int), Allocator.Temp);
            MessageManager.SendNamedMessage("ModName_OnRequestConfigSync", 0uL, stream);
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

                MessageManager.SendNamedMessage("ModName_OnReceiveConfigSync", clientId, stream);
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
            public bool DisableOutsideEnemies = false;
            public bool DisableInsideEnemies = true;
            public bool OpenAllDoors = true;
            public bool SpawnOnlyGifts = true;
        }

        private static byte[] SerializeToBytes()
        {
            ConfigPayload toSerialise = new()
            {
                ShouldBugsDance = BoomboxPatches.enabled,
                DanceAmplitude = BoomboxPatches.amplitude,
                DanceSpeed = BoomboxPatches.danceSpeed,
                DisableOutsideEnemies = RemoveOutsideMobsPatches.enabled,
                DisableInsideEnemies = RemoveInsideMobsPatches.enabled,
                OpenAllDoors = OpenAllDoorsPatches.enabled,
                SpawnOnlyGifts = SpawnOnlyGiftsPatches.enabled
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
            RemoveOutsideMobsPatches.enabled = cfg.DisableOutsideEnemies;
            RemoveInsideMobsPatches.enabled = cfg.DisableInsideEnemies;
            OpenAllDoorsPatches.enabled = cfg.OpenAllDoors;
            SpawnOnlyGiftsPatches.enabled = cfg.SpawnOnlyGifts;

            IsSynced = true;
            mls.LogInfo("Config: Loaded remote config");
            mls.LogInfo("Config: ShouldBugsDance - " + BoomboxPatches.enabled);
            mls.LogInfo("Config: DanceAmplitude - " + BoomboxPatches.amplitude);
            mls.LogInfo("Config: DanceSpeed - " + BoomboxPatches.danceSpeed);
            mls.LogInfo("Config: DisableOutsideEnemies - " + RemoveOutsideMobsPatches.enabled);
            mls.LogInfo("Config: DisableInsideEnemies - " + RemoveInsideMobsPatches.enabled);
            mls.LogInfo("Config: OpenAllDoors - " + OpenAllDoorsPatches.enabled);
            mls.LogInfo("Config: SpawnOnlyGifts - " + SpawnOnlyGiftsPatches.enabled);
        }

        private void ApplyLocalSettings()
        {
            BoomboxPatches.enabled = Config.Bind<bool>("Boombox", "ShouldBugsDance", true, "Should hoarder bugs dance when music is playing?").Value;
            BoomboxPatches.amplitude = Config.Bind<float>("Boombox", "DanceAmplitude", 0.7f, "Dance amplitude").Value;
            BoomboxPatches.danceSpeed = Config.Bind<float>("Boombox", "DanceSpeed", 4f, "Dance speed").Value;
            RemoveOutsideMobsPatches.enabled = Config.Bind<bool>("General", "DisableOutsideEnemies", false, "Disables all outside enemies").Value;
            RemoveInsideMobsPatches.enabled = Config.Bind<bool>("General", "DisableInsideEnemies", true, "Disables all inside enemies except for Hoarder Buddy").Value;
            OpenAllDoorsPatches.enabled = Config.Bind<bool>("General", "OpenAllDoors", true, "Start game with all the inside doors open").Value;
            SpawnOnlyGiftsPatches.enabled = Config.Bind<bool>("General", "SpawnOnlyGifts", true, "All scrap spawned inside is ").Value;

            mls.LogInfo("Config: Loaded local config");
            mls.LogInfo("Config: ShouldBugsDance - " + BoomboxPatches.enabled);
            mls.LogInfo("Config: DanceAmplitude - " + BoomboxPatches.amplitude);
            mls.LogInfo("Config: DanceSpeed - " + BoomboxPatches.danceSpeed);
            mls.LogInfo("Config: DisableOutsideEnemies - " + RemoveOutsideMobsPatches.enabled);
            mls.LogInfo("Config: DisableInsideEnemies - " + RemoveInsideMobsPatches.enabled);
            mls.LogInfo("Config: OpenAllDoors - " + OpenAllDoorsPatches.enabled);
            mls.LogInfo("Config: SpawnOnlyGifts - " + SpawnOnlyGiftsPatches.enabled);
        }


        private static void PlayerControllerB_PerformEmote(On.GameNetcodeStuff.PlayerControllerB.orig_PerformEmote orig, GameNetcodeStuff.PlayerControllerB self, UnityEngine.InputSystem.InputAction.CallbackContext context, int emoteID)
        {
            mls.LogDebug("Player pos: " + self.transform.position + "|" + self.serverPlayerPosition);
            orig(self, context, emoteID);
        }

    }
}