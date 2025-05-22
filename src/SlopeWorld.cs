using BepInEx;
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
            On.BodyChunk.checkAgainstSlopesVertically += BodyChunk_checkAgainstSlopesVertically;

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
	

    private void BodyChunk_checkAgainstSlopesVertically(On.BodyChunk.orig_checkAgainstSlopesVertically orig, BodyChunk self)
    {	
        IntVector2 tilePosition = self.owner.room.GetTilePosition(self.pos);
		IntVector2 b = new IntVector2(0, 0);
		Room.SlopeDirection a = self.owner.room.IdentifySlope(self.pos);
		if (self.owner.room.GetTile(self.pos).Terrain != Room.Tile.TerrainType.Slope)
		{
			if (self.owner.room.IdentifySlope(tilePosition.x - 1, tilePosition.y) != Room.SlopeDirection.Broken && self.pos.x - self.slopeRad <= self.owner.room.MiddleOfTile(self.pos).x - 10f)
			{
				a = self.owner.room.IdentifySlope(tilePosition.x - 1, tilePosition.y);
				b.x = -1;
			}
			else if (self.owner.room.IdentifySlope(tilePosition.x + 1, tilePosition.y) != Room.SlopeDirection.Broken && self.pos.x + self.slopeRad >= self.owner.room.MiddleOfTile(self.pos).x + 10f)
			{
				a = self.owner.room.IdentifySlope(tilePosition.x + 1, tilePosition.y);
				b.x = 1;
			}
			else if (self.pos.y - self.slopeRad < self.owner.room.MiddleOfTile(self.pos).y - 10f)
			{
				if (self.owner.room.IdentifySlope(tilePosition.x, tilePosition.y - 1) != Room.SlopeDirection.Broken)
				{
					a = self.owner.room.IdentifySlope(tilePosition.x, tilePosition.y - 1);
					b.y = -1;
				}
			}
			else if (self.pos.y + self.slopeRad > self.owner.room.MiddleOfTile(self.pos).y + 10f && self.owner.room.IdentifySlope(tilePosition.x, tilePosition.y + 1) != Room.SlopeDirection.Broken)
			{
				a = self.owner.room.IdentifySlope(tilePosition.x, tilePosition.y + 1);
				b.y = 1;
			}
		}
		if (a != Room.SlopeDirection.Broken)
		{
			Vector2 vector = self.owner.room.MiddleOfTile(self.owner.room.GetTilePosition(self.pos) + b);
			int num = 0;
			float num2;
			int num3;
			if (a == Room.SlopeDirection.UpLeft)
			{
				num = -1;
				num2 = self.pos.x - (vector.x - 10f) + (vector.y - 10f);
				num3 = -1;
			}
			else if (a == Room.SlopeDirection.UpRight)
			{
				num = 1;
				num2 = 20f - (self.pos.x - (vector.x - 10f)) + (vector.y - 10f);
				num3 = -1;
			}
			else if (a == Room.SlopeDirection.DownLeft)
			{
				num2 = 20f - (self.pos.x - (vector.x - 10f)) + (vector.y - 10f);
				num3 = 1;
			}
			else
			{
				num2 = self.pos.x - (vector.x - 10f) + (vector.y - 10f);
				num3 = 1;
			}
			if (num3 == -1 && self.pos.y <= num2 + self.slopeRad + self.slopeRad)
			{
				self.pos.y = num2 + self.slopeRad + self.slopeRad;
				// self.contactPoint.y = -1;
				// self.vel.x *= (1f - self.owner.surfaceFriction);
				// self.vel.x += Mathf.Abs(self.vel.y) * Mathf.Clamp(0.5f - self.owner.surfaceFriction, 0f, 0.5f) * (float)num * 0.2f;
				// self.vel.y = 0f;

				self.onSlope = num;

				self.slopeRad = self.TerrainRad - 1f;

				// Dune collision code
				
				vector = new Vector2(num, 1f).normalized;
				self.terrainCurveNormal = vector;
				float num5 = -self.vel.y * vector.y;
				if (num5 > self.owner.impactTreshhold)
				{
					self.owner.TerrainImpact(self.index, new IntVector2(0, -1), num5, self.lastContactPoint.y > -1);
				}
				if (self.terrainCurveNormal.y < TerrainCurve.maxSlideNormalY)
				{
					self.contactPoint.y = 0;
					self.vel -= vector * Mathf.Min(0f, Vector2.Dot(self.vel, vector) * (1f + self.owner.bounce * 0.2f));
					Vector2 vector3 = new Vector2(-vector.y, vector.x);
					self.vel -= Vector2.Dot(self.vel, vector3) * Mathf.Clamp01(1f - self.owner.surfaceFriction * 2f) * vector3;
					return;
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

				do {
					try {
						if (self.owner is Player player && player.bodyChunks[0] == self)
							break;
					}
					catch(Exception ex) {
						LogError(ex);
					}
					
					if (options.EnablePatches.Value) self.vel.y = Mathf.Min(0.0f, self.vel.y); // Fix eslides over slopes, and spear bouncing
				}
				while (false);
				
				self.vel = Vector2.ClampMagnitude(self.vel, magnitude);
				
				return;
			} 

			if (num3 == 1 && self.pos.y >= num2 - self.slopeRad - self.slopeRad)
			{
				self.pos.y = num2 - self.slopeRad - self.slopeRad;
				self.contactPoint.y = 1;
				self.vel.y = 0f;
				self.vel.x *= 1f - self.owner.surfaceFriction;
				self.slopeRad = self.TerrainRad - 1f;
			}
		}
    }

    internal static void Log(object msg)
        => Instance.Logger.LogInfo(msg);

    internal static void LogError(object msg)
        => Instance.Logger.LogError(msg);
}
