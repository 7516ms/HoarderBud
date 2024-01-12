using System.Linq;
using UnityEngine;

namespace HoarderBud.Patches
{
    internal class RemoveInsideMobsPatches
    {
        public static bool enabled = true;
        public static void Apply()
        {
            On.RoundManager.AssignRandomEnemyToVent += RoundManager_AssignRandomEnemyToVent;
            On.RoundManager.AdvanceHourAndSpawnNewBatchOfEnemies += RoundManager_AdvanceHourAndSpawnNewBatchOfEnemies;
        }

        private static bool RoundManager_AssignRandomEnemyToVent(On.RoundManager.orig_AssignRandomEnemyToVent orig, RoundManager self, EnemyVent vent, float spawnTime)
        {
            if (!enabled)
            {
                return orig(self, vent, spawnTime);
            }

            int bugIndex = 0; //maybe do that once instead of each vent
            bool found = false;
            for (int i = 0; i < self.currentLevel.Enemies.Count; i++)
            {
                if (self.currentLevel.Enemies[i].enemyType.name == "HoarderBug")
                {
                    bugIndex = i;
                    found = true;
                }
                else
                {
                    self.currentLevel.Enemies[i].rarity = 0;
                }
            }
            if (!found)
            {
                return orig(self, vent, spawnTime);
            }

            self.currentEnemyPower += self.currentLevel.Enemies[bugIndex].enemyType.PowerLevel;
            vent.enemyType = self.currentLevel.Enemies[bugIndex].enemyType;
            vent.enemyTypeIndex = bugIndex;
            vent.occupied = true;
            vent.spawnTime = spawnTime;

            self.currentLevel.Enemies[bugIndex].enemyType.numberSpawned++;

            return true;
        }

        private static void RoundManager_AdvanceHourAndSpawnNewBatchOfEnemies(On.RoundManager.orig_AdvanceHourAndSpawnNewBatchOfEnemies orig, RoundManager self)
        {
            if (enabled)
            {
                RoundManager.Instance.currentLevel.maxEnemyPowerCount = int.MaxValue;
                if (RoundManager.Instance.minEnemiesToSpawn < 5)
                {
                    RoundManager.Instance.minEnemiesToSpawn = 5;
                }
            }
            orig(self);
        }
    }
}
