using BepInEx;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using System;
using System.Globalization;
using System.Reflection;
using System.Security.Permissions;
using UnityEngine;

[assembly: AssemblyVersion(SlopeWorld.SlopeWorld.Version)]
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace SlopeWorld;

[BepInPlugin(ModID, ModName, Version)]
public sealed partial class SlopeWorld : BaseUnityPlugin
{
    public const string ModID = "knightragu.slopeworld";
    public const string ModName = "Slope World";
    public const string Version = "1.0.0";

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

    private bool modInit;
    private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);

        if (modInit) return;
        modInit = true;

        try
        {
			MachineConnector.SetRegisteredOI(ModID, options);

			// Slope physics changes
            IL.BodyChunk.checkAgainstSlopesVertically += BodyChunk_checkAgainstSlopesVertically;

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
	
	private void BodyChunk_checkAgainstSlopesVertically(ILContext il)
	{
		// System.Reflection.Emit.OpCodes.Br_S
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
				c.Index += 4;

				Log($"Instructs: {c}");

				c.Emit(OpCodes.Ldarg_0);
				c.Emit(OpCodes.Ldloc_S, (byte)4);
				c.EmitDelegate<Action<BodyChunk, int>>((self, num) =>
				{
					// Dune collision code
					
					Vector2 vector = new Vector2(num, 1f).normalized;
					Log($"num is: {num}");
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

					if (options.EnablePatches.Value)
						do {
							try {
								if (self.owner is Player player && player.bodyChunks[0] == self)
									break;
							}
							catch(Exception ex) {
								LogError(ex);
							}
							
							self.vel.y = Mathf.Min(0.0f, self.vel.y); // Fix esliding over slopes, and spear bouncing
						}
						while (false);
					
					self.vel = Vector2.ClampMagnitude(self.vel, magnitude);
				});

				Log($"Instructs aft: {c}");

				c.Emit(OpCodes.Br, instr);

				Log($"Instructs done: {c}");
			}
			else
			{
				LogError($"{il.Method.Name} Il hook match set pos.y failed!");
			}
		}
		else
		{
			LogError($"{il.Method.Name} Il hook match set_onSlope failed!");
		}
	}

    internal static void Log(object msg)
        => Instance.Logger.LogInfo(msg);

    internal static void LogError(object msg)
        => Instance.Logger.LogError(msg);
}
