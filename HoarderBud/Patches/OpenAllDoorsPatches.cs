using System.Linq;
using UnityEngine;

namespace HoarderBud.Patches
{
    internal class OpenAllDoorsPatches
    {
        public static bool enabled = true;
        public static void Load()
        {
            On.RoundManager.SetBigDoorCodes += RoundManager_SetBigDoorCodes;
        }
        

        private static void RoundManager_SetBigDoorCodes(On.RoundManager.orig_SetBigDoorCodes orig, RoundManager self, Vector3 mainEntrancePosition)
        {
            orig(self, mainEntrancePosition);
            if (!enabled) return;

            TerminalAccessibleObject[] array =
                Object.FindObjectsOfType<TerminalAccessibleObject>()
                .Where(door => door.isBigDoor).ToArray();
            for (int i = 0; i < array.Length; i++)
            {
                array[i].SetDoorOpen(open: true);
            }
        }
    }
}
