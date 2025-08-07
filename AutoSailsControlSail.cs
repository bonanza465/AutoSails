using HarmonyLib;
using UnityEngine;
using SailwindModdingHelper;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
// using System.Diagnostics;


namespace AutoSails
{
    public enum HoistState
    {
        Idle,           // Not moving
        Hoisting,       // Moving up
        Lowering,       // Moving down
        PausedHoisting, // Paused while hoisting
        PausedLowering  // Paused while lowering
    }

    public class AutoSailsControlSail : MonoBehaviour
    {

        private Sail sail;
        private RopeController hoistWinch;
        private GPButtonRopeWinch hoistButton;
        // Changed from [] to new List<T>() - couldn't get [] syntax to build despite LangVersion=latest
        private List<GPButtonRopeWinch> angleButtons = new List<GPButtonRopeWinch>();
        private Queue<float> sailAngles = new Queue<float>();
        private const int maxSailAngles = 50;

        public bool canControl = false;
        private PurchasableBoat boat;
        // private Sail sailComponent;
        // private GPButtonRopeWinch ropeWinch;
        public HoistState hoistState = HoistState.Idle; // Current hoisting state for interruptible control
        public bool hoistSailsSquare = false;
        public bool hoistSailsLateen = false;
        public bool hoistSailsJunk = false;
        public bool hoistSailsGaff = false;
        public bool hoistSailsOther = false;
        public bool hoistSailsStaysail = false;

        public bool trimSails = false; // This is used to determine if the sails should be trimmed or not
        public bool hoisted = false; // This is used to determine if the sails are hoisted or not
        public bool hoistedTrimm= false; // This is used to determine if the sails are hoisted or not
        // private string sailName;
        private float hoistingSpeed = 0.005f; // Speed at which the sails are hoisted
        private float trimmingSpeed = 0.0005f; // Speed at which the sails are hoisted
                                               // float hoistSign = -1;  // This is used to determine the direction of the hoist, depending on the sail type
                                               //TODO on some ships, the hoist Sign calculation does not work properly. Why?
        private bool reverseReefing = false;
        private float trimDirection = -1f; // This is used to determine the direction of the trim
        private float oldEfficiency = 1f;
        private int i = 0;
        private void Start()
        {

            sail = GetComponent<Sail>();
            if (!sail) return;

            GPButtonRopeWinch[] allGPButtonRopeWinch = FindObjectsOfType<GPButtonRopeWinch>();
            foreach (GPButtonRopeWinch button in allGPButtonRopeWinch)
            {
                if (button.rope)
                {
                    // Check for hoist winch
                    if (button.rope is RopeControllerSailReef reefWinch && reefWinch.sail == sail)
                    {
                        hoistButton = button;
                        hoistWinch = button.rope;
                    }
                    // Check for angle winch types
                    else if (IsAngleWinch(button.rope, sail))
                    {
                        angleButtons.Add(button);
                    }
                }
            }

            reverseReefing = (bool)Traverse.Create(hoistWinch).Field("reverseReefing").GetValue();
            boat = (PurchasableBoat)Traverse.Create(hoistButton).Field("boat").GetValue();

            GameEvents.OnPlayerInput += (_, __) =>
            {
                if (AutoSailsMain.hoistSails.Value.IsDown() && canControl)
                {
                    HandleHoistInput();
                }
                if (AutoSailsMain.trimSails.Value.IsDown() && canControl)
                {
                    trimSails = !trimSails;
                    
                    var debugLevel = AutoSailsMain.debugLogging.Value;
                    if (debugLevel == AutoSails.DebugLoggingLevel.Trim || debugLevel == AutoSails.DebugLoggingLevel.All)
                    {
                        AutoSailsMain.Logger.LogInfo($"Trim toggle: trimSails={trimSails}, hoisted={hoisted}");
                    }
                    
                    // UI elements
                    if (AutoSailsMain.autoSailsUI.Value)
                    {
                        if (trimSails)
                        {
                            NotificationUi.instance.ShowNotification("Start trimming the sails!");
                        }
                        else
                        {
                            NotificationUi.instance.ShowNotification("Stop trimming the sails!");
                        }
                    }
                }
            };

        }

