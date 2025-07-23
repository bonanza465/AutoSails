using HarmonyLib;
// using System;
using System.Reflection;
using UnityEngine;
using SailwindModdingHelper;
// using System.Runtime.CompilerServices;
// using System;
// using UnityEngine.Experimental.GlobalIllumination;

namespace AutoSails
{
    public class AutoSailsControlSail : MonoBehaviour
    {
        private Transform sailTransform;
        private Transform boatTransform;

        public Sail sailComponent;

        private FieldInfo unamplifiedForwardInfo;
        private FieldInfo unamplifiedSideInfo;
        private FieldInfo totalWindForceInfo;

        private GPButtonRopeWinch ropeWinch;

        private bool hoistSails = false; // This is used to determine if the sails should be hoisted or not
        private bool trimSails = false; // This is used to determine if the sails should be trimmed or not
        private bool hoisted = false; // This is used to determine if the sails are hoisted or not
        // private string sailName;
        float hoistingSpeed = 0.005f; // Speed at which the sails are hoisted
        float trimmingSpeed = 0.0005f; // Speed at which the sails are hoisted
                                       // float hoistSign = -1;  // This is used to determine the direction of the hoist, depending on the sail type
                                       //TODO on some ships, the hoist Sign calculation does not work properly. Why?
        bool reverseReefing; 
        float trimDirection = 1f; // This is used to determine the direction of the trim
        float oldEfficiency = 1f;
        int i = 0;
        public void Awake()
        {
            unamplifiedForwardInfo = AccessTools.Field(typeof(Sail), "unamplifiedForwardForce");
            unamplifiedSideInfo = AccessTools.Field(typeof(Sail), "unamplifiedSidewayForce");
            totalWindForceInfo = AccessTools.Field(typeof(Sail), "totalWindForce");
            ropeWinch = GetComponentInParent<GPButtonRopeWinch>();

            GameEvents.OnPlayerInput += (_, __) =>
            {
                if (AutoSailsMain.hoistSails.Value.IsDown())
                {
                    hoistSails = !hoistSails;
                }
                if (AutoSailsMain.trimSails.Value.IsDown())
                {
                    trimSails = !trimSails;
                }
            };

        }



        private int SailDegree()
        {   //gets the sailComponent in and returns the angle wiht the boat forward direction out out

            if (sailTransform == null) sailTransform = sailComponent.transform;
            if (boatTransform == null) boatTransform = sailComponent.shipRigidbody.transform;

            Vector3 boatVector = boatTransform.forward;    //boat direction
            Vector3 sailVector = sailComponent.squareSail ? sailTransform.up : sailTransform.right; //sailComponent "direction" since squares are made differently we use the up direction for them, otherwise the -right direction (also known as left)

            int angle = Mathf.RoundToInt(Vector3.SignedAngle(boatVector, sailVector, Vector3.up)); //calculate the angle

            angle = angle > 90 ? 180 - angle : angle; //keep it in a 0° to 90° angle
            angle = angle < 0 ? -angle : angle; //keep it positive
            return angle;
        }
        private float SailEfficiency()
        {   // Calculates the efficiency of a sailComponent trim (max is best)

            //This is the force created by the sailComponent
            float unamplifiedForce = (float)unamplifiedForwardInfo.GetValue(sailComponent);
            //This is the total force the wind applies to the sailComponent. This is also the maximum force forward the sailComponent can generate on the boat.
            float totalWindForce = GetTotalForce();
            float efficiency = unamplifiedForce / totalWindForce * 100f;

            return efficiency;
        }
        private float SailInefficiency()
        {   // Calculates the percentage of sideway force on a sailComponent (min is best)
            float unamplifiedForce = (float)unamplifiedSideInfo.GetValue(sailComponent);

            //This is the total force the wind applies to the sailComponent. This is also the maximum force forward the sailComponent can generate on the boat.
            float totalWindForce = GetTotalForce();

            float inefficiency = Mathf.Abs(unamplifiedForce / totalWindForce * 100f);

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
                return (float)totalWindForceInfo.GetValue(sailComponent);
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

            // TODO various field displayed on the winches for debug
            // if (ropeWinch.IsLookedAt() || ropeWinch.IsStickyClicked() || ropeWinch.IsCliked())
            // {
            //     ropeWinch.description = "";
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
            //     // ropeWinch.description += $"\n CombinedEfficiency: {Mathf.Round(CombinedEfficiency())}";
            //     // ropeWinch.description += $"\n SailEfficiency: {Mathf.Round(SailEfficiency())}";
            //     // ropeWinch.description += $"\n SailInefficiency: {Mathf.Round(SailInefficiency())}";
            // }
            if (
                ropeWinch.rope is RopeControllerSailAngle
                || ropeWinch.rope is RopeControllerSailAngleSquare
                || ropeWinch.rope is RopeControllerSailAngleJib
            )
            {
                sailComponent = (Sail)Traverse.Create(ropeWinch.rope).Field("sail").GetValue();
            }

            if (ropeWinch.rope is RopeControllerSailReef)
            {
                if (hoistSails)
                {
                    if (hoisted)
                    {
                        if (reverseReefing)
                        {
                            hoistUp();
                        }
                        else
                        {
                            hoistDown();
                        }
                    }
                    else
                    {
                        if (reverseReefing)
                        {
                            hoistDown();
                        }
                        else
                        {
                            hoistUp();
                        }
                    }
                }
            }
            else if (ropeWinch.rope is RopeControllerSailAngle)
            {
                if (trimSails)
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
                // TODO: logic for steerig square sails goes here        
            }
            else if (ropeWinch.rope is RopeControllerSailAngleJib)
            {
                // TODO logic for steering jib sails goes here
            }
        }
        private void hoistDown()
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
        private void hoistUp()
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