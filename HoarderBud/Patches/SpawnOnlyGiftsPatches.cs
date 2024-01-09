using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using MonoMod.Cil;
using System.Reflection;

namespace HoarderBud.Patches
{
    internal class SpawnOnlyGiftsPatches
    {
        public static bool enabled = true;

        public static void Load()
        {
            On.RoundManager.SpawnScrapInLevel += UpdatePositionTypes;
            IL.RoundManager.SpawnScrapInLevel += ReplaceOriginalRandom;
        }

        private static void ReplaceOriginalRandom(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            c.GotoNext(
                x => x.MatchCall<RoundManager>("GetRandomWeightedIndex")
                );
            //c.Index += 6;

            c.Next.Operand = typeof(SpawnOnlyGiftsPatches).GetMethod("FakeWeightedRandom", BindingFlags.NonPublic | BindingFlags.Static);
        }

        static int FakeWeightedRandom(RoundManager instance, int[] weights, System.Random random)
        {
            if (enabled)
            {
                for (int i = 0; i < instance.currentLevel.spawnableScrap.Count; i++)
                {
                    var item = instance.currentLevel.spawnableScrap[i];
                    if (item.spawnableItem.itemName == "Gift")
                    {
                        return i;
                    }
                }
            }

            return instance.GetRandomWeightedIndex(weights, random);
        }

        //allow Gift to be generated natively at any SpawnPositionType group
        private static void UpdatePositionTypes(On.RoundManager.orig_SpawnScrapInLevel orig, RoundManager self)
        {
            if (enabled)
            {
                SelectableLevel level = self.currentLevel;
                RandomScrapSpawn[] source = Object.FindObjectsOfType<RandomScrapSpawn>();
                HashSet<ItemGroup> groups = new();
                foreach (var spawn in source)
                {
                    groups.Add(spawn.spawnableItems);
                }
                for (int i = 0; i < level.spawnableScrap.Count; i++)
                {
                    var item = level.spawnableScrap[i];
                    if (item.spawnableItem.itemName == "Gift")
                    {
                        level.spawnableScrap[i].spawnableItem.spawnPositionTypes = groups.ToList();
                    }
                    //"SmallItems", "TabletopItems", "GeneralItemClass"
                }
            }
            orig(self);
        }
    }
}
