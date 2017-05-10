﻿using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Harmony;
using Verse.AI;
using System;
using UnityEngine;
using System.Reflection.Emit;
using System.Reflection;

namespace CosmicHorror
{
    [StaticConstructorOnStartup]
    static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            HarmonyInstance harmony = HarmonyInstance.Create("rimworld.cosmic_Horrors");
            harmony.Patch(AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.BestAttackTarget)),
                new HarmonyMethod(typeof(HarmonyPatches), nameof(BestAttackTargetPrefix)), null);
            harmony.Patch(AccessTools.Constructor(AccessTools.TypeByName("Wound"), new Type[] { typeof(Pawn) }), null, null, new HarmonyMethod(typeof(HarmonyPatches), nameof(WoundConstructorTranspiler)));
            harmony.Patch(AccessTools.Method(typeof(ThingSelectionUtility), nameof(ThingSelectionUtility.SelectableByMapClick)), null, new HarmonyMethod(typeof(HarmonyPatches), nameof(SelectableByMapClickPostfix)));
            harmony.Patch(AccessTools.Method(typeof(HediffSet), "CalculatePain"), new HarmonyMethod(typeof(HarmonyPatches), "CalculatePain_PreFix"), null);
        }


        // Verse.HediffSet
        static bool CalculatePain_PreFix(HediffSet __instance, ref float __result)
        {
            CosmicHorrorPawn p = __instance.pawn as CosmicHorrorPawn;
            if (p != null)
            {
                if (p.Dead)
                {
                    __result = 0f;
                    return false;
                }
                float num = 0f;
                for (int i = 0; i < __instance.hediffs.Count; i++)
                {
                    num += __instance.hediffs[i].PainOffset;
                }
                float num2 = num / p.HealthScale;
                num2 *= p.PawnExtension.painFactor;
                for (int j = 0; j < __instance.hediffs.Count; j++)
                {
                    num2 *= __instance.hediffs[j].PainFactor;
                }
                __result = Mathf.Clamp(num2, 0f, 1f);
                return false;
            }
            return true;
        }

        static void SelectableByMapClickPostfix(Thing t, ref bool __result)
        {
            if (__result)
                __result = t is CosmicHorrorPawn cosmicPawn ? !cosmicPawn.IsInvisible : true;
        }

        static IEnumerable<CodeInstruction> WoundConstructorTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            FieldInfo pawnDefInfo = AccessTools.Field(typeof(Thing), nameof(Thing.def));
            FieldInfo defNameInfo = AccessTools.Field(typeof(Def), nameof(Def.defName));
            MethodInfo startsWithInfo = AccessTools.Method(typeof(String), nameof(String.StartsWith), new Type[] { typeof(string) });
            bool didIt = false;
            List<CodeInstruction> instructionList = instructions.ToList();
            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];

                if (!didIt && instruction.opcode == OpCodes.Bne_Un)
                {
                    Label label = il.DefineLabel();
                    instructionList[i + 1].labels = new List<Label>() { label };
                    yield return new CodeInstruction(OpCodes.Beq_S, label);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Ldfld, pawnDefInfo);
                    yield return new CodeInstruction(OpCodes.Ldfld, defNameInfo);
                    yield return new CodeInstruction(OpCodes.Ldstr, "ROM_");
                    yield return new CodeInstruction(OpCodes.Callvirt, startsWithInfo);
                    instruction.opcode = OpCodes.Brfalse_S;
                    didIt = true;
                }
                yield return instruction;
            }
        }

        static void BestAttackTargetPrefix(ref Predicate<Thing> validator)
        {
            Predicate<Thing> validatorCopy = validator;
            validator = new Predicate<Thing>(delegate (Thing t)
            {
                return (validatorCopy != null ? validatorCopy(t) : true) && (t is CosmicHorrorPawn cosmicPawn ? !cosmicPawn.IsInvisible : true);
            });
        }

        public static bool IsTameable(PawnKindDef kindDef)
        {
            if (kindDef.defName == "ROM_ChthonianLarva" ||
                kindDef.defName == "ROM_DarkYoung")
                return true;
            return false;
        }

        public static IEnumerable<CosmicHorrorPawn> SpawnHorrorsOfCountAt(PawnKindDef kindDef, IntVec3 at, Map map, int count, Faction fac = null, bool berserk = false, bool target = false)
        {
            List<CosmicHorrorPawn> pawns = new List<CosmicHorrorPawn>();
            for (int i = 1; i <= count; i++)
            {
                if ((from cell in GenAdj.CellsAdjacent8Way(new TargetInfo(at, map))
                     where at.Walkable(map) && !at.Fogged(map) && at.InBounds(map)
                     select cell).TryRandomElement(out at))
                {
                    CosmicHorrorPawn pawn = null;
                    if (fac != Faction.OfPlayer)
                    {
                        PawnGenerationRequest request = new PawnGenerationRequest(kindDef, fac, PawnGenerationContext.NonPlayer, map.Tile);
                        pawn = (CosmicHorrorPawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(request), at, map);
                    }
                    else
                    {
                        pawn = (CosmicHorrorPawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(kindDef, fac), at, map);
                    }

                    pawns.Add(pawn);
                    if (berserk) pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk);
                }
            }
            return pawns.AsEnumerable<CosmicHorrorPawn>();
        }
    }
}