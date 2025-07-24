using HarmonyLib;
using UnityEngine;
using SailwindModdingHelper;


namespace AutoSails
{
    public class AutoSailsControlSail : MonoBehaviour
    {
        public bool canControl = false; 
        private PurchasableBoat boat;
        private Sail sailComponent;
        private GPButtonRopeWinch ropeWinch;
        public bool hoistSails = false; // This is used to determine if the sails should be hoisted or not
        public bool trimSails = false; // This is used to determine if the sails should be trimmed or not
        public bool hoisted = false; // This is used to determine if the sails are hoisted or not

        // private string sailName;
        private float hoistingSpeed = 0.005f; // Speed at which the sails are hoisted
        private float trimmingSpeed = 0.0005f; // Speed at which the sails are hoisted
                                       // float hoistSign = -1;  // This is used to determine the direction of the hoist, depending on the sail type
                                       //TODO on some ships, the hoist Sign calculation does not work properly. Why?
        private bool reverseReefing; 
        private float trimDirection = 1f; // This is used to determine the direction of the trim
        private float oldEfficiency = 1f;
        private int i = 0;
        public void Awake()
        {
            ropeWinch = GetComponentInParent<GPButtonRopeWinch>();
            if (ropeWinch == null) return;
            GameEvents.OnPlayerInput += (_, __) =>
            {
                if (AutoSailsMain.hoistSails.Value.IsDown())
                {
                    hoistSails = !hoistSails;
                    // if (hoistSails)
                    // {
                    //     NotificationUi.instance.ShowNotification("Hoist Sails!");
                    // }
                    // else
                    // {
                    //     NotificationUi.instance.ShowNotification("Lower Sails!");
                    // }   
                }
                if (AutoSailsMain.trimSails.Value.IsDown())
                {
                    trimSails = !trimSails; 
                    // if (trimSails)
                    // {
                    //     NotificationUi.instance.ShowNotification("Start trimming the sails!");
                    // }
                    // else
                    // {
                    //     NotificationUi.instance.ShowNotification("Stop trimming the sails!");
                    // }   
                }
            };

        }
        // private int SailDegree()
        // {   //gets the sailComponent in and returns the angle wiht the boat forward direction out out

        //     if (sailTransform == null) sailTransform = sailComponent.transform;
        //     if (boatTransform == null) boatTransform = sailComponent.shipRigidbody.transform;

        //     Vector3 boatVector = boatTransform.forward;    //boat direction
        //     Vector3 sailVector = sailComponent.squareSail ? sailTransform.up : sailTransform.right; //sailComponent "direction" since squares are made differently we use the up direction for them, otherwise the -right direction (also known as left)

        //     int angle = Mathf.RoundToInt(Vector3.SignedAngle(boatVector, sailVector, Vector3.up)); //calculate the angle

