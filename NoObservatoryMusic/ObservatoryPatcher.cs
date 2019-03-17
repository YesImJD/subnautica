﻿namespace NoObservatoryMusic
{
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using System.Reflection;

    // This should patch the Observatory for us. Let's do it.
    [HarmonyPatch(typeof(ObservatoryAmbientSound))]
    [HarmonyPatch("IsPlayerInObservatory")]
    internal class InObservatoryPatcher
    {
        [HarmonyPrefix] // We're attempting to cancel the entire method
        public static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(ObservatoryAmbientSound))]
    [HarmonyPatch("OnTriggerEnter")]
    internal class OnEnterPatcher
    {
        [HarmonyPrefix] // We're attempting to cancel the entire method
        public static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(ObservatoryAmbientSound))]
    [HarmonyPatch("OnTriggerExit")]
    internal class OnExitPatcher
    {
        [HarmonyPrefix] // We're attempting to cancel the entire method
        public static bool Prefix()
        {
            return false;
        }
    }

}
