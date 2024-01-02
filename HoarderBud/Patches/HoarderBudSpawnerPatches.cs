using BepInEx.Logging;
using Discord;
using HoarderBud.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using static MonoMod.Cil.RuntimeILReferenceBag.FastDelegateInvokers;

namespace HoarderBud.Patches
{

    internal class HoarderBudSpawnerPatches
    {
        public static bool enabled = true;
        private static GameObject HoarderBugModel = null;
        private static Item HoarderBugSpawnerItem;

        private static ManualLogSource mls => HoarderBudPlugin.mls;

        public static void Load()
        {
            foreach (string resource in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                mls.LogInfo("Found resource called " + resource);
            }

            AssetBundle ass = AssetBundle.LoadFromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream(Assembly.GetExecutingAssembly().GetManifestResourceNames()[0]));
            foreach (string assname in ass.GetAllAssetNames())
            {
                mls.LogInfo("Found asset " + assname);
            }

            HoarderBugSpawnerItem = ass.LoadAsset<Item>("assets/templates/bang/hoarderspawner.asset");

            if (HoarderBugSpawnerItem == null)
            {
                mls.LogError("asset is null");
            }
            LethalLib.Modules.Utilities.FixMixerGroups(HoarderBugSpawnerItem.spawnPrefab);


            On.StartOfRound.Awake += CopyOriginalAssets;
            On.GameNetworkManager.Start += GameNetworkManager_Start;
        }

        private static void GameNetworkManager_Start(On.GameNetworkManager.orig_Start orig, GameNetworkManager self)
        {
            orig(self);
            Unity.Netcode.NetworkManager.Singleton.AddNetworkPrefab(HoarderBugSpawnerItem.spawnPrefab);
        }

        public static void UpdateItem()
        {
            if (enabled) 
            {
                if (LethalLib.Modules.Items.shopItems.Any(item => item.item == HoarderBugSpawnerItem)) { 
                    return; //check if already registered
                }

                LethalLib.Modules.Items.RegisterShopItem(HoarderBugSpawnerItem, 20);
            }
            else
            {
                //check if registered
                if (LethalLib.Modules.Items.shopItems.Any(item => item.item == HoarderBugSpawnerItem))
                {
                    LethalLib.Modules.Items.RemoveShopItem(HoarderBugSpawnerItem);
                }
            }
        }

        private static void CopyOriginalAssets(On.StartOfRound.orig_Awake orig, StartOfRound startOfRound)
        {
            bool foundPickles = false;
            bool foundStun = false;
            foreach (Item item in startOfRound.allItemsList.itemsList)
            {
                mls.LogDebug("Item name: " + item.itemName);
                if(item.itemName == "Jar of pickles" && !foundPickles)
                {
                    HoarderBugSpawnerItem.dropSFX = item.dropSFX;
                    HoarderBugSpawnerItem.grabSFX = item.grabSFX;
                    HoarderBugSpawnerItem.pocketSFX = item.pocketSFX;
                    HoarderBugSpawnerItem.itemIcon = item.itemIcon;
                    foundPickles = true;
                }
                if(item.itemName == "Stun grenade" && !foundStun)
                {
                    HoarderBugSpawnerItem.throwSFX = item.throwSFX;
                    ThrowableItemComponent throwable = HoarderBugSpawnerItem.spawnPrefab.GetComponent<ThrowableItemComponent>();

                    StunGrenadeItem stun = item.spawnPrefab.GetComponent<StunGrenadeItem>();
                    mls.LogInfo("Throwable is null? " + (throwable == null) + "Stun is null? " + (stun == null));
                    throwable.grenadeHit = stun.grenadeHit;
                    throwable.grenadeThrowRay = stun.grenadeThrowRay;
                    throwable.grenadeFallCurve = stun.grenadeFallCurve;
                    throwable.grenadeVerticalFallCurve = stun.grenadeVerticalFallCurve;
                    throwable.grenadeVerticalFallCurveNoBounce = stun.grenadeVerticalFallCurveNoBounce;
                    foundStun = true;
                }
            }

            foreach (SelectableLevel level in startOfRound.levels)
            {
                if (HoarderBugModel != null) break;

                foreach(var enemy in level.Enemies)
                {
                    if (enemy.enemyType.name == "HoarderBug")
                    {
                        HoarderBugModel = enemy.enemyType.enemyPrefab.transform.Find("HoarderBugModel").gameObject;

                        if(ThrowableItemComponent.HoarderType == null)
                        {
                            ThrowableItemComponent.HoarderType = enemy.enemyType;
                        }

                        if (HoarderBugModel == null)
                        {
                            HoarderBudPlugin.mls.LogError("model not found in bug");
                        }
                        else
                        {
                            GameObject go = GameObject.Instantiate(HoarderBugModel);
                            go.transform.parent = HoarderBugSpawnerItem.spawnPrefab.transform;
                            go.transform.localScale = new Vector3(0.025f, 0.025f, 0.025f);
                            go.transform.localPosition = new Vector3(go.transform.localPosition.x, go.transform.localPosition.y + 0.1f, go.transform.localPosition.z);
                            for(int i = 0; i < go.transform.childCount; i++)
                            {
                                try
                                {
                                    GameObject.Destroy(go.transform.GetChild(i).gameObject.GetComponent<EnemyAICollisionDetect>());
                                }
                                catch (Exception) { }
                            }

                            GameObject.Destroy(go.GetComponent<EnemyAICollisionDetect>());
                        }
                        break;
                    }
                }
            }
            orig(startOfRound);
        }
    }
}
