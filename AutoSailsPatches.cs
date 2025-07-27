// using System;
// using UnityEngine;
using HarmonyLib;
// using System.Reflection;
// using System.Reflection.Emit;

namespace AutoSails
{

    [HarmonyPatch(typeof(Sail), "Start")]
    public class AutoSailsPatches
    {
        static void Postfix(Sail __instance)
        {
            if (__instance == null) return;
            __instance.gameObject.AddComponent<AutoSailsControlSail>();
        }

    }
}