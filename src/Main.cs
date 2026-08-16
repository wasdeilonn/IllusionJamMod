using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Polytopia;
using Polytopia.Data;
using PolytopiaBackendBase.Common;
using Polibrary.PolyScript;
using Il2Gen = Il2CppSystem.Collections.Generic;

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
    public static Dictionary<uint, uint> Pairs = new();

    public static void SetPair(this UnitState inst, UnitState pair)
    {
        Pairs[inst.id] = pair.id;
        Pairs[pair.id] = inst.id;
    }
    
    public static void RemovePair(this UnitState inst, GameState state)
    {
        Pairs.Remove(inst.GetPair(state).id);
        Pairs.Remove(inst.id);
    }

    public static UnitState GetPair(this UnitState inst, GameState state)
    {
        if (Pairs.TryGetValue(inst.id, out var pair))
        {
            if (state.TryGetUnit(pair, out var pairUnit))
            return pairUnit;
        }
        return null;
    }
    public static bool TryGetPair(this UnitState inst, out UnitState pair, GameState state)
    {
        if (inst.GetPair(state) == null)
        {
            pair = null;
            return false;
        }
        else
        {
            pair = inst.GetPair(state);
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

        PolibActionManager.RegisterAction<ApplyRadiantAction>("applyradiantaction");
        PolibReactionManager.AssignReaction<ApplyRadiantReaction>("applyradiantaction");
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
            return;
        }
        if (unit.type == RealUnit)
        {
            if (unit.TryGetPair(out var dummy, state))
            {
                unit.RemovePair(state);
                state.Map.GetTile(dummy.coordinates).SetUnit(null);
            }

            WorldCoordinates originCoords = __instance.Path[__instance.Path.Count - 1];

            UnitState newDummy = ActionUtils.TrainUnit(state, player, state.Map.GetTile(originCoords), fakeUnitData);
            newDummy.MakeExhauseted(state);
            unit.SetPair(newDummy);
        }
        if (unit.type == FakeUnit)
        {
            if (!unit.TryGetPair(out var real, state))
            {
                modLogger.LogError("nice one shitsmear");
                return;
            }

            WorldCoordinates originCoords = __instance.Path[__instance.Path.Count - 1];

            state.Map.GetTile(real.coordinates).SetUnit(null);
            state.Map.GetTile(originCoords).SetUnit(real);
            real.coordinates = originCoords;
            real.MakeExhauseted(state);
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

        if (unit.type == RealUnit || unit.type == FakeUnit)
        {
            WorldCoordinates originCoords = __instance.action.Path[__instance.action.Path.Count - 1];
            Tile fromTile = MapRenderer.Current.GetTileInstance(originCoords);
            fromTile.Render();

            foreach (TileData tileData in GameManager.GameState.Map.GetArea(originCoords, 2, true, true))
            {
                Tile tile = MapRenderer.Current.GetTileInstance(tileData.coordinates);
                if (tile == null || tile.IsHidden) continue;
                tile.Render();
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(KillUnitAction), nameof(KillUnitAction.Execute))]
    private static void KillUnitPatch(GameState gameState, KillUnitAction __instance)
    {
        UnitState unit = gameState.Map.GetTile(__instance.Coordinates).unit;
        if (unit == null) return;

        if (unit.type == RealUnit)
        {
            if (unit.TryGetPair(out var dummy, gameState))
            {
                __instance.AddSubAction(new KillUnitAction(__instance.PlayerId, dummy.coordinates));
            }
        }
        if (unit.type == FakeUnit)
        {
            if (unit.TryGetPair(out var real, gameState))
            {
                ApplyRadiantAction action = PolibActionManager.MakeIl2CppAction<ApplyRadiantAction>();
                action.PlayerId = __instance.PlayerId;
                action.Coordinates = real.coordinates;
                __instance.AddSubAction(action);
            }
        }
        __instance.CommitSubActionsToStack(gameState.ActionStack);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InfiltrateAction), nameof(InfiltrateAction.Execute))]
    private static void InfiltratePatch(GameState gameState, InfiltrateAction __instance)
    {
        UnitState unit = gameState.Map.GetTile(__instance.Origin).unit;
        if (unit == null) return;

        if (unit.type == RealUnit)
        {
            if (unit.TryGetPair(out var dummy, gameState))
            {
                __instance.AddSubAction(new KillUnitAction(__instance.PlayerId, dummy.coordinates));
            }
        }
        __instance.CommitSubActionsToStack(gameState.ActionStack);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartTurnAction), nameof(StartTurnAction.Execute))]
    private static void HideRadiantUnitsPatch(GameState state, StartTurnAction __instance)
    {
        foreach (TileData tile in state.Map.tiles)
        {
            if (tile.unit == null) continue;
            if (tile.unit.owner != __instance.PlayerId) continue;
            if (tile.unit.HasEffect(EnumCache<UnitEffect>.GetType("radiant")))
            {
                __instance.AddSubAction(new HideAction(__instance.PlayerId, tile.coordinates));
            }
        }
        __instance.CommitSubActionsToStack(state.ActionStack);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(HideAction), nameof(HideAction.Execute))]
    private static void RemoveRadiancePatch(GameState state, HideAction __instance)
    {
        UnitState unit = state.Map.GetTile(__instance.Coordinates).unit;
        if (unit == null) return;

        unit.RemoveEffect(EnumCache<UnitEffect>.GetType("radiant"));
        unit.attacked = false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetAttackOptions))]
    private static void DummyCantAttack(this UnitState unit, GameState state, ref int range, bool ignoreDiplomacyRelation = false)
    {
        if (unit.type == FakeUnit)
        {
            range = 0;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Unit), nameof(Unit.OnSelected))]
    private static void SwayPairs(Unit __instance)
    {
        if (__instance.unitState.TryGetPair(out var pair, GameManager.GameState))
        {
            Tile inst = MapRenderer.Current.GetTileInstance(pair.coordinates);
            inst.Render();
            GameManager.DelayCall(0.07f, (Il2CppSystem.Action) delegate {inst.Unit.Sway();});
        }
    }

    /*[HarmonyPostfix]
    [HarmonyPatch(typeof(UnitState), nameof(UnitState.SerializeDefault))]
    private static void UnitSerialize(Il2CppSystem.IO.BinaryWriter writer, int version, UnitState __instance)
    {
        writer.Write(Pairs.GetValueOrDefault(__instance.id, uint.MaxValue));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitState), nameof(UnitState.DeserializeDefault))]
    private static void UnitDeserialize(Il2CppSystem.IO.BinaryReader reader, int version, UnitState __instance)
    {
        uint pairId = reader.ReadUInt32();

        if (pairId != uint.MaxValue)
        {
            Pairs[__instance.id] = pairId;
        }
    }*/
}

public class ApplyRadiantAction : PolibActionBase
{
    public WorldCoordinates Coordinates;
    public ApplyRadiantAction(IntPtr ptr) : base(ptr) {}
    public ApplyRadiantAction() {}
    
    public override bool IsValid(GameState state)
    {
        return true;
    }

    public override ActionType GetActionType()
    {
        return EnumCache<ActionType>.GetType("applyradiantaction");
    }
    
    public override void Execute(GameState state)
    {
        TileData tile = state.Map.GetTile(Coordinates);
        if (tile.unit != null)
        {
            tile.unit.AddEffect(EnumCache<UnitEffect>.GetType("radiant"));
        }
    }

    public override void Serialize(Il2CppSystem.IO.BinaryWriter writer, int version)
    {
        writer.Write(PlayerId); //this line is important btw
        Coordinates.Serialize(writer, version);
    }

    public override void Deserialize(Il2CppSystem.IO.BinaryReader reader, int version)
    {
        PlayerId = reader.ReadByte(); //leave this line in
        Coordinates.Deserialize(reader, version);
    }

    public override string ToString()
    {
        return string.Format("{0} (PlayerId: {1}, Coordinates: {2})", new object[]
        {
            base.GetType(),
            base.PlayerId,
            this.Coordinates
        });
    }
}

public class ApplyRadiantReaction : PolibReactionBase
{
    protected ApplyRadiantAction action;
    public override ActionBase actionProperty 
    { 
        get => this.action; 
        set
        {
            ApplyRadiantAction polibActionBase = value.TryCast<ApplyRadiantAction>();
            if (polibActionBase != null)
            this.action = polibActionBase;
            else
            Main.modLogger.LogInfo("shits fucked");
        } 
    }
    public ApplyRadiantReaction(IntPtr ptr) : base(ptr) {}
    public ApplyRadiantReaction(ApplyRadiantAction action)
    {
        this.action = action;
    }

    public override bool ShouldFocusCamera()
    {
        return IsRecapOrOpponentAction(action);
    }

    public override WorldCoordinates GetCameraFocusCoordinates()
    {
        return action.Coordinates;
    }

    public override void Execute(Il2CppSystem.Action onComplete)
    {
        TileData tile = GameManager.GameState.Map.GetTile(action.Coordinates);
        Tile instance = tile.GetInstance();
        if (instance != null && !instance.IsHidden)
        {
            instance.Render();
            instance.SpawnDarkPuff();
            instance.SpawnEmbers();
            AudioManager.PlaySFXAtTile(SFXTypes.Burn, tile.coordinates);
            GameManager.DelayCall(0.2f, onComplete);
        }
        else
        {
            onComplete.Invoke();
        }
    }
}