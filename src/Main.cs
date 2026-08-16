using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Polytopia;
using Polytopia.Data;
using PolytopiaBackendBase.Common;
using Polibrary.PolyScript;
using Il2Gen = Il2CppSystem.Collections.Generic;
using DG.Tweening;

namespace IllusionJamMod;

public static class Main
{
    public static ManualLogSource modLogger;
    public static void Load(ManualLogSource logger)
    {
        modLogger = logger;
        Harmony.CreateAndPatchAll(typeof(Main));
    }

    static UnitData.Type RealUnit;
    static UnitData.Type FakeUnit;
    static UnitAbility.Type IllusionAbil;
    public static Dictionary<UnitState, UnitState> Pairs = new();

    public static void SetPair(this UnitState inst, UnitState pair)
    {
        Pairs[inst] = pair;
        Pairs[pair] = inst;
    }
    
    public static void RemovePair(this UnitState inst)
    {
        Pairs.Remove(inst.GetPair());
        Pairs.Remove(inst);
    }

    public static UnitState GetPair(this UnitState inst)
    {
        if (Pairs.TryGetValue(inst, out var pair))
        {
            return pair;
        }
        return null;
    }
    public static bool TryGetPair(this UnitState inst, out UnitState pair)
    {
        if (inst.GetPair() == null)
        {
            pair = null;
            return false;
        }
        else
        {
            pair = inst.GetPair();
            return true;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void RegisterStuff()
    {
        if (
            !EnumCache<UnitData.Type>.TryGetType("afterimage", out RealUnit) ||
            !EnumCache<UnitData.Type>.TryGetType("afterimagefake", out FakeUnit) ||
            !EnumCache<UnitAbility.Type>.TryGetType("illusion", out IllusionAbil)
        )
        {
            modLogger.LogError("couldnt find enum stuff");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetMovementOptions))]
    private static void DisableMoveForDummy(UnitState unit, GameState gameState, int range, ref Il2Gen.List<WorldCoordinates> __result)
    {
        if (unit.type == FakeUnit)
        {
            __result = new Il2Gen.List<WorldCoordinates>();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MoveAction), nameof(MoveAction.Execute))]
    private static void MoveActionPatch(GameState state, MoveAction __instance)
    {
        if (__instance.Reason == MoveAction.MoveReason.Push) return;

        if (
            !state.TryGetUnit(__instance.UnitId, out var unit) || 
            !state.TryGetPlayer(__instance.PlayerId, out var player) || 
            !state.GameLogicData.TryGetData(FakeUnit, out var fakeUnitData)
            )
        {
            modLogger.LogError("idiot");
            return;
        }
        if (unit.type == RealUnit)
        {
            if (unit.TryGetPair(out var dummy))
            {
                unit.RemovePair();
                state.Map.GetTile(dummy.coordinates).SetUnit(null);
            }

            WorldCoordinates originCoords = __instance.Path[__instance.Path.Count - 1];

            UnitState newDummy = ActionUtils.TrainUnit(state, player, state.Map.GetTile(originCoords), fakeUnitData);
            newDummy.MakeExhauseted(state);
            unit.SetPair(newDummy);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MoveReaction), nameof(MoveReaction.Execute))]
    private static void MoveReactionPatch(Il2CppSystem.Action onComplete, MoveReaction __instance)
    {
        if (__instance.action.Reason == MoveAction.MoveReason.Push) return;

        if (!GameManager.GameState.TryGetUnit(__instance.action.UnitId, out var unit))
        {
            modLogger.LogError("idiot");
            return;
        }
        if (unit.type == RealUnit)
        {
            WorldCoordinates originCoords = __instance.action.Path[__instance.action.Path.Count - 1];

            foreach (TileData tileData in GameManager.GameState.Map.GetArea(originCoords, 3, true, true))
            {
                Tile tile = MapRenderer.Current.GetTileInstance(tileData.coordinates);
                if (tile == null || tile.IsHidden) continue;
                tile.Render();
            }
        }
    }
}