        private bool IsAngleWinch(RopeController winch, Sail sail)
        {
            var type = winch.GetType().Name;
            // List of angle winch types
            bool isAngleType =
                type == "RopeControllerSailAngle" ||
                type == "RopeControllerSailAngleJib" ||
                type == "RopeControllerSailAngleSquare";

            if (!isAngleType) return false;
            var winchSail = (Sail)Traverse.Create(winch).Field("sail").GetValue();
            return winchSail == sail;
        }

        private float SailDegree()
        {   //gets the sailComponent in and returns the angle wiht the boat forward direction out out

            Vector3 boatVector = boat.transform.forward;    //boat direction
            Vector3 sailVector = sail.squareSail ? sail.transform.up : sail.transform.right; //sailComponent "direction" since squares are made differently we use the up direction for them, otherwise the -right direction (also known as left)

            float angle = Vector3.SignedAngle(boatVector, sailVector, Vector3.up); //calculate the angle

            // angle = angle > 90 ? 180 - angle : angle; //keep it in a 0° to 90° angle
            // angle = angle < 0 ? -angle : angle; //keep it positive
            return angle;
        }
        private float SailEfficiency()
        {   // Calculates the efficiency of a sail trim (max is best)

            //This is the force created by the sail
            // float unamplifiedForce = (float)unamplifiedForwardInfo.GetValue(sail);
            float unamplifiedForce = (float)Traverse.Create(sail).Field("unamplifiedForwardForce").GetValue();
            //This is the total force the wind applies to the sailComponent. This is also the maximum force forward the sail can generate on the boat.
            float totalWindForce = GetTotalForce();
            float efficiency = unamplifiedForce / totalWindForce * 100f;

            return efficiency;
        }
        private float SailInefficiency()
        {   // Calculates the percentage of sideway force on a sail (min is best)
            // float unamplifiedSideInfo = (float)unamplifiedSideInfo.GetValue(sailComponent);
            float unamplifiedSideInfo = (float)Traverse.Create(sail).Field("unamplifiedSidewayForce").GetValue();
            //This is the total force the wind applies to the sail. This is also the maximum force forward the sail can generate on the boat.
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
            float comb = (3 * eff + ineff) / 4f;

            return comb;
        }
        private float GetTotalForce()
        {   // avoid using reflections when possible to get totalWindForce
            float applied = sail.appliedWindForce;

            if (applied == 0f)
            {   //use reflections to get totalWindForce instead
                return (float)Traverse.Create(sail).Field("totalWindForce").GetValue();
            }

            return applied / sail.GetCapturedForceFraction();
        }
        private void FixedUpdate()
        {
            if (!sail || !hoistWinch || !hoistButton) return;
            if (boat == null) return;
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
            

        }
        private void Update()
        {
            // set steady wind conditions for testing
            // Wind.currentWind = new Vector3(1f, 0f, -0.25f).normalized * 9f;
            if (!sail || !hoistWinch || !hoistButton) return;

            // Overlays for debug
            var debugUILevel = AutoSailsMain.debugUI.Value;
            if (debugUILevel != AutoSails.DebugLoggingLevel.None && (hoistButton.IsLookedAt() || hoistButton.IsStickyClicked() || hoistButton.IsCliked()))
            {
                hoistButton.description = "";
                
                // State-specific debug info
                if (debugUILevel == AutoSails.DebugLoggingLevel.State || debugUILevel == AutoSails.DebugLoggingLevel.All)
                {
                    hoistButton.description += $"\n Sail: {sail}";
                    hoistButton.description += $"\n hoistWinch: {hoistWinch}";
                    hoistButton.description += $"\n angleButtons: {angleButtons.Count}";
                    hoistButton.description += $"\n canControl: {canControl}";
                    hoistButton.description += $"\n Sail name: {sail.sailName}";
                    hoistButton.description += $"\n squareSail: {sail.squareSail}";
                    hoistButton.description += $"\n junkType: {sail.junkType}";
                    // hoistButton.description += $"\n SailCategory: {sail.category}";
                }
                
                // Hoist-specific debug info
                if (debugUILevel == AutoSails.DebugLoggingLevel.Hoist || debugUILevel == AutoSails.DebugLoggingLevel.All)
                {
                    hoistButton.description += $"\n reverseReefing: {reverseReefing}";
                    hoistButton.description += $"\n hoisted: {hoisted}";
                    hoistButton.description += $"\n hoistState: {hoistState}";
                    hoistButton.description += $"\n currentLength: {hoistButton.rope.currentLength}";
                    hoistButton.description += reverseReefing && hoistButton.rope.currentLength == 0;
                    hoistButton.description += !reverseReefing && hoistButton.rope.currentLength == 1;
                    hoistButton.description += !reverseReefing && hoistButton.rope.currentLength == 1;
                    hoistButton.description += reverseReefing && hoistButton.rope.currentLength == 0;
                    hoistButton.description += $"\n {hoisted}";
                    hoistButton.description += $"\n {reverseReefing}";
                    hoistButton.description += $"\n {hoistButton.rope.currentLength}";
                    hoistButton.description += $"\n Update() calls PerformHoist: {(hoistState == HoistState.Hoisting || hoistState == HoistState.Lowering)}";
                    if (hoistState == HoistState.Hoisting || hoistState == HoistState.Lowering)
                    {
                        hoistButton.description += $"\n PerformHoist direction: {(hoistState == HoistState.Hoisting ? "UP" : "DOWN")}";
                    }
                    hoistButton.description += $"\n Rope changed flag: {(hoistButton.rope?.changed ?? false)}";
                    hoistButton.description += $"\n Last frame state was: {hoistState}";
                }
                
                // Trim-specific debug info would go here when trim UI debug is needed
                
                 
                
            }
            if (hoistState == HoistState.Hoisting || hoistState == HoistState.Lowering)
            {
                // Simple garage door logic: hoisting moves toward out, lowering moves toward in
                bool movingOut = (hoistState == HoistState.Hoisting);
                
                // Convert "moving out" to the correct up/down direction based on sail type
                bool up = reverseReefing ? !movingOut : movingOut;
                
                PerformHoist(up);
            }
            else if ( // Determine hoisted status based on sail type
                (reverseReefing && hoistButton.rope.currentLength <= 0) // reverse reefing: 0 = fully hoisted
                ||
                (!reverseReefing && hoistButton.rope.currentLength > 0) // normal: > 0 = hoisted
            )
            {
                hoisted = true;
            }
            else if ( // Determine furled status based on sail type
                (reverseReefing && hoistButton.rope.currentLength >= 1) // reverse reefing: 1 = furled
                ||
                (!reverseReefing && hoistButton.rope.currentLength <= 0) // normal: 0 = furled
            )
            {
                hoisted = false;
            }


            // Overlays for debug
            // foreach (GPButtonRopeWinch winchButton in angleButtons)
            // {
            //     string windSide = Vector3.SignedAngle(boat.transform.forward, sail.apparentWind, Vector3.up) < 0 ? "starboard" : "port";
            //     winchButton.description = "";
            //     winchButton.description += $"\n SailDegree: {SailDegree()}";
            //     winchButton.description += $"\n windSide: {windSide}";
            //     winchButton.description += $"\n winchButton.rope.currentLength: {winchButton.rope.currentLength}";
            //     winchButton.description += $"\n CombinedEfficiency(): {CombinedEfficiency():F2}";
            //     winchButton.description += $"\n SailEfficiency(): {SailEfficiency():F2}";
            //     winchButton.description += $"\n SailInefficiency(): {SailInefficiency():F2}";
            //     winchButton.description += $"\n ApparentWind: {Vector3.SignedAngle(-boat.transform.forward, sail.apparentWind, Vector3.up)}";
            //     winchButton.description += $"\n AngleStandardDeviation: {AngleStandardDeviation():F4}";
            //     winchButton.description += $"\n Windangle: {Vector3.SignedAngle(boat.transform.forward, sail.apparentWind, Vector3.up):F4}";
            // }
            if (trimSails && hoisted)
            {
                string windSide = Vector3.SignedAngle(boat.transform.forward, sail.apparentWind, Vector3.up) < 0 ? "starboard" : "port";
                if (sail.category is SailCategory.junk || sail.category is SailCategory.gaff || sail.category is SailCategory.lateen)
                {
                    if (AutoSailsMain.autoSailsAutoJibe.Value)
                    {
                        // wind from wrong side, pull main sheet tight. Hopefully sail degree works for all ships in the same way
                        if (
                            (((windSide == "starboard") && (SailDegree() < -5))
                            ||
                            ((windSide == "port") && (SailDegree() > 5)))
                            &&
                            (Mathf.Abs(SailDegree()) > 8)
                            &&
                            (Mathf.Abs(Vector3.SignedAngle(boat.transform.forward, sail.apparentWind, Vector3.up)) > 8)
                        )
                        {
                            TightenSheetRope(angleButtons[0]);
                        }
                        else
                        {
                            PrimitiveSailControl(angleButtons[0]);
                        }
                    }
                    else
                    {
                        PrimitiveSailControl(angleButtons[0]);
                    }
                }
                else if (sail.category is SailCategory.staysail)
                {
                    AddAngle(SailDegree());
                    foreach (GPButtonRopeWinch winchButton in angleButtons)
                    {
                        if (
                            (((RopeControllerSailAngleJib)winchButton.rope).side == RopeControllerSailAngleJib.JibWinch.left)
                            && (windSide == "starboard")
                            )
                        {
                            PrimitiveSailControl(winchButton);
                        }
                        else if (
                            (((RopeControllerSailAngleJib)winchButton.rope).side == RopeControllerSailAngleJib.JibWinch.right)
                            && (windSide == "port")
                            )
                        {
                            PrimitiveSailControl(winchButton);
                        }
                        else
                        {
                            LoosenSheetRope(winchButton); // quickly loosen
                        }
                    }
                }
                else if (sail.category is SailCategory.square)
                {
                    AddAngle(SailDegree());
                    foreach (GPButtonRopeWinch winchButton in angleButtons)
                    {
                        // check if square sail is in the right orientation?
                        if ((windSide == "port") && AngleMean() < 90)
                        {
                            // loosen left, tighten right
                            if (((RopeControllerSailAngleSquare)winchButton.rope).side == RopeControllerSailAngleSquare.WinchSide.left)
                            {
                                LoosenSheetRope(winchButton);
                            }
                            else
                            {
                                TightenSheetRope(winchButton);
                            }
                        }
                        else if ((windSide == "starboard") && AngleMean() > 90)
                        {
                            // loosen right, tighten left
                            if (((RopeControllerSailAngleSquare)winchButton.rope).side == RopeControllerSailAngleSquare.WinchSide.left)
                            {
                                TightenSheetRope(winchButton);
                            }
                            else
                            {
                                LoosenSheetRope(winchButton);
                            }
                        }
                        else if (
                                (((RopeControllerSailAngleSquare)winchButton.rope).side == RopeControllerSailAngleSquare.WinchSide.left)
                                && (windSide == "port")
                                )
                        {
                            // winchButton.description += "\nleft";
                            PrimitiveSailControl(winchButton);
                        }
                        else if (
                            (((RopeControllerSailAngleSquare)winchButton.rope).side == RopeControllerSailAngleSquare.WinchSide.right)
                            && (windSide == "starboard")
                            )
                        {
                            PrimitiveSailControl(winchButton);
                            // winchButton.description += "right";
                        }
                        else
                        {
                            if (winchButton.rope.currentLength < 1f)
                            {
                                Traverse.Create(winchButton).Field("currentInput").SetValue(-5f);
                                winchButton.ApplyRotation();
                                winchButton.rope.currentLength += 4 * trimmingSpeed;
                            }
                            else
                            {
                                winchButton.rope.currentLength = 1f;
                            }
                        }
                    }
                }
            }
            else if (trimSails && !hoisted)
            {
                // furling sails automation
                if (sail.category is SailCategory.junk || sail.category is SailCategory.gaff || sail.category is SailCategory.lateen)
                {
                    TightenSheetRope(angleButtons[0]);
                }
                else if (sail.category is SailCategory.staysail)
                {
                    foreach (GPButtonRopeWinch winchButton in angleButtons)
                    {
                        LoosenSheetRope(winchButton);
                    }

                }
                else if (sail.category is SailCategory.square)
                {
                    AddAngle(SailDegree());
                    foreach (GPButtonRopeWinch winchButton in angleButtons)
                    {
                        if (AngleMean() < 85)
                        {
                            // loosen left, tighten right
                            if (((RopeControllerSailAngleSquare)winchButton.rope).side == RopeControllerSailAngleSquare.WinchSide.left)
                            {
                                LoosenSheetRope(winchButton);
                            }
                            else
                            {
                                TightenSheetRope(winchButton);
                            }
                        }
                        else if (AngleMean() > 95)
                        {
                            // loosen right, tighten left
                            if (((RopeControllerSailAngleSquare)winchButton.rope).side == RopeControllerSailAngleSquare.WinchSide.left)
                            {
                                TightenSheetRope(winchButton);
                            }
                            else
                            {
                                LoosenSheetRope(winchButton);
                            }
                        }
                    }

                }


            }
        }
        private void PrimitiveSailControl(GPButtonRopeWinch button)
        {
            // only visual effect of turning winch wheel
            Traverse.Create(button).Field("currentInput").SetValue(-trimDirection * 5f);
            button.ApplyRotation();
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
            if (sail.category == SailCategory.staysail)
            {
                if (AngleStandardDeviation() > .5)
                {
                    trimDirection = -1f;
                    i = 0;
                    TightenSheetRope(button); // quickly tighten
                    return;
                }
            }
            else if (sail.category == SailCategory.square)
            {
                if (CombinedEfficiency() == 0)
                {
                    trimDirection = 1f;
                    i = 0;
                }
            }
            else // gaff, junk, lateen sails (but also other, and I dont know what these are)
            {
                if (CombinedEfficiency() == 0f)
                {
                    // if the efficiency is 0, we need to tighten the sail
                    trimDirection = -1f;
                    i = 0;
                    TightenSheetRope(button); // quickly tighten
                    return;
                }
            }

            if (button.rope.currentLength > 1f)
            {
                // button.rope.currentLength -= trimmingSpeed;
                button.rope.currentLength = 1f;

            }
            else if (button.rope.currentLength < 0f)
            {
                // button.rope.currentLength += trimmingSpeed;
                button.rope.currentLength = 0f;
            }
            else
            {

                button.rope.currentLength += trimDirection * trimmingSpeed;
            }
            button.rope.changed = true; // mark the rope as changed such that changes are applied
            i++;
        }

        private void LoosenSheetRope(GPButtonRopeWinch button)
        {
            if (button.rope.currentLength > 1f)
            {
                // button.rope.currentLength -= trimmingSpeed;
                button.rope.currentLength = 1f;
            }
            else
            {
                button.rope.currentLength += 4 * trimmingSpeed;
            }
            button.rope.changed = true;
        }
        private void TightenSheetRope(GPButtonRopeWinch button)
        {
            if (button.rope.currentLength < 0f)
            {
                // button.rope.currentLength -= trimmingSpeed;
                button.rope.currentLength = 0f;

            }
            else
            {
                button.rope.currentLength -= 4 * trimmingSpeed;

            }
            button.rope.changed = true;
        }

        private void PerformHoist(bool up)
        {
            var debugLevel = AutoSailsMain.debugLogging.Value;
            if (debugLevel == AutoSails.DebugLoggingLevel.Hoist || debugLevel == AutoSails.DebugLoggingLevel.All)
            {
                AutoSailsMain.Logger.LogInfo($"PerformHoist: up={up}, currentLength={hoistWinch.currentLength:F3}, state={hoistState}");
            }
            
            float prevLength = hoistWinch.currentLength;
            

            // Visual winch wheel rotation
            float input = up ? 25f : -25f;
            Traverse.Create(hoistButton).Field("currentInput").SetValue(input);
            hoistButton.ApplyRotation();

            // Adjust hoist length
            hoistWinch.currentLength += up ? -hoistingSpeed : hoistingSpeed;

            // Clamp and stop condition
            if (hoistWinch.currentLength > 1f) hoistWinch.currentLength = 1f;
            if (hoistWinch.currentLength < 0f) hoistWinch.currentLength = 0f;

            // Only stop if we've reached the limit AND can't move further
            bool hitUpperLimit = (hoistWinch.currentLength >= 1f && up);
            bool hitLowerLimit = (hoistWinch.currentLength <= 0f && !up);
            bool stuckAtSamePosition = Mathf.Approximately(prevLength, hoistWinch.currentLength);
            
            if (hitUpperLimit || hitLowerLimit || stuckAtSamePosition)
            {
                if (debugLevel == AutoSails.DebugLoggingLevel.Hoist || debugLevel == AutoSails.DebugLoggingLevel.All)
                {
                    string reason = hitUpperLimit ? "hit upper limit" : hitLowerLimit ? "hit lower limit" : "stuck at same position";
                    AutoSailsMain.Logger.LogInfo($"PerformHoist: Stopping - {reason}. Final length={hoistWinch.currentLength:F3}");
                }
                hoistState = HoistState.Idle;
            }
        }
        private void AddAngle(float angle)
        {
            sailAngles.Enqueue(angle);
            while (sailAngles.Count > maxSailAngles)
                sailAngles.Dequeue();
        }
        float AngleStandardDeviation()
        {
            if (sailAngles.Count == 0) return 0f;

            float mean = AngleMean();
            float sumSq = sailAngles.Sum(v => (v - mean) * (v - mean));
            return Mathf.Sqrt(sumSq / sailAngles.Count);
        }
        float AngleMean()
        {
            if (sailAngles.Count == 0) return 0f;
            return sailAngles.Average();
        }
        
        private void ShowHoistStateNotification()
        {
            if (!AutoSailsMain.autoSailsUI.Value) return;
            
            string message = hoistState switch
            {
                HoistState.Hoisting => GetRandomMessage(new[] { "Hoist away!", "Run up the colors!", "Set all sail!" }),
                HoistState.Lowering => GetRandomMessage(new[] { "Strike the sails!", "Douse the sails!", "Take in sail!" }),
                HoistState.PausedHoisting => GetRandomMessage(new[] { "Belay that!", "Avast!", "Hold fast!" }),
                HoistState.PausedLowering => GetRandomMessage(new[] { "Belay that!", "Avast!", "As you were!" }),
                _ => ""
            };
            
            if (!string.IsNullOrEmpty(message))
            {
                NotificationUi.instance.ShowNotification(message);
            }
        }
        
        private string GetRandomMessage(string[] messages)
        {
            return messages[UnityEngine.Random.Range(0, messages.Length)];
        }
        
        private void HandleHoistInput()
        {
            var debugLevel = AutoSailsMain.debugLogging.Value;
            if (debugLevel == AutoSails.DebugLoggingLevel.Hoist || debugLevel == AutoSails.DebugLoggingLevel.State || debugLevel == AutoSails.DebugLoggingLevel.All)
            {
                AutoSailsMain.Logger.LogInfo($"HandleHoistInput: Current state = {hoistState}, hoisted = {hoisted}");
            }
            
            switch (hoistState)
            {
                case HoistState.Idle:
                    // Start hoisting if sails are down, lowering if sails are up
                    hoistState = hoisted ? HoistState.Lowering : HoistState.Hoisting;
                    break;
                case HoistState.Hoisting:
                    hoistState = HoistState.PausedHoisting;
                    break;
                case HoistState.PausedHoisting:
                    if (AutoSailsMain.resumeHoisting.Value == AutoSails.ResumeHoistingBehavior.SameDirection)
                    {
                        hoistState = HoistState.Hoisting;
                    }
                    else
                    {
                        hoistState = HoistState.Lowering;
                    }
                    break;
                case HoistState.Lowering:
                    hoistState = HoistState.PausedLowering;
                    break;
                case HoistState.PausedLowering:
                    if (AutoSailsMain.resumeHoisting.Value == AutoSails.ResumeHoistingBehavior.SameDirection)
                    {
                        hoistState = HoistState.Lowering;
                    }
                    else
                    {
                        hoistState = HoistState.Hoisting;
                    }
                    break;
            }
            
            // for simplification, all sails use the same hotkey
            bool shouldHoist = (hoistState == HoistState.Hoisting || hoistState == HoistState.PausedHoisting);
            hoistSailsSquare = shouldHoist;
            hoistSailsLateen = shouldHoist;
            hoistSailsJunk = shouldHoist;
            hoistSailsGaff = shouldHoist;
            hoistSailsOther = shouldHoist;
            hoistSailsStaysail = shouldHoist;
            
            ShowHoistStateNotification();
            
            if (debugLevel == AutoSails.DebugLoggingLevel.Hoist || debugLevel == AutoSails.DebugLoggingLevel.State || debugLevel == AutoSails.DebugLoggingLevel.All)
            {
                AutoSailsMain.Logger.LogInfo($"HandleHoistInput: New state = {hoistState}");
            }
        }
    }
}