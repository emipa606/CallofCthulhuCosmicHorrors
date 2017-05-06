﻿using System.Collections.Generic;
using System.Reflection;
using Verse;
using RimWorld;

namespace Cthulhu.Detour
{
    internal static class _MassUtility
    {
        [Detour(typeof(MassUtility), bindingFlags = (BindingFlags.Static | BindingFlags.Public))]
        public static bool CanEverCarryAnything(Pawn p) => p.RaceProps.ToolUser || p.RaceProps.packAnimal || p.def.defName == "ROM_DarkYoung";

    }

    internal static class _CollectionsMassCalculator
    {

        internal static FieldInfo _tmpThingStackParts;

        internal static List<ThingStackPart> GetTmpThingStackParts()
        {
            if (_CollectionsMassCalculator._tmpThingStackParts == null)
            {
                _CollectionsMassCalculator._tmpThingStackParts = typeof(CollectionsMassCalculator).GetField("tmpThingStackParts", BindingFlags.Static | BindingFlags.NonPublic);
                if (_CollectionsMassCalculator._tmpThingStackParts == null)
                {
                    Log.ErrorOnce("Unable to reflect CollectionsMassCalculator.tmpThingStackParts!", 225232221);
                }
            }
            return (List<ThingStackPart>)_CollectionsMassCalculator._tmpThingStackParts.GetValue(new object[0]);
        }

        [Detour(typeof(CollectionsMassCalculator), bindingFlags = (BindingFlags.Static | BindingFlags.Public))]
        internal static float CapacityTransferables(List<TransferableOneWay> transferables)
        {
            Cthulhu.Utility.DebugReport("Detour Called: CollectionMassCalc");
            GetTmpThingStackParts().Clear();
            for (int i = 0; i < transferables.Count; i++)
            {
                if (transferables[i].HasAnyThing)
                {
                    if (transferables[i].AnyThing is Pawn ||
                        transferables[i].AnyThing.def.defName == "ROM_DarkYoung")
                    {
                        TransferableUtility.TransferNoSplit(transferables[i].things, transferables[i].CountToTransfer, delegate (Thing originalThing, int toTake)
                        {
                            GetTmpThingStackParts().Add(new ThingStackPart(originalThing, toTake));
                        }, false, false);
                    }
                }
            }
            float result = CollectionsMassCalculator.Capacity(GetTmpThingStackParts());
            GetTmpThingStackParts().Clear();
            return result;
        }


    }
}
