using System.Linq;
using UnityEngine;

namespace HoarderBud.Patches
{
    internal class RemoveOutsideMobsPatches
    {
        public static bool enabled = true;
        public static void Load()
        {
            On.RoundManager.LoadNewLevelWait += RoundManager_LoadNewLevelWait;
            On.RoundManager.AdvanceHourAndSpawnNewBatchOfEnemies += RoundManager_AdvanceHourAndSpawnNewBatchOfEnemies;
        }

        private static void RoundManager_AdvanceHourAndSpawnNewBatchOfEnemies(On.RoundManager.orig_AdvanceHourAndSpawnNewBatchOfEnemies orig, RoundManager self)
        {
            if (enabled)
            {
                RoundManager.Instance.currentLevel.maxOutsideEnemyPowerCount = 0;
            }
            orig(self);
        }

        private static System.Collections.IEnumerator RoundManager_LoadNewLevelWait(On.RoundManager.orig_LoadNewLevelWait orig, RoundManager self, int randomSeed)
        {
            if (!enabled)
            {
                return orig(self, randomSeed);
            }

            var result = orig(self, randomSeed);

            if (self.currentLevel != null)
            {
                self.currentLevel.maxOutsideEnemyPowerCount = 0;
            }
            return result;
        }
    }
}
