using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HoarderBud.Patches
{
    class HoarderMusicFlag : MonoBehaviour
    {
        public bool canHearMusic = false;
    }

    internal class BoomboxPatches
    {
        public static bool enabled = true;

        private static readonly float startingY = 1.5863f;
        private static float t = 0f;
        private static float direction = 1;
        public static float amplitude = 0.7f;
        public static float danceSpeed = 4f;

        public static void Apply()
        {
            On.HoarderBugAI.Update += UpdateDance;
            On.RoundManager.Update += RoundManager_Update;
            On.HoarderBugAI.DetectNoise += HoarderBugAI_DetectNoise;
        }

        private static void HoarderBugAI_DetectNoise(On.HoarderBugAI.orig_DetectNoise orig, HoarderBugAI self, Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot, int noiseID)
        {
            orig(self, noisePosition, noiseLoudness, timesPlayedInOneSpot, noiseID);
            if (!enabled) return;

            HoarderMusicFlag flag;
            if (!self.gameObject.TryGetComponent<HoarderMusicFlag>(out flag))
            {
                HoarderBudPlugin.mls.LogDebug("Flag is null, assigning");
                self.gameObject.AddComponent<HoarderMusicFlag>();
            }
            flag = self.gameObject.GetComponent<HoarderMusicFlag>();
            if (flag == null)
            {
                HoarderBudPlugin.mls.LogDebug("Flag is null even after assigning, shouldnt be happening");
                return;
            }

            if (noiseID == 5 && !Physics.Linecast(self.transform.position, noisePosition, StartOfRound.Instance.collidersAndRoomMask) && Vector3.Distance(self.transform.position, noisePosition) < 12f)
            {
                flag.canHearMusic = true;
                HoarderBudPlugin.mls.LogDebug("Can hear music assigned");
            }
            else
            {
                flag.canHearMusic = false;
                HoarderBudPlugin.mls.LogDebug("Cant hear music assigned");
            }

        }

        private static void RoundManager_Update(On.RoundManager.orig_Update orig, RoundManager self)
        {
            orig(self);
            if (!enabled) return;

            t += danceSpeed * Time.deltaTime * direction;
            if (t > 1f)
            {
                direction *= -1;
                t = 1f;
            }
            else if (t < 0)
            {
                direction *= -1;
                t = 0f;
            }
        }
        private static void UpdateDance(On.HoarderBugAI.orig_Update orig, HoarderBugAI self)
        {
            orig(self);
            if (!enabled) return;

            Transform chest = self.gameObject.transform.Find("HoarderBugModel").Find("AnimContainer").Find("Armature").Find("Abdomen").Find("Chest");
            
            HoarderMusicFlag flag = self.gameObject.GetComponent<HoarderMusicFlag>();

            bool canHearMusic = (flag != null && flag.canHearMusic);
            chest.localPosition = new Vector3(chest.localPosition.x, canHearMusic ? Mathf.Lerp(startingY - amplitude, startingY + amplitude, t) : startingY, chest.localPosition.z);
            
        }
    }
}
