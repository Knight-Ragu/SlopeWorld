using BepInEx;
using RWCustom;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using UnityEngine;

[assembly: AssemblyVersion(SlopeWorld.SlopeWorld.Version)]
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace SlopeWorld;

[BepInPlugin(ModID, ModName, Version)]
public sealed partial class SlopeWorld : BaseUnityPlugin
{
    public const string ModName = "Slope World";
    public const string ModID = "knightragu.slopeworld";
    public const string Version = "1.0.0";

    public static SlopeWorld Instance { get; private set; }

	private static readonly ConditionalWeakTable<Player, StrongBox<int>> onSlopeValues = new ConditionalWeakTable<Player, StrongBox<int>>();


    public void OnEnable()
    {
        try
        {
            Instance = this;

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
            On.BodyChunk.checkAgainstSlopesVertically += BodyChunk_checkAgainstSlopesVertically;

			On.Player.ctor += Player_ctor;
			On.PlayerGraphics.Update += PlayerGraphics_Update;
        }

        catch (Exception e)
        {
            Logger.LogError(e);
        }
    }

    private void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
    {
		orig(self, abstractCreature, world);

		onSlopeValues.Add(self, new StrongBox<int>(0));
    }

    private void PlayerGraphics_Update(On.PlayerGraphics.orig_Update orig, PlayerGraphics self)
    {	
		if (self.owner is Player player && onSlopeValues.TryGetValue(player, out var onSlope))
		{
			self.owner.bodyChunks[1].onSlope = onSlope.Value;
			orig(self);
			self.owner.bodyChunks[1].onSlope = 0;
		}
		else {
			orig(self);
		}
    }

    private void BodyChunk_checkAgainstSlopesVertically(On.BodyChunk.orig_checkAgainstSlopesVertically orig, BodyChunk self)
    {	
		Player? player = self.owner as Player;
		StrongBox<int>? onSlopeBox = null;
		if (player is not null) onSlopeValues.TryGetValue(player, out onSlopeBox);

		if (player is not null && player.bodyChunks[0] == self) Debug.Log($"{player!.animation}, {player.bodyMode}, {self.vel}");

		if (onSlopeBox is not null)
			onSlopeBox.Value = 0;

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

				// self.onSlope = num;
				

				if (onSlopeBox is not null)
					onSlopeBox.Value = num;

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

				bool headChunk = player is not null && player.bodyChunks[0] == self;
				if (!headChunk) self.vel.y = Mathf.Min(0.0f, self.vel.y); // Fix esliding over slopes, and spear bouncing

				self.vel = Vector2.ClampMagnitude(self.vel, magnitude);


				if (!headChunk || player!.input == null) return;

				var input = player!.input[0];
				
				// Make crawlturns faster

				bool facingRight = player!.bodyChunks[0].pos.x - 5f > player.bodyChunks[1].pos.x;

				if (num == -1)
					facingRight = !facingRight;

				if (input.x == -num && facingRight) {
					self.vel += vector * 5f; 
					Log("Turn");
				}
				else
					self.vel.y = Mathf.Min(0.0f, self.vel.y);
				
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
