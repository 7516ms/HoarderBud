using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HoarderBud.Patches
{
    internal class HoarderBugPatches
    {

        public static Vector3 properNest = new Vector3(-12.17f, -219.56f, 65.56f);
        public static Dictionary<int, Vector3> nests = new();

        public static bool enabled = true;

        public static void Load()
        {
            On.RoundManager.LoadNewLevelWait += RoundManager_LoadNewLevelWait;
            On.RoundManager.SetBigDoorCodes += RoundManager_SetBigDoorCodes;
            On.HoarderBugAI.SyncNestPositionServerRpc += ReplaceNestWithEntrance;
            On.HoarderBugAI.IsHoarderBugAngry += DontBeAngry;
        }

        private static bool DontBeAngry(On.HoarderBugAI.orig_IsHoarderBugAngry orig, HoarderBugAI self)
        {
            if (!enabled)
            {
                return orig(self);
            }

            self.angryAtPlayer = null;

            if (!self.isOutside)
            {
                self.nestPosition = nests.GetValueOrDefault(self.GetInstanceID(), properNest + new Vector3(UnityEngine.Random.Range(-2f, 2f), 0, UnityEngine.Random.Range(-2f, 2f)));
                nests[self.GetInstanceID()] = self.nestPosition;
            }

            return false;
        }

        private static void ReplaceNestWithEntrance(On.HoarderBugAI.orig_SyncNestPositionServerRpc orig, HoarderBugAI self, Vector3 newNestPosition)
        {
            if (enabled)
            {
                HoarderBudPlugin.mls.LogDebug("pre synced nest " + properNest);
                if (!self.isOutside)
                {
                    self.nestPosition = properNest;
                }
            }
            orig(self, newNestPosition);
        }

        private static void RoundManager_SetBigDoorCodes(On.RoundManager.orig_SetBigDoorCodes orig, RoundManager self, Vector3 mainEntrancePosition)
        {
            orig(self, mainEntrancePosition);
            if (!enabled) return;

            HoarderBudPlugin.mls.LogDebug("SetBigDoorCodes: expected main entrance: " + mainEntrancePosition);
            properNest = mainEntrancePosition + new Vector3(5.00f, -1.40f, 0.00f);
        }

        private static IEnumerator RoundManager_LoadNewLevelWait(On.RoundManager.orig_LoadNewLevelWait orig, RoundManager self, int randomSeed)
        {
            //its fine to not check for enabled for this one
            nests = new();
            return orig(self, randomSeed);
        }
    }
}