        //     angle = angle > 90 ? 180 - angle : angle; //keep it in a 0° to 90° angle
        //     angle = angle < 0 ? -angle : angle; //keep it positive
        //     return angle;
        // }
        private float SailEfficiency()
        {   // Calculates the efficiency of a sailComponent trim (max is best)

            //This is the force created by the sailComponent
            // float unamplifiedForce = (float)unamplifiedForwardInfo.GetValue(sailComponent);
            float unamplifiedForce = (float)Traverse.Create(sailComponent).Field("unamplifiedForwardForce").GetValue();
            //This is the total force the wind applies to the sailComponent. This is also the maximum force forward the sailComponent can generate on the boat.
            float totalWindForce = GetTotalForce();
            float efficiency = unamplifiedForce / totalWindForce * 100f;

            return efficiency;
        }
        private float SailInefficiency()
        {   // Calculates the percentage of sideway force on a sailComponent (min is best)
            // float unamplifiedSideInfo = (float)unamplifiedSideInfo.GetValue(sailComponent);
            float unamplifiedSideInfo = (float)Traverse.Create(sailComponent).Field("unamplifiedSidewayForce").GetValue();
            //This is the total force the wind applies to the sailComponent. This is also the maximum force forward the sailComponent can generate on the boat.
            float totalWindForce = GetTotalForce();

            float inefficiency = Mathf.Abs(unamplifiedSideInfo / totalWindForce * 100f);

            return inefficiency;
        }
        private float CombinedEfficiency()
        {   //combines Efficiency and Inefficiency into one single value (max is best)
            //this is the real efficiency!
            float eff = SailEfficiency();
            if (eff <= 0f)
            {
                return eff;
            }
            float ineff = 100 - SailInefficiency();
            float comb = (3*eff + ineff) / 4f;

            return comb;
        }
        private float GetTotalForce()
        {   // avoid using reflections when possible to get totalWindForce
            float applied = sailComponent.appliedWindForce;

            if (applied == 0f)
            {   //use reflections to get totalWindForce instead
                return (float) Traverse.Create(sailComponent).Field("totalWindForce").GetValue();
            }

            return applied / sailComponent.GetCapturedForceFraction();
        }
        private void FixedUpdate()
        {
            if (ropeWinch == null) return;
            if (ropeWinch.rope is RopeControllerSailReef)
            {
                // TODO if I call this in Awake, it will not work properly.
                // Maybe because the rope is not properly initialized yet
                reverseReefing = (bool)Traverse.Create(ropeWinch.rope).Field("reverseReefing").GetValue();
            }
            
            
        }
        private void Update()
        {
            if (ropeWinch == null) return;
            if (boat == null) boat = (PurchasableBoat)Traverse.Create(ropeWinch).Field("boat").GetValue();
            // user can control the sail if player is on boat, or is currently not any boat and last boat is boat
            if (GameState.currentBoat != null)
            {
                canControl = GameState.currentBoat.IsChildOf(boat.transform) && boat.isPurchased();
            }
            else if (GameState.lastBoat != null)
            {
                canControl = GameState.lastBoat.IsChildOf(boat.transform) && boat.isPurchased();
            }
            else
            {
                canControl = false;
            }
            /*
            // TODO various field displayed on the winches for debug
            // if (ropeWinch.IsLookedAt() || ropeWinch.IsStickyClicked() || ropeWinch.IsCliked())
            // {
            // ropeWinch.description = "";
            // ropeWinch.description += $"\n GameState.currentBoat: {GameState.currentBoat}";
            // Transform boat = GetComponentInParent<PurchasableBoat>().transform;
            // PurchasableBoat boat = GetComponentInParent<PurchasableBoat>();//.transform;
            // ropeWinch.description += $"\n boat: {boat}";
            // ropeWinch.description += $"\n root: {GameState.currentBoat}";
            // ropeWinch.description += $"\n GetChild: {GameState.currentBoat.GetChild(0)}";
            // ropeWinch.description += $"\n GetChild: {GetComponentInParent<PurchasableBoat>()}"; 
            // ropeWinch.description += $"\n ropeWinch.boat: {Traverse.Create(ropeWinch).Field("boat").GetValue()}";
            // PurchasableBoat boat = (PurchasableBoat)Traverse.Create(ropeWinch).Field("boat").GetValue();
            // ropeWinch.description += $"\n ropeWinch.boat: {boat}";
            // ropeWinch.description += $"\n ropeWinch.boat.transform: {boat.transform}";
            // ropeWinch.description += $"\n GameState.currentBoat: {GameState.currentBoat}";
            // ropeWinch.description += $"\n equals?: {boat.transform == GameState.currentBoat}";

            // SaveableObject saveable = (SaveableObject)Traverse.Create(boat).Field("saveable").GetValue();
            // GameObject purchaseUI = (GameObject)Traverse.Create(boat).Field("purchaseUI").GetValue();
            // ropeWinch.description += $"\n saveable: {saveable}";
            // ropeWinch.description += $"\n purchaseUI: {purchaseUI}";
            // ropeWinch.description += $"\n GameState.currentShipyard: {GameState.currentShipyard}";
            // ropeWinch.description += $"\n GameState.currentBoat: {GameState.currentBoat}";

            // Transform cboat = GameState.currentBoat;
            // Transform boat = ((PurchasableBoat)Traverse.Create(ropeWinch).Field("boat").GetValue()).transform;
            // ropeWinch.description += $"\n equals: {cboat == boat}";
            // ropeWinch.description += $"\n name: {cboat.name} || {boat.name}";
            // ropeWinch.description += $"\n gameObject: {cboat.gameObject} || {boat.gameObject}";
            // ropeWinch.description += $"\n tag: {cboat.tag} || {boat.tag}";
            // ropeWinch.description += $"\n transform: {cboat.transform} || {boat.transform}";
            // ropeWinch.description += $"\n hideFlags: {cboat.hideFlags} || {boat.hideFlags}";
            // ropeWinch.description += $"\n GetInstanceID: {cboat.GetInstanceID()} || {boat.GetInstanceID()}";
            // ropeWinch.description += $"\n parent: {cboat.parent} || {boat.parent}";
            // ropeWinch.description += $"\n position: {cboat.position} || {boat.position}";
            // ropeWinch.description += $"\n equals: {cboat.parent == boat}";
            // ropeWinch.description += $"\n equals: {cboat.IsChildOf(boat)}";

            // PurchasableBoat boat = (PurchasableBoat)Traverse.Create(ropeWinch).Field("boat").GetValue();
            // SaveableObject saveable = (SaveableObject)Traverse.Create(boat).Field("saveable").GetValue();
            // GameObject purchaseUI = (GameObject)Traverse.Create(boat).Field("purchaseUI").GetValue();
            // ropeWinch.description += $"\n isPurchased: {boat.isPurchased()}";
            // // ropeWinch.description += $"\n purchaseUI.activeSelf: {purchaseUI.activeSelf}"; // if bought it has no pochase UI anymore
            // CleanableObject  saveCleanable = (CleanableObject) Traverse.Create(saveable).Field("saveCleanable").GetValue();
            // ropeWinch.description += $"\n saveCleanable: {saveCleanable}";
            // int sceneIndex = (int) Traverse.Create(saveable).Field("sceneIndex").GetValue();
            // ropeWinch.description += $"\n sceneIndex: {sceneIndex}";
            // bool registered = (bool) Traverse.Create(saveable).Field("registered").GetValue();
            // ropeWinch.description += $"\n registered: {registered}";
            // bool loaded = (bool) Traverse.Create(saveable).Field("loaded").GetValue();
            // ropeWinch.description += $"\n loaded: {loaded}";
            // bool extraSetting = (bool) Traverse.Create(saveable).Field("extraSetting").GetValue();
            // ropeWinch.description += $"\n extraSetting: {extraSetting}";
            // float extraValue = (float) Traverse.Create(saveable).Field("extraValue").GetValue();
            // ropeWinch.description += $"\n extraValue: {extraValue}";
            // Texture2D extraTexture = (Texture2D) Traverse.Create(saveable).Field("extraTexture").GetValue();
            // ropeWinch.description += $"\n extraTexture: {extraTexture}";
            // SaveableBoatCustomization customization = (SaveableBoatCustomization) Traverse.Create(saveable).Field("customization").GetValue();
            // ropeWinch.description += $"\n customization: {customization}";
            // BoatLocalItems localItems = (BoatLocalItems) Traverse.Create(saveable).Field("localItems").GetValue();
            // ropeWinch.description += $"\n localItems: {localItems}";
            // BoatDamage damage = (BoatDamage) Traverse.Create(saveable).Field("damage").GetValue();
            // ropeWinch.description += $"\n damage: {damage}";

            //     ropeWinch.description += $"\n currentLength: {ropeWinch.rope.currentLength}";
            //     ropeWinch.description += $"\n ropeWinch.rope: {ropeWinch.rope}";

            //     // debug for RopeControllerSailAngle
            //     ropeWinch.description += $"\n trimSails: {trimSails}";
            //     ropeWinch.description += $"\n trimSails: {trimSails}";

            //     // debug for RopeControllerSailReef
            //     // ropeWinch.description += $"\n hoistSails: {hoistSails}";
            //     // ropeWinch.description += $"\n rope.changed: {ropeWinch.rope.changed}";
            //     // ropeWinch.description += $"\n rope.reverseReefing: {(bool)Traverse.Create(ropeWinch.rope).Field("reverseReefing").GetValue()}";
            //     // ropeWinch.description += $"\n reverseReefing here: {reverseReefing}";
            //     // ropeWinch.description += $"\n rope.lastLength: {(float)Traverse.Create(ropeWinch.rope).Field("lastLength").GetValue()}";
            //     // debug for RopeControllerSailAngleSquare
            // ropeWinch.description += $"\n CombinedEfficiency: {Mathf.Round(CombinedEfficiency())}";
            // ropeWinch.description += $"\n SailEfficiency: {Mathf.Round(SailEfficiency())}";
            // ropeWinch.description += $"\n SailInefficiency: {Mathf.Round(SailInefficiency())}";
            // }
            */
            if (ropeWinch.rope == null) return;
            if (
                sailComponent == null
                &&
                (ropeWinch.rope is RopeControllerSailAngle
                || ropeWinch.rope is RopeControllerSailAngleSquare
                || ropeWinch.rope is RopeControllerSailAngleJib)
            )
            {
                sailComponent = (Sail)Traverse.Create(ropeWinch.rope).Field("sail").GetValue();
            }
            if (ropeWinch.rope is RopeControllerSailReef)
            {
                if (hoistSails && canControl)
                {
                    if (hoisted)
                    {
                        if (reverseReefing)
                        {
                            HoistUp();
                        }
                        else
                        {
                            HoistDown();
                        }
                    }
                    else
                    {
                        if (reverseReefing)
                        {
                            HoistDown();
                        }
                        else
                        {
                            HoistUp();
                        }
                    }
                }
            }
            else if (ropeWinch.rope is RopeControllerSailAngle)
            {
                // PurchasableBoat boat = (PurchasableBoat)Traverse.Create(ropeWinch).Field("boat").GetValue();
                if (trimSails && canControl)
                {
                    // only visual effect of turning winch wheel
                    Traverse.Create(ropeWinch).Field("currentInput").SetValue(-trimDirection * 5f);
                    ropeWinch.ApplyRotation();
                    // trim logic
                    if (i == 20)
                    {   //every 20 frames, we check the efficiency and trim the sailComponent
                        i = 0;
                        if (oldEfficiency > CombinedEfficiency())
                        {
                            trimDirection *= -1f; // if the efficiency is worse, reverse the trim direction
                        }
                        oldEfficiency = CombinedEfficiency();
                    }

                    if (CombinedEfficiency() == 0f)
                    {
                        // if the efficiency is 0, we need to tighten the sail
                        trimDirection = -1f;
                        i = 0;
                    }
                    if (ropeWinch.rope.currentLength >= 1f)
                    {
                        ropeWinch.rope.currentLength -= trimmingSpeed;

                    }
                    else if (ropeWinch.rope.currentLength <= 0f)
                    {
                        ropeWinch.rope.currentLength += trimmingSpeed;
                    }
                    else
                    {

                        ropeWinch.rope.currentLength += trimDirection * trimmingSpeed;
                    }
                    ropeWinch.rope.changed = true; // mark the rope as changed such that changes are applied
                    i++;
                }
            }
            else if (ropeWinch.rope is RopeControllerSailAngleSquare)
            {
                if (trimSails && canControl)
                {
                    // TODO: logic for steerig square sails goes here    
                }
            }
            else if (ropeWinch.rope is RopeControllerSailAngleJib)
            {
                if (trimSails && canControl)
                {
                    // TODO logic for steering jib sails goes here
                }
            }
        }
        private void HoistDown()
        {
            if (ropeWinch.rope.currentLength > 0)
            {
                // only visual effect of turning winch wheel
                Traverse.Create(ropeWinch).Field("currentInput").SetValue(25f);
                ropeWinch.ApplyRotation();
                // hoist logic
                float previousLength = ropeWinch.rope.currentLength;
                ropeWinch.rope.currentLength -= hoistingSpeed; // Hoist the sailComponent
                if (previousLength == ropeWinch.rope.currentLength || ropeWinch.rope.currentLength < 0f)
                {
                    ropeWinch.rope.currentLength = 0f; // Prevents the sail from getting stuck at 0.005f
                    hoistSails = false;
                    hoisted = !hoisted;
                }
            }
        }
        private void HoistUp()
        {
            if (ropeWinch.rope.currentLength < 1)
            {
                // only visual effect of turning winch wheel
                Traverse.Create(ropeWinch).Field("currentInput").SetValue(-25f);
                ropeWinch.ApplyRotation();
                // hoist logic
                float previousLength = ropeWinch.rope.currentLength;
                ropeWinch.rope.currentLength += hoistingSpeed; // Hoist the sailComponent
                if (previousLength == ropeWinch.rope.currentLength || ropeWinch.rope.currentLength > 1f)
                {
                    ropeWinch.rope.currentLength = 1f; // Prevents the sail from getting stuck at 0.005f
                    hoistSails = false;
                    hoisted = !hoisted;
                }
                
            }

        }
    }
}