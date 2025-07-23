// using System;
// using UnityEngine;
using HarmonyLib;
// using System.Reflection;
// using System.Reflection.Emit;

namespace AutoSails
{

    [HarmonyPatch(typeof(GPButtonRopeWinch), "Awake")]
    public class AutoSailsPatches
    {
        static void Postfix(GPButtonRopeWinch __instance)
        {
            if (__instance == null) return;
            __instance.gameObject.AddComponent<AutoSailsControlSail>();
        }

    }
}