using BepInEx;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RWCustom;
using SimplifiedMoveset;
using System;
using System.Reflection;
using System.Security.Permissions;
using UnityEngine;
using static Player;
using static Room;

[assembly: AssemblyVersion(SlopeWorld.SlopeWorld.Version)]
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace SlopeWorld;

[BepInPlugin(ModID, ModName, Version)]
public sealed partial class SlopeWorld : BaseUnityPlugin
{
    public const string ModID = "knightragu.slopeworld";
    public const string ModName = "Slope World";
    public const string Version = "1.1.1";

    public static SlopeWorld Instance { get; private set; }
	public static SlopeWorldOptions options { get; private set; }

    public void OnEnable()
    {
        try
        {
            Instance = this;
			options = new();

            On.RainWorld.OnModsInit += RainWorld_OnModsInit;
        }
        
        catch (Exception e)
        {
            Logger.LogError($"Failed to initialize: {e}");
        }
    }

    public void BodyChunkMod_IL_BodyChunk_CheckAgainstSlopesVertically(Action<ILContext> _, ILContext _1)
	{ }

	public static void HookSimplifiedMoveset()
	{
		new Hook(
			typeof(BodyChunkMod).GetMethod("IL_BodyChunk_CheckAgainstSlopesVertically", BindingFlags.NonPublic | BindingFlags.Static),
			delegate(Action<ILContext> _, ILContext _) {}
		);
	} 
	
	private bool preModInit;
    private bool modInit;
    private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
		// Both my IL hooks and simplified moveset's at the same time causes very strange behaviour
		if (!preModInit)
			try {
				if (ModManager.ActiveMods.Exists(m => m.id == "SimplifiedMoveset"))
				{
					HookSimplifiedMoveset();

					preModInit = true;
				}
			}
		catch (Exception e)
		{
			Logger.LogError(e);
		}

       	orig(self);

        if (modInit) return;
        modInit = true;

        try
        {
			MachineConnector.SetRegisteredOI(ModID, options);

			// Slope physics changes
            IL.BodyChunk.checkAgainstSlopesVertically += BodyChunk_checkAgainstSlopesVertically;

			// Make slides work on slopes (thanks to simplified moveset for reference!)
			IL.Player.UpdateAnimation += Player_UpdateAnimation;

			// Always tell all these functions that the onSlope value is 0
			On.Player.Jump += (orig, self) => SpoofOnSlope(orig, self);
			On.Player.UpdateAnimation += (orig, self) => SpoofOnSlope(orig, self);
			On.Player.UpdateBodyMode += (orig, self) => SpoofOnSlope(orig, self);

			// Slope crawlturn patch
			On.Player.MovementUpdate += Player_MovementUpdate;
        }

        catch (Exception e)
        {
            Logger.LogError(e);
        }
    }

	private void SpoofOnSlope(Delegate orig, Player self)
	{
		var chunk0 = self.bodyChunks[0];
		var chunk1 = self.bodyChunks[1];

		(int onSlope0, int onSlope1) = (chunk0.onSlope, chunk1.onSlope);

		chunk0.onSlope = 0;
		chunk1.onSlope = 0;

        orig.Method.Invoke(null, [self]);
		
		chunk0.onSlope = onSlope0;
		chunk1.onSlope = onSlope1;
	}

    private void Player_MovementUpdate(On.Player.orig_MovementUpdate orig, Player self, bool eu)
    {
		var chunk0 = self.bodyChunks[0];
		var chunk1 = self.bodyChunks[1];

		(int onSlope0, int onSlope1) = (chunk0.onSlope, chunk1.onSlope);
		
		// Make crawlturns faster

		if ((options.EnablePatches.Value && onSlope0 != 0) || options.SillyMode.Value)
		{
			var input = self.input[0];
			Vector2 vector = new Vector2(onSlope0, 1f).normalized;

			bool facingRight = chunk0.pos.x - 5f > chunk1.pos.x;

			if (onSlope0 == -1)
				facingRight = !facingRight;

			if (input.x == -onSlope0 && facingRight) {
				chunk0.vel += vector * 5f; 
			}
			else
				chunk0.vel.y = Mathf.Min(0.0f, chunk0.vel.y);
		}

		chunk0.onSlope = 0;
		chunk1.onSlope = 0;

        orig(self, eu);
		
		chunk0.onSlope = onSlope0;
		chunk1.onSlope = onSlope1;
    }
	

	private void Player_UpdateAnimation(ILContext il)
	{
		var c = new ILCursor(il);

		if (c.TryGotoNext(
			x => x.MatchLdsfld<AnimationIndex>("BellySlide"),
			x => x.MatchCall("ExtEnum`1<Player/AnimationIndex>", "op_Equality")
		)) {

			for (int i = 0; i < 2; i++)
				if (c.TryGotoNext(
					x => x.MatchLdarg(0),
					x => x.MatchLdcI4(1 - i),
					x => x.MatchLdcI4(0),
					x => x.MatchLdcI4(-2),
					x => x.MatchCall<PhysicalObject>("IsTileSolid")
				)) {

					var enterIf = c.Prev.Operand;
					c.Goto(c.Index + 5);

					c.Emit(OpCodes.Brtrue_S, enterIf);

					c.Emit(OpCodes.Ldarg_0);
					c.EmitDelegate<Func<Player, bool>>(player =>
					{
						if (!options.EnableSlides.Value || player.room is not Room room) return false;
						
						var chunk0 = player.bodyChunks[0];
						var chunk1 = player.bodyChunks[1];
						
						bool head = IsTileSlope(room, chunk0, 0, -1) || IsTileSlope(room, chunk0, 0, -2);
						bool feet = IsTileSlope(room, chunk1, 0, -1) || IsTileSlope(room, chunk1, 0, -2);

						return head || feet;
					});
				}
				else LogError($"{il.Method.Name}: Il hook failed to match slide stick logic! {i}");
			




			if (c.TryGotoNext(
				x => x.MatchLdarg(0),
				x => x.MatchLdcI4(0),
				x => x.MatchStfld<Player>("standing"),
				x => x.MatchRet()
			)) {
				
				var continueSlide = c.Next;

				if (c.TryGotoPrev(
					x => x.MatchLdarg(0),
					x => x.MatchLdcI4(0),
					x => x.MatchLdcI4(0),
					x => x.MatchLdcI4(-1),
					x => x.MatchCall<PhysicalObject>("IsTileSolid")
				)) {
					
					c.Goto(c.Index + 1);

					Log($"{il.Method.Name} | {c}");

					c.Emit(OpCodes.Ldloc_S, (byte)27);
					c.Emit(OpCodes.Ldloc_S, (byte)28);

					c.EmitDelegate<Func<Player, int, int, bool>>((player, num12, num13) =>
					{
						if (!options.EnableSlides.Value) return false;

						if (player.room is not Room room) return false;
						
						bool head = IsTileSlope(room, player.bodyChunks[0], 0, -1) || IsTileSlope(room, player.bodyChunks[0], 0, -2);
						bool feet = IsTileSlope(room, player.bodyChunks[1], 0, -1) || IsTileSlope(room, player.bodyChunks[0], 0, -2);

						// Log($"Keep sliding: {head || feet}");

						// Not entirely sure what those other conditions mean
						return (head || feet) && !(player.input[0].jmp && !player.input[1].jmp && player.rollCounter > 0 && player.rollCounter < (player.longBellySlide ? num13 : num12));
					});

					c.Emit(OpCodes.Brtrue_S, continueSlide);
					
					c.Emit(OpCodes.Ldarg_0);
				}
				else LogError($"{il.Method.Name}: Il hook failed to match slide end conditions!");
			}
			else Log($"{il.Method.Name}: Il hook failed to match slide end logic!");
		}
		else LogError($"{il.Method.Name}: Il hook failed to match BellySlide logic!");
	}

	public bool IsTileSlope(Room room, BodyChunk chunk, int relative_x, int relative_y)
        => room.GetTile(room.GetTilePosition(chunk.pos) + new IntVector2(relative_x, relative_y)).Terrain == Tile.TerrainType.Slope;


	private void BodyChunk_checkAgainstSlopesVertically(ILContext il)
	{
		ILCursor c = new ILCursor(il);

		if (c.TryGotoNext(
			x => x.MatchLdarg(0),
			x => x.MatchLdloc(4),
			x => x.MatchCall<BodyChunk>("set_onSlope")
		)) {

			var instr = c.Next;

			if (c.TryGotoPrev(
				x => x.MatchLdarg(0),
				x => x.MatchLdfld<BodyChunk>("slopeRad"),
				x => x.MatchAdd(),
				x => x.MatchStfld<Vector2>("y")
			)) {
				c.Goto(c.Index + 4);

				Log($"{il.Method.Name} | {c}");

				c.Emit(OpCodes.Ldarg_0);
				c.Emit(OpCodes.Ldloc_S, (byte)4);
				c.EmitDelegate<Action<BodyChunk, int>>((self, num) =>
				{
					// Dune collision code
					
					Vector2 vector = new Vector2(num, 1f).normalized;
					self.terrainCurveNormal = vector;
					float num5 = -self.vel.y * vector.y;
					if (num5 > self.owner.impactTreshhold)
					{
						self.owner.TerrainImpact(self.index, new IntVector2(0, -1), num5, self.lastContactPoint.y > -1);
					}

					self.contactPoint.y = -1;

					float magnitude = self.vel.magnitude;
					float num6 = self.vel.x * -vector.x / vector.y;

					self.vel.y -= num6;
					self.vel.y = Mathf.Abs(self.vel.y) * self.owner.bounce;

					if (self.vel.y < self.owner.gravity || self.vel.y < 1f + 9f * (1f - self.owner.bounce))
						self.vel.y = 0f;

					self.vel.y += num6;
					self.vel.x *= Mathf.Clamp(self.owner.surfaceFriction * 2f, 0f, 1f);

					// Fix esliding over slopes, and spear bouncing
					if (options.EnablePatches.Value)
						do {
							try {
								if (self.owner is Player player && player.bodyChunks[0] == self)
									break;
							}
							catch(Exception ex) {
								LogError(ex);
							}
							
							self.vel.y = Mathf.Min(0.0f, self.vel.y);
						}
						while (false);
					
					self.vel = Vector2.ClampMagnitude(self.vel, magnitude);
				});

				c.Emit(OpCodes.Br, instr);
			}
			else LogError($"{il.Method.Name} Il hook match set pos.y failed!");
		}
		else LogError($"{il.Method.Name} Il hook match set_onSlope failed!");
	}

    internal static void Log(object msg)
        => Instance.Logger.LogInfo(msg);

    internal static void LogError(object msg)
        => Instance.Logger.LogError(msg);
}
