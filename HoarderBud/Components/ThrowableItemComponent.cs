using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Unity.Netcode;

namespace HoarderBud.Components
{
    internal class ThrowableItemComponent : PhysicsProp
    {
        public RaycastHit grenadeHit;
        public Ray grenadeThrowRay;
        public AnimationCurve grenadeFallCurve;
        public AnimationCurve grenadeVerticalFallCurve;
        public AnimationCurve grenadeVerticalFallCurveNoBounce;
        public bool wasThrown = false;
        public static EnemyType HoarderType = null;


        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            base.ItemActivate(used, buttonDown);
            HoarderBudPlugin.mls.LogInfo("IsHost:" + NetworkManager.Singleton.IsHost + " IsOwner: " + base.IsOwner);


            if (base.IsOwner || NetworkManager.Singleton.IsHost)
            {
                if (playerHeldBy.isInsideFactory || HoarderBudPlugin.HasLethalEscape)
                {
                    wasThrown = true;
                }

                playerHeldBy.DiscardHeldObject(placeObject: true, null, GetGrenadeThrowDestination());

            }
        }
        public override void FallWithCurve()
        {
            float magnitude = (startFallingPosition - targetFloorPosition).magnitude;
            base.transform.rotation = Quaternion.Lerp(base.transform.rotation, Quaternion.Euler(itemProperties.restingRotation.x, base.transform.eulerAngles.y, itemProperties.restingRotation.z), 14f * Time.deltaTime / magnitude);
            base.transform.localPosition = Vector3.Lerp(startFallingPosition, targetFloorPosition, grenadeFallCurve.Evaluate(fallTime));
            if (magnitude > 5f)
            {
                base.transform.localPosition = Vector3.Lerp(new Vector3(base.transform.localPosition.x, startFallingPosition.y, base.transform.localPosition.z), new Vector3(base.transform.localPosition.x, targetFloorPosition.y, base.transform.localPosition.z), grenadeVerticalFallCurveNoBounce.Evaluate(fallTime));
            }
            else
            {
                base.transform.localPosition = Vector3.Lerp(new Vector3(base.transform.localPosition.x, startFallingPosition.y, base.transform.localPosition.z), new Vector3(base.transform.localPosition.x, targetFloorPosition.y, base.transform.localPosition.z), grenadeVerticalFallCurve.Evaluate(fallTime));
            }
            fallTime += Mathf.Abs(Time.deltaTime * 12f / magnitude);

            if (fallTime > 1 && wasThrown)
            {
                HoarderBudPlugin.mls.LogInfo("Stopped rolling");
                if (NetworkManager.Singleton.IsHost)
                {
                    SpawnBug();
                }
                Destroy(this.gameObject);
            }
        }

        public Vector3 GetGrenadeThrowDestination()
        {
            Vector3 position = base.transform.position;
            Debug.DrawRay(playerHeldBy.gameplayCamera.transform.position, playerHeldBy.gameplayCamera.transform.forward, Color.yellow, 15f);
            grenadeThrowRay = new Ray(playerHeldBy.gameplayCamera.transform.position, playerHeldBy.gameplayCamera.transform.forward);
            position = ((!Physics.Raycast(grenadeThrowRay, out grenadeHit, 12f, StartOfRound.Instance.collidersAndRoomMaskAndDefault)) ? grenadeThrowRay.GetPoint(10f) : grenadeThrowRay.GetPoint(grenadeHit.distance - 0.05f));
            Debug.DrawRay(position, Vector3.down, Color.blue, 15f);
            grenadeThrowRay = new Ray(position, Vector3.down);
            if (Physics.Raycast(grenadeThrowRay, out grenadeHit, 30f, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
            {
                return grenadeHit.point + Vector3.up * 0.05f;
            }
            return grenadeThrowRay.GetPoint(30f);
        }

        private void SpawnBug()
        {
            if(HoarderType == null)
            {
                HoarderBudPlugin.mls.LogError("BUG IS NOT PRESENT");
                return;
            }

            RoundManager.Instance.SpawnEnemyGameObject(base.transform.position, 0, -1, HoarderType);
        }

    }
}
