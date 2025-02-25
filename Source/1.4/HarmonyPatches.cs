﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.Sound;
using HarmonyLib;
using System.Text;
using System.Reflection.Emit;
using UnityEngine;
using Verse.AI.Group;
using RimWorld.QuestGen;
using System.Collections;
using RimworldMod;

namespace SaveOurShip2
{
	//GUI
	[HarmonyPatch(typeof(ColonistBar), "ColonistBarOnGUI")]
	public static class ShipCombatOnGUI
	{
		public static void Postfix(ColonistBar __instance)
		{
			Map mapPlayer = null;
			ShipHeatMapComp playerShipComp = null;
			var list = AccessExtensions.Utility.shipHeatMapCompCache;
			for (int i = list.Count; i-- > 0;) //find player map, comp
			{
				playerShipComp = list[i];
				if (playerShipComp.InCombat && playerShipComp.ShipCombatOrigin)
				{
					mapPlayer = playerShipComp.map;
					break;
				}
            }
            if (mapPlayer == null)
            {
                if (!ModSettings_SoS.persistShipUI)
                    return;
                for (int i = list.Count; i-- > 0;) //try find ship map OOC
                {
                    playerShipComp = list[i];
                    if (playerShipComp.map.IsPlayerHome && playerShipComp.map.IsSpace())
                    {
                        mapPlayer = playerShipComp.map;
                        break;
                    }
                }
                if (mapPlayer == null)
                {
                    return;
                }
            }
            if (playerShipComp.ShipsOnMapNew.NullOrEmpty() || playerShipComp.ShipsOnMapNew.All(sc => sc.Value?.IsWreck ?? true))
                return;
            if (!playerShipComp.InCombat && playerShipComp.IsGraveyard)
            {
                Map m = playerShipComp.ShipGraveyard;
                playerShipComp = m.GetComponent<ShipHeatMapComp>();
            }
            float screenHalf = (float)UI.screenWidth / 2 + ModSettings_SoS.offsetUIx - 200;
            //player heat & energy bars
            float baseY = __instance.Size.y + 40 + ModSettings_SoS.offsetUIy;
            foreach (int i in playerShipComp.ShipsOnMapNew.Keys)
            {
                var bridge = playerShipComp.ShipsOnMapNew[i].Core;
                if (bridge == null)
                    continue;

                baseY += 45;
                string str = bridge.ShipName;
                int strSize = 0;
                if (playerShipComp.ShipsOnMapNew.Count > 1)
                {
                    strSize = 5 + str.Length * 8;
                }
                Rect rect2 = new Rect(screenHalf - 430 - strSize, baseY - 40, 395 + strSize, 35);
                Widgets.DrawMenuSection(rect2);
                if (playerShipComp.ShipsOnMapNew.Count > 1)
                    Widgets.Label(rect2.ContractedBy(7), str);

                DrawPower(screenHalf - 220, baseY, bridge);
                DrawHeat(screenHalf - 415, baseY, bridge);
            }
            //no UI OOC bellow
            var enemyShipComp = playerShipComp.TargetMapComp;
            if (enemyShipComp == null || !enemyShipComp.InCombat)
                return;
            //enemy heat & energy bars
            baseY = __instance.Size.y + 40 + ModSettings_SoS.offsetUIy;
            foreach (int i in enemyShipComp.ShipsOnMapNew.Keys)
            {
                var bridge = enemyShipComp.ShipsOnMapNew[i].Core;
                if (bridge == null || bridge.powerComp?.PowerNet == null || bridge.heatComp.myNet == null)
                    continue;

                baseY += 45;
                Rect rect2 = new Rect(screenHalf + 435, baseY - 40, 395, 35);
                Widgets.DrawMenuSection(rect2);

                DrawHeat(screenHalf + 455, baseY, bridge);
                DrawPower(screenHalf + 645, baseY, bridge);
            }

            //range bar
            baseY = __instance.Size.y + 85 + ModSettings_SoS.offsetUIy;
            Rect rect = new Rect(screenHalf - 25, baseY - 40, 450, 50);
            Widgets.DrawMenuSection(rect);
            Widgets.DrawTexturePart(new Rect(screenHalf, baseY - 38, 400, 46),
                new Rect(0, 0, 1, 1), (Texture2D)ResourceBank.ruler.MatSingle.mainTexture);
			float range = playerShipComp.Range;
            switch (playerShipComp.Heading)
            {
                case -1:
                    Verse.Widgets.DrawTexturePart(new Rect(screenHalf - 23, baseY - 28, 36, 36),
                        new Rect(0, 0, 1, 1), (Texture2D)ResourceBank.shipOne.MatSingle.mainTexture);
                    break;
                case 1:
                    Verse.Widgets.DrawTexturePart(new Rect(screenHalf - 35, baseY - 28, 36, 36),
                        new Rect(0, 0, -1, 1), (Texture2D)ResourceBank.shipOne.MatSingle.mainTexture);
                    break;
                default:
                    Verse.Widgets.DrawTexturePart(new Rect(screenHalf - 35, baseY - 28, 36, 36),
                        new Rect(0, 0, -1, 1), (Texture2D)ResourceBank.shipZero.MatSingle.mainTexture);
                    break;
            }
            switch (enemyShipComp.Heading)
            {
                case -1:
                    Verse.Widgets.DrawTexturePart(
                        new Rect(screenHalf - 16 + range, baseY - 28, 36, 36),
                        new Rect(0, 0, -1, 1), (Texture2D)ResourceBank.shipOneEnemy.MatSingle.mainTexture);
                    break;
                case 1:
                    Verse.Widgets.DrawTexturePart(
                        new Rect(screenHalf + range, baseY - 28, 36, 36),
                        new Rect(0, 0, 1, 1), (Texture2D)ResourceBank.shipOneEnemy.MatSingle.mainTexture);
                    break;
                default:
                    Verse.Widgets.DrawTexturePart(
                        new Rect(screenHalf + range, baseY - 28, 36, 36),
                        new Rect(0, 0, 1, 1), (Texture2D)ResourceBank.shipZeroEnemy.MatSingle.mainTexture);
                    break;
            }
            foreach (ShipCombatProjectile proj in playerShipComp.Projectiles)
            {
                if (proj.turret != null)
                {
                    Verse.Widgets.DrawTexturePart(
                        new Rect(screenHalf - 10 + proj.range, baseY - 12, 12, 12),
                        new Rect(0, 0, 1, 1), (Texture2D)ResourceBank.projectile.MatSingle.mainTexture);
                }
            }
            foreach (ShipCombatProjectile proj in enemyShipComp.Projectiles)
            {
                if (proj.turret != null)
                {
                    Verse.Widgets.DrawTexturePart(
                        new Rect(screenHalf - 10 - proj.range + range, baseY - 24, 12, 12),
                        new Rect(0, 0, -1, 1), (Texture2D)ResourceBank.projectileEnemy.MatSingle.mainTexture);
                }
            }
            foreach (TravelingTransportPods obj in Find.WorldObjects.TravelingTransportPods)
            {
                float rng = (float)Traverse.Create(obj).Field("traveledPct").GetValue();
                int initialTile = (int)Traverse.Create(obj).Field("initialTile").GetValue();
                if (obj.destinationTile == playerShipComp.ShipCombatTargetMap.Tile && initialTile == mapPlayer.Tile)
                {
                    Verse.Widgets.DrawTexturePart(
                        new Rect(screenHalf + rng * range, baseY - 16, 12, 12),
                        new Rect(0, 0, 1, 1), (Texture2D)ResourceBank.shuttlePlayer.MatSingle.mainTexture);
                }
                else if (obj.destinationTile == mapPlayer.Tile && initialTile == playerShipComp.ShipCombatTargetMap.Tile && obj.Faction != Faction.OfPlayer)
                {
                    Verse.Widgets.DrawTexturePart(
                        new Rect(screenHalf + (1 - rng) * range, baseY - 20, 12, 12),
                        new Rect(0, 0, -1, 1), (Texture2D)ResourceBank.shuttleEnemy.MatSingle.mainTexture);
                }
                else if (obj.destinationTile == mapPlayer.Tile && initialTile == playerShipComp.ShipCombatTargetMap.Tile && obj.Faction == Faction.OfPlayer)
                {
                    Verse.Widgets.DrawTexturePart(
                        new Rect(screenHalf + (1 - rng) * range, baseY - 20, 12, 12),
                        new Rect(0, 0, -1, 1), (Texture2D)ResourceBank.shuttlePlayer.MatSingle.mainTexture);
                }
            }
            if (Mouse.IsOver(rect))
            {
                string iconTooltipText = TranslatorFormattedStringExtensions.Translate("ShipCombatTooltip");
                if (!iconTooltipText.NullOrEmpty())
                {
                    TooltipHandler.TipRegion(rect, iconTooltipText);
                }
            }
        }
        private static void DrawPower(float offset, float baseY, Building_ShipBridge bridge)
        {
            Rect rect = new Rect(offset - 15, baseY - 40, 200, 35);
            Widgets.FillableBar(rect.ContractedBy(6), bridge.powerRat, ResourceBank.PowerTex);
            Text.Font = GameFont.Small;
            rect.y += 7;
            rect.x = offset;
            rect.height = Text.LineHeight;
            if (bridge.powerCap > 0)
                Widgets.Label(rect, "Energy: " + bridge.power + " / " + bridge.powerCap);
            else
                Widgets.Label(rect, "<color=red>Energy: N/A</color>");
        }
        private static void DrawHeat(float offset, float baseY, Building_ShipBridge bridge)
        {
            Rect rect = new Rect(offset - 15, baseY - 40, 200, 35);
            FillableBarWithDepletion(rect.ContractedBy(6), bridge.heatRat, bridge.heatRatDep, ResourceBank.HeatTex, ResourceBank.DepletionTex);
            rect.y += 7;
            rect.x = offset;
            rect.height = Text.LineHeight;
            if (bridge.heatCap > 0)
                Widgets.Label(rect, "Heat: " + Mathf.Floor(bridge.heat) + " / " + bridge.heatCap);
            else
                Widgets.Label(rect, "<color=red>Heat: N/A</color>");
        }
        public static Rect FillableBarWithDepletion(Rect rect, float fillPercent, float fillDepletion, Texture2D fillTex, Texture2D depletionTex)
        {
            bool doBorder = rect.height > 15f && rect.width > 20f;
            if (doBorder)
            {
                GUI.DrawTexture(rect, BaseContent.BlackTex);
                rect = rect.ContractedBy(3f);
            }
            Rect heatRect = new Rect(rect);
            heatRect.width *= fillPercent;
            GUI.DrawTexture(heatRect, fillTex);
            Rect depletionRect = new Rect(rect);
            depletionRect.width *= fillDepletion;
            depletionRect.x = rect.x + rect.width * (1 - fillDepletion);
            GUI.DrawTexture(depletionRect, depletionTex);
            return rect;
        }
    }

	[HarmonyPatch(typeof(ColonistBarColonistDrawer), "DrawGroupFrame")]
	public static class ShipIconOnPawnBar
	{
		public static void Postfix(int group, ColonistBarColonistDrawer __instance)
		{
			List<ColonistBar.Entry> entries = Find.ColonistBar.Entries;
			var length = entries.Count;
			for (int i = 0; i < length; i++)
			{
				ColonistBar.Entry entry = entries[i];
				if (entry.group == group && entry.pawn == null && entry.map.IsSpace())
				{
					Rect rect = __instance.GroupFrameRect(group);
					var mapComp = entry.map.GetComponent<ShipHeatMapComp>();
					if (mapComp.IsGraveyard) //wreck
						Verse.Widgets.DrawTextureFitted(rect, ResourceBank.shipBarNeutral.MatSingle.mainTexture, 1);
					else if (entry.map.ParentFaction == Faction.OfPlayer)//player
						Verse.Widgets.DrawTextureFitted(rect, ResourceBank.shipBarPlayer.MatSingle.mainTexture, 1);
					else //enemy
						Verse.Widgets.DrawTextureFitted(rect, ResourceBank.shipBarEnemy.MatSingle.mainTexture, 1);
				}
			}
		}
	}

	[HarmonyPatch(typeof(LetterStack), "LettersOnGUI")] //add burnup timer
	public static class TimerOnGUI
	{
		public static bool Prefix(ref float baseY)
		{
			Map map = Find.CurrentMap;
			if (map != null && map.IsSpace())
			{
				var timecomp = map.Parent.GetComponent<TimedForcedExitShip>();
				if (timecomp != null && timecomp.ForceExitAndRemoveMapCountdownActive)
				{
					float num = (float)UI.screenWidth - 200f;
					Rect rect = new Rect(num, baseY - 16f, 193f, 26f);
					Text.Anchor = TextAnchor.MiddleRight;
					string detectionCountdownTimeLeftString = timecomp.ForceExitAndRemoveMapCountdownTimeLeftString;
					string text;
                    if (timecomp.ticksLeftToForceExitAndRemoveMap < 5000)
                        text = "ShipBurnUpCountdown".Translate(detectionCountdownTimeLeftString).Colorize(Color.red);
                    else if (timecomp.ticksLeftToForceExitAndRemoveMap < 30000)
                        text = "ShipBurnUpCountdown".Translate(detectionCountdownTimeLeftString).Colorize(Color.yellow);
                    else
                        text = "ShipBurnUpCountdown".Translate(detectionCountdownTimeLeftString);
                    float x = Text.CalcSize(text).x;
					Rect rect2 = new Rect(rect.xMax - x, rect.y, x, rect.height);
					if (Mouse.IsOver(rect2))
					{
						Widgets.DrawHighlight(rect2);
					}
					TooltipHandler.TipRegionByKey(rect2, "ShipBurnUpCountdownTip", detectionCountdownTimeLeftString);
					Widgets.Label(rect2, text);
					Text.Anchor = TextAnchor.UpperLeft;
					baseY -= 26f;
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(GlobalControls), "TemperatureString")] //add breach and breathability info to UI
	public static class ShowBreathability
	{
		public static void Postfix(ref string __result)
		{
			Map map = Find.CurrentMap;
			if (!map.IsSpace()) return;

			if (ShipInteriorMod2.ExposedToOutside(UI.MouseCell().GetRoom(map)))
			{
				if (__result.StartsWith("IndoorsUnroofed".Translate() + " (1)"))
                {
					__result = "Breach detected!".Colorize(Color.red) + __result.Remove(0, "IndoorsUnroofed".Translate().Length + 4);
				}
				__result += " (Vacuum)";
			}
			else
			{
				if (map.GetComponent<ShipHeatMapComp>().VecHasLS(UI.MouseCell()))
                    __result += " (Breathable Atmosphere)";
				else
					__result += " (Non-Breathable Atmosphere)".Colorize(Color.yellow);
			}
		}
	}

	//biome
	[HarmonyPatch(typeof(MapDrawer), "DrawMapMesh", null)]
	public static class RenderPlanetBehindMap
	{
		public const float altitude = 1100f;
		public static void Prefix()
		{
			var worldComp = ShipInteriorMod2.WorldComp;

			// if we aren't in space, abort!
			if ((worldComp.renderedThatAlready && !ModSettings_SoS.renderPlanet) || !Find.CurrentMap.IsSpace())
			{
				return;
			}
			var camera = Find.WorldCamera;
			//TODO replace this when interplanetary travel is ready
			//Find.PlaySettings.showWorldFeatures = false;
			RenderTexture oldTexture = camera.targetTexture;
			RenderTexture oldSkyboxTexture = WorldCameraManager.WorldSkyboxCamera.targetTexture;
			var worldRender = Find.World.renderer;
			var cameraDriver = Find.WorldCameraDriver;
			worldRender.wantedMode = WorldRenderMode.Planet;
			cameraDriver.JumpTo(Find.CurrentMap.Tile);
			cameraDriver.altitude = altitude;
			cameraDriver.desiredAltitude = altitude;
			cameraDriver.Update();
			worldRender.CheckActivateWorldCamera();
			worldRender.DrawWorldLayers();
			WorldRendererUtility.UpdateWorldShadersParams();
			//TODO replace this when interplanetary travel is ready
			/*
            foreach(WorldLayer layer in Find.World.renderer.layers)
            {
                if (layer is WorldLayer_Stars)
                    layer.Render();
            }
            Find.PlaySettings.showWorldFeatures = false;*/
			WorldCameraManager.WorldSkyboxCamera.targetTexture = ResourceBank.target;
			float num = (float)UI.screenWidth / (float)UI.screenHeight;
			WorldCameraManager.WorldSkyboxCamera.aspect = num;
			WorldCameraManager.WorldSkyboxCamera.Render();

			camera.targetTexture = ResourceBank.target;
			camera.aspect = num;
			camera.Render();

			RenderTexture.active = ResourceBank.target;
			ResourceBank.virtualPhoto.ReadPixels(new Rect(0, 0, 2048, 2048), 0, 0);
			ResourceBank.virtualPhoto.Apply();
			RenderTexture.active = null;

			camera.targetTexture = oldTexture;
			WorldCameraManager.WorldSkyboxCamera.targetTexture = oldSkyboxTexture;
			worldRender.wantedMode = WorldRenderMode.None;
			worldRender.CheckActivateWorldCamera();

			if (!worldRender.layers.FirstOrFallback().ShouldRegenerate)
				worldComp.renderedThatAlready = true;
		}
	}

	[HarmonyPatch(typeof(SectionLayer), "FinalizeMesh", null)]
	public static class GenerateSpaceSubMesh
	{
		public static bool Prefix(SectionLayer __instance, Section ___section)
		{
			if (__instance.GetType().Name != "SectionLayer_Terrain")
				return true;

			bool foundSpace = false;
			foreach (IntVec3 cell in ___section.CellRect.Cells)
			{
				TerrainDef terrain1 = ___section.map.terrainGrid.TerrainAt(cell);
				if (terrain1 == ResourceBank.TerrainDefOf.EmptySpace)
				{
					foundSpace = true;
					Printer_Mesh.PrintMesh(__instance, Matrix4x4.TRS(cell.ToVector3() + new Vector3(0.5f, 0f, 0.5f), Quaternion.identity, Vector3.one), MeshMakerPlanes.NewPlaneMesh(1f), ResourceBank.PlanetMaterial);
				}
			}
			if (!foundSpace)
			{
				for (int i = 0; i < __instance.subMeshes.Count; i++)
				{
					if (__instance.subMeshes[i].material == ResourceBank.PlanetMaterial)
					{
						__instance.subMeshes.RemoveAt(i);
					}
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Map), "Biome", MethodType.Getter)]
	public static class SpaceBiomeGetter
	{
		public static bool Prefix(Map __instance, out bool __state)
		{
			__state = __instance.info?.parent != null &&
						   (__instance.info.parent is WorldObjectOrbitingShip || __instance.info.parent is SpaceSite || __instance.info.parent is MoonBase || __instance.Parent.AllComps.Any(comp => comp is MoonPillarSiteComp));
			return !__state;
		}
		public static void Postfix(Map __instance, ref BiomeDef __result, bool __state)
		{
			if (__state)
				__result = ResourceBank.BiomeDefOf.OuterSpaceBiome;
		}
	}

	[HarmonyPatch(typeof(MapTemperature), "OutdoorTemp", MethodType.Getter)]
	public static class ForceOutdoorTempInSpace
	{
		public static void Postfix(ref float __result, Map ___map)
		{
			if (___map.IsSpace()) __result = -100f;
		}
	}

	[HarmonyPatch(typeof(MapTemperature), "SeasonalTemp", MethodType.Getter)]
    public static class ForceSeasonalTempInSpace
    {
		public static void Postfix(ref float __result, Map ___map)
		{
			if (___map.IsSpace()) __result = -100f;
		}
	}

	[HarmonyPatch(typeof(Room), "OpenRoofCount", MethodType.Getter)] //set to 1 if in space and missing roof/ship hull
	public static class SpaceRoomCheck
	{
		public static bool Prefix(ref int ___cachedOpenRoofCount, out bool __state)
		{
			__state = false;
			if (___cachedOpenRoofCount == -1)
				__state = true;
			return true;
		}
		public static int Postfix(int __result, Room __instance, ref int ___cachedOpenRoofCount, bool __state)
		{
			if (__state && __result == 0 && __instance.Map.IsSpace() && !__instance.TouchesMapEdge && !__instance.IsDoorway)
			{
				foreach (IntVec3 tile in __instance.Cells)
				{
					var roof = tile.GetRoof(__instance.Map);
					if (!ShipInteriorMod2.IsRoofDefAirtight(roof))
					{
						___cachedOpenRoofCount = 1;
						return ___cachedOpenRoofCount;
					}
				}
				foreach (IntVec3 vec in __instance.BorderCells)
				{
					bool hasShipPart = false;
					foreach (Thing t in vec.GetThingList(__instance.Map))
					{
						if (t is Building b)
						{
							var shipPart = b.TryGetComp<CompSoShipPart>();
							if (b.def.mineable || (shipPart != null && shipPart.Props.hermetic))
							{
								hasShipPart = true;
								break;
							}
						}
					}
					if (!hasShipPart)
					{
						___cachedOpenRoofCount = 1;
						return ___cachedOpenRoofCount;
					}
				}
			}
			return ___cachedOpenRoofCount;
		}
	}

	[HarmonyPatch(typeof(GenTemperature), "EqualizeTemperaturesThroughBuilding")] //block vents and open airlocks in vac, closed airlocks vent slower
    public static class NoVentingToSpace
	{
		public static bool Prefix(Building b, ref float rate, bool twoWay)
		{
			if (!b.Map.IsSpace())
				return true;
			if (twoWay) //vent
			{
				IntVec3 vec = b.Position + b.Rotation.FacingCell;
				Room room = vec.GetRoom(b.Map);
				if (ShipInteriorMod2.ExposedToOutside(room))
				{
					return false;
				}
				vec = b.Position - b.Rotation.FacingCell;
				room = vec.GetRoom(b.Map);
				if (ShipInteriorMod2.ExposedToOutside(room))
				{
					return false;
				}
				return true;
			}
			if (b is Building_ShipAirlock a)
			{
				if (a.Open && a.Outerdoor())
					return false;
				else
					rate = 0.5f;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(RoomTempTracker), "EqualizeTemperature")]
	public static class ExposedToVacuum
	{
		public static void Postfix(RoomTempTracker __instance, ref Room ___room)
		{
			if (___room.Map.terrainGrid.TerrainAt(IntVec3.Zero) != ResourceBank.TerrainDefOf.EmptySpace)
				return;
			if (___room.Role != RoomRoleDefOf.None && ___room.OpenRoofCount > 0)
				__instance.Temperature = -100f;
		}
	}

	[HarmonyPatch(typeof(RoomTempTracker), "WallEqualizationTempChangePerInterval")]
	public static class TemperatureDoesntDiffuseFastInSpace
	{
		public static void Postfix(ref float __result, Room ___room)
		{
			if (___room.Map.IsSpace())
			{
				__result *= 0.01f;
			}
		}
	}

	[HarmonyPatch(typeof(RoomTempTracker), "ThinRoofEqualizationTempChangePerInterval")]
	public static class TemperatureDoesntDiffuseFastInSpaceToo
	{
		public static void Postfix(ref float __result, Room ___room)
		{
			if (___room.Map.IsSpace())
			{
				__result *= 0.01f;
			}
		}
	}

	[HarmonyPatch(typeof(Fire), "DoComplexCalcs")]
	public static class CannotBurnInSpace
	{
		public static void Postfix(Fire __instance)
		{
			if (!(__instance is MechaniteFire) && __instance.Spawned && __instance.Map.IsSpace())
			{
				Room room = __instance.Position.GetRoom(__instance.Map);
				if (ShipInteriorMod2.ExposedToOutside(room))
					__instance.TakeDamage(new DamageInfo(DamageDefOf.Extinguish, 100, 0, -1f, null, null, null,
						DamageInfo.SourceCategory.ThingOrUnknown, null));
			}
		}
	}

	[HarmonyPatch(typeof(PenFoodCalculator), "ProcessTerrain")]
	public static class SpaceHasNoWildPlants
	{
		public static bool Prefix(PenFoodCalculator __instance, IntVec3 c, Map map)
		{
			if (map.IsSpace())
			{
				__instance.numCells++;
				MapPastureNutritionCalculator.NutritionPerDayPerQuadrum other = new MapPastureNutritionCalculator.NutritionPerDayPerQuadrum();
				other.quadrum[0] = 0;
				other.quadrum[1] = 0;
				other.quadrum[2] = 0;
				other.quadrum[3] = 0;
				__instance.nutritionPerDayPerQuadrum.AddFrom(other);
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Plant), "TickLong")]
	public static class KillPlantsInSpace
	{
		public static void Postfix(Plant __instance)
		{
			if (__instance.Spawned && __instance.Map.IsSpace())
			{
				if (ShipInteriorMod2.AirlockBugFlag)
					return;
				Room room = __instance.Position.GetRoom(__instance.Map);
				if (ShipInteriorMod2.ExposedToOutside(room))
				{
					__instance.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, 10, 0, -1f, null, null, null,
						DamageInfo.SourceCategory.ThingOrUnknown, null));
				}
			}
		}
	}

	[HarmonyPatch(typeof(Plant), "MakeLeafless")]
	public static class DoNotKillPlantsOnMove
	{
		public static bool Prefix()
		{
			if (ShipInteriorMod2.AirlockBugFlag)
			{
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(PollutionGrid), "SetPolluted")]
	public static class DoNotPolluteSpace
	{
		public static bool Prefix(IntVec3 cell, Map ___map)
		{
			if (___map.terrainGrid.TerrainAt(cell) == ResourceBank.TerrainDefOf.EmptySpace)
			{
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(WeatherManager), "TransitionTo")]
	public static class SpaceWeatherStays
	{
		public static bool Prefix(WeatherManager __instance)
		{
			if (__instance.map.IsSpace() && __instance.curWeather == ResourceBank.WeatherDefOf.OuterSpaceWeather)
			{
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(WeatherDecider), "StartNextWeather")]
	public static class SpaceWeatherStaysTwo
	{
		public static bool Prefix(WeatherManager __instance)
		{
			if (__instance.map.IsSpace() && __instance.curWeather == ResourceBank.WeatherDefOf.OuterSpaceWeather)
			{
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(JoyUtility), "EnjoyableOutsideNow", new Type[] { typeof(Map), typeof(StringBuilder) })]
	public static class NoNatureRunningInSpace
	{
		public static void Postfix(Map map, ref bool __result)
		{
			if (map.IsSpace())
			{
				__result = false;
			}
		}
	}

	//map
	[HarmonyPatch(typeof(CompShipPart), "CompGetGizmosExtra")]
	public static class NoGizmoInSpace
	{
		public static bool Prefix(CompShipPart __instance, out bool __state)
		{
			__state = false;
			if (__instance.parent.Map != null && __instance.parent.Map.IsSpace())
			{
				__state = true;
				return false;
			}
			return true;
		}
		public static void Postfix(ref IEnumerable<Gizmo> __result, bool __state)
		{
			if (__state)
				__result = new List<Gizmo>();
		}
	}

	[HarmonyPatch(typeof(SettleInExistingMapUtility), "SettleCommand")]
	public static class NoSpaceSettle
	{
		public static void Postfix(Command __result, Map map)
		{
			if (map.IsSpace())
			{
				__result.disabled = true;
				__result.disabledReason = "Cannot settle space sites";
			}
		}
	}

	[HarmonyPatch(typeof(Building), "ClaimableBy")]
	public static class NoClaimingEnemyShip //prevent claiming when all enemy pawns are dead but bridges exist
	{
		public static void Postfix(Building __instance, ref bool __result)
		{
			if (__instance.Map.IsSpace() && __instance.Map.GetComponent<ShipHeatMapComp>().MapRootListAll.Any())
				__result = false;
		}
	}

	[HarmonyPatch(typeof(MapDeiniter), "Deinit_NewTemp")]
	public static class RemoveSpaceMap
	{
		public static void Postfix()
		{
			AccessExtensions.Utility.RecacheSpaceMaps();
		}
	}

	[HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), "CanFireNowSub")]
	public static class NoTradersInSpace
	{
		public static void Postfix(IncidentParms parms, ref bool __result)
		{
			if (parms.target != null && parms.target is Map map && map.IsSpace()) __result = false;
		}
	}

	[HarmonyPatch(typeof(ExitMapGrid), "MapUsesExitGrid", MethodType.Getter)]
	public static class InSpaceNoOneCanHearYouRunAway
	{
		public static void Postfix(Map ___map, ref bool __result)
		{
			if (___map.IsSpace()) __result = false;
		}
	}

	/*[HarmonyPatch(typeof(TileFinder), "TryFindNewSiteTile")] //changed destructive patch, unsure if this is even needed anymore
	public static class NoQuestsNearTileZero
	{
		public static bool Prefix(out int tile, int minDist, int maxDist, bool allowCaravans,
            TileFinderMode tileFinderMode, int nearThisTile, ref bool __result)
        {
			tile = -1;
            if (ShipInteriorMod2.FindPlayerShipMap() == null)
				return true;

            Func<int, int> findTile = delegate (int root) {
                int minDist2 = minDist;
                int maxDist2 = maxDist;
                Predicate<int> validator = (int x) =>
                    !Find.WorldObjects.AnyWorldObjectAt(x) && TileFinder.IsValidTileForNewSettlement(x, null);
                int result;
                if (TileFinder.TryFindPassableTileWithTraversalDistance(root, minDist2, maxDist2, out result,
                    validator: validator, ignoreFirstTilePassability: false, tileFinderMode, false))
                {
                    return result;
                }

                return -1;
            };
            int arg;
            if (nearThisTile != -1)
            {
                arg = nearThisTile;
            }
            else if (!TileFinder.TryFindRandomPlayerTile(out arg, allowCaravans,
                (int x) => findTile(x) != -1 && (Find.World.worldObjects.MapParentAt(x) == null ||
                                                 !(Find.World.worldObjects.MapParentAt(x) is WorldObjectOrbitingShip))))
            {
                tile = -1;
                __result = false;
                return false;
            }

            tile = findTile(arg);
            __result = (tile != -1);
			return false;
        }
	}*/

	[HarmonyPatch(typeof(RCellFinder), "TryFindRandomExitSpot")]
	public static class NoPrisonBreaksInSpace
	{
		public static void Postfix(Pawn pawn, ref bool __result)
		{
			if (pawn.Map.IsSpace()) __result = false;
		}
	}
    /*HarmonyPatch(typeof(PrisonBreakUtility), "StartPrisonBreak")] //td change to breach doors, find weapons. hack bridge in space
    public static class PrisonBreaksInSpace
	{
		
	}*/

    [HarmonyPatch(typeof(RoofCollapseCellsFinder), "ConnectsToRoofHolder")]
	public static class NoRoofCollapseInSpace
	{
		public static void Postfix(ref bool __result, Map map)
		{
			if (map.IsSpace()) __result = true;
		}
	}

	[HarmonyPatch(typeof(RoofCollapseUtility), "WithinRangeOfRoofHolder")]
	public static class NoRoofCollapseInSpace2
	{
		public static void Postfix(ref bool __result, Map map)
		{
			if (map.IsSpace()) __result = true;
		}
	}

	[HarmonyPatch(typeof(FogGrid), "FloodUnfogAdjacent")]
	public static class NoFogSpamInSpace
	{
		public static bool Prefix(Map ___map, out bool __state)
		{
			__state = false;
			if (___map != null && ___map.IsSpace())
			{
				__state = true;
				return false;
			}
			return true;
		}
		public static void Postfix(FogGrid __instance, Map ___map, IntVec3 c, bool __state)
		{
			if (__state)
			{
				__instance.Unfog(c);
				for (int i = 0; i < 4; i++)
				{
					IntVec3 intVec = c + GenAdj.CardinalDirections[i];
					if (intVec.InBounds(___map) && intVec.Fogged(___map))
					{
						Building edifice = intVec.GetEdifice(___map);
						if (edifice == null || !edifice.def.MakeFog)
						{
							FloodFillerFog.FloodUnfog(intVec, ___map);
						}
						else
						{
							__instance.Unfog(intVec);
						}
					}
				}
				for (int j = 0; j < 8; j++)
				{
					IntVec3 c2 = c + GenAdj.AdjacentCells[j];
					if (c2.InBounds(___map))
					{
						Building edifice2 = c2.GetEdifice(___map);
						if (edifice2 != null && edifice2.def.MakeFog)
						{
							__instance.Unfog(c2);
						}
					}
				}
			}
		}
	}

	[HarmonyPatch(typeof(RoyalTitlePermitWorker), "AidDisabled")]
	public static class RoyalTitlePermitWorkerInSpace
	{
		public static void Postfix(Map map, ref bool __result)
		{
			if (map != null && map.IsSpace() && __result == true)
				__result = false;
		}
	}

	[HarmonyPatch(typeof(Site), "PostMapGenerate")]
	public static class RaidsStartEarly
	{
		public static void Postfix(Site __instance)
		{
			if (__instance.parts.Where(part => part.def.tags.Contains("SoSMayday")).Any())
			{
				__instance.GetComponent<TimedDetectionRaids>().StartDetectionCountdown(Rand.Range(6000, 12000), 1);
			}
		}
	}

	//sensor
	[HarmonyPatch(typeof(MapPawns), "AnyPawnBlockingMapRemoval", MethodType.Getter)]
	public static class KeepMapAlive
	{
		public static void Postfix(MapPawns __instance, ref bool __result)
		{
			Map mapPlayer = ShipInteriorMod2.FindPlayerShipMap();
			if (mapPlayer != null)
			{
				foreach (Building_ShipAdvSensor sensor in ShipInteriorMod2.WorldComp.Sensors)
				{
					if (sensor.observedMap != null && sensor.observedMap.Map != null && sensor.observedMap.Map.mapPawns == __instance)
						__result = true;
				}
			}
		}
	}

	[HarmonyPatch(typeof(SettlementDefeatUtility), "IsDefeated")]
	public static class NoInstaWin
	{
		public static void Postfix(Map map, ref bool __result)
		{
			Map mapPlayer = ShipInteriorMod2.FindPlayerShipMap();
			if (mapPlayer != null)
			{
				foreach (Building_ShipAdvSensor sensor in ShipInteriorMod2.WorldComp.Sensors)
				{
					if (sensor.observedMap != null && sensor.observedMap.Map == map)
					{
						__result = false;
					}
				}
			}
		}
	}

	[HarmonyPatch(typeof(TimedDetectionRaids), "CompTick")]
	static class NoScanRaids //prevents raids on scanned sites
	{
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			var label = generator.DefineLabel();

			//Find the return to jump the Valdiate() to
			foreach (var instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Ret)
				{
					instruction.labels.Add(label);
					break;
				}
			}
			
			bool found = false;
			foreach (var instruction in instructions)
			{
				yield return instruction;
				if (!found && instruction.opcode == OpCodes.Brfalse)
				{
					yield return new CodeInstruction(OpCodes.Ldloc_0); //Grabs MapParent mapParent
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NoScanRaids), nameof(Validate)));
					yield return new CodeInstruction(OpCodes.Brfalse, label);
					found = true;
				}
			}
			if (!found) Log.Error("SOS2: transpiler failed: " + nameof(NoScanRaids) + ". Did RimWorld update?");
		}
		public static bool Validate(MapParent mapParent)
		{
			return mapParent.Map.mapPawns.AnyColonistSpawned;
		}
	}

	//comms
	[HarmonyPatch(typeof(Building_CommsConsole), "GetFailureReason")]
	public static class NoCommsWhenCloaked
	{
		public static void Postfix(Pawn myPawn, ref FloatMenuOption __result)
		{
			foreach (Building_ShipCloakingDevice cloak in myPawn.Map.GetComponent<ShipHeatMapComp>().Cloaks)
			{
				if (cloak.active && cloak.Map == myPawn.Map)
				{
					__result = new FloatMenuOption("CannotUseCloakEnabled".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null);
					break;
				}
			}
		}
	}

	[HarmonyPatch(typeof(TradeShip), "TryOpenComms")]
	public static class ReplaceCommsIfPirate
	{
		public static bool Prefix(TradeShip __instance, Pawn negotiator, out bool __state) //normal trade on ground if no bounty
        {
            __state = false;
            if (!__instance.Map.IsSpace() && ShipInteriorMod2.WorldComp.PlayerFactionBounty > negotiator.skills.GetSkill(SkillDefOf.Social).levelInt * 2)
				return true;

            __state = true;
            return false;
        }
		public static void Postfix(TradeShip __instance, Pawn negotiator, bool __state) //altered original
		{
			if (!__instance.CanTradeNow || !__state)
			{
				return;
            }
			DiaNode diaNode;
            int bounty = ShipInteriorMod2.WorldComp.PlayerFactionBounty;
			int skill = negotiator.skills.GetSkill(SkillDefOf.Social).levelInt;
            var mapComp = __instance.Map.GetComponent<ShipHeatMapComp>();
            //pirate ship
            if (__instance is PirateShip pirateShip)
            {
				bool pirate =  bounty > 50;
                int demand = mapComp.MapShipCells.Count; //td better calc?
                string text = TranslatorFormattedStringExtensions.Translate("ShipPirateTalk");
                if (pirate)
                    text += TranslatorFormattedStringExtensions.Translate("ShipPirateTalkPirate");
                else if (pirateShip.parleyed)
                    text += TranslatorFormattedStringExtensions.Translate("ShipPirateTalkParley");
                else if (pirateShip.paidOff)
                    text += TranslatorFormattedStringExtensions.Translate("ShipPirateTalkPaid");
                else
                    text += TranslatorFormattedStringExtensions.Translate("ShipPirateTalkNormal", demand);

                diaNode = new DiaNode(text);
                if (pirate || pirateShip.parleyed) //pirate/parleyed, normal trade
                {
                    DiaOption diaOption3 = new DiaOption(TranslatorFormattedStringExtensions.Translate("ShipPirateTrade"));
                    diaOption3.action = delegate
                    {
                        Find.WindowStack.Add(new Dialog_Trade(negotiator, __instance, false));
                        LessonAutoActivator.TeachOpportunity(ConceptDefOf.BuildOrbitalTradeBeacon, OpportunityType.Critical);
                        PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter_Send(__instance.Goods.OfType<Pawn>(), "LetterRelatedPawnsTradeShip".Translate(Faction.OfPlayer.def.pawnsPlural), LetterDefOf.NeutralEvent, false, true);
                        TutorUtility.DoModalDialogIfNotKnown(ConceptDefOf.TradeGoodsMustBeNearBeacon, Array.Empty<string>());
                    };
                    diaOption3.resolveTree = true;
                    diaNode.options.Add(diaOption3);
                }
                else if (!pirateShip.paidOff) //not a pirate, not paidOff
                {
                    //parley: fail - immediate attack
                    DiaOption diaOption = new DiaOption(TranslatorFormattedStringExtensions.Translate("ShipPirateParley"));
                    diaOption.action = delegate
                    {
						int check = bounty + Rand.RangeInclusive(1, 10) + skill;
						pirateShip.parleyed = check > 19;
                        Log.Message("parley roll DC20: " + check);
						if (!pirateShip.parleyed)
                        {
                            Find.LetterStack.ReceiveLetter(TranslatorFormattedStringExtensions.Translate("ShipPirateParleyFail"), TranslatorFormattedStringExtensions.Translate("ShipPirateParleyFailDesc"), LetterDefOf.ThreatBig);
                            mapComp.StartShipEncounter(pirateShip);
                        }
						else
                        {
                            Find.LetterStack.ReceiveLetter(TranslatorFormattedStringExtensions.Translate("ShipPirateParleyWin"), TranslatorFormattedStringExtensions.Translate("ShipPirateParleyWinDesc"), LetterDefOf.PositiveEvent);
                        }
                    };
                    diaOption.resolveTree = true;
                    diaNode.options.Add(diaOption);
                    //accept demand - trade window
                    DiaOption diaOption2 = new DiaOption(TranslatorFormattedStringExtensions.Translate("ShipPirateDemand"));
                    diaOption2.action = delegate
                    {
                        TradeUtility.LaunchThingsOfType(ThingDefOf.Silver, demand, __instance.Map, pirateShip);
						pirateShip.paidOff = true;
                        //td custom trade window, onclose if paid, set paidOff
                        /*Find.WindowStack.Add(new Dialog_Trade(negotiator, __instance, false));
                        LessonAutoActivator.TeachOpportunity(ConceptDefOf.BuildOrbitalTradeBeacon, OpportunityType.Critical);
                        PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter_Send(__instance.Goods.OfType<Pawn>(), "LetterRelatedPawnsTradeShip".Translate(Faction.OfPlayer.def.pawnsPlural), LetterDefOf.NeutralEvent, false, true);
                        TutorUtility.DoModalDialogIfNotKnown(ConceptDefOf.TradeGoodsMustBeNearBeacon, Array.Empty<string>());*/
                    };
                    diaOption2.resolveTree = true;
                    diaNode.options.Add(diaOption2);
                    if (AmountSendableSilver(__instance.Map) < demand)
                    {
                        diaOption2.Disable(TranslatorFormattedStringExtensions.Translate("ShipPirateDemandFail", demand));
                    }
                }
			}
            //normal trader
            else
            {
                diaNode = new DiaNode("TradeShipComms".Translate() + __instance.TraderName);

                //trade normally if no bounty or low bounty with social check
                DiaOption diaOption = new DiaOption("TradeShipTradeWith".Translate());
                diaOption.action = delegate
                {
                    Find.WindowStack.Add(new Dialog_Trade(negotiator, __instance, false));
                    LessonAutoActivator.TeachOpportunity(ConceptDefOf.BuildOrbitalTradeBeacon, OpportunityType.Critical);
                    PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter_Send(__instance.Goods.OfType<Pawn>(), "LetterRelatedPawnsTradeShip".Translate(Faction.OfPlayer.def.pawnsPlural), LetterDefOf.NeutralEvent, false, true);
                    TutorUtility.DoModalDialogIfNotKnown(ConceptDefOf.TradeGoodsMustBeNearBeacon, Array.Empty<string>());
                };
                diaOption.resolveTree = true;
                diaNode.options.Add(diaOption);

                if (skill * 2 < bounty)
                {
                    diaOption.Disable("TradeShipTradeDecline".Translate(__instance.TraderName));
                }

                //if in space add pirate option
                if (__instance.Map.IsSpace())
                {
                    Building bridge = mapComp.MapRootListAll.FirstOrDefault();
                    if (bridge != null)
                    {
                        DiaOption diaOption2 = new DiaOption("TradeShipPirate".Translate());
                        diaOption2.action = delegate
                        {
                            if (Rand.Chance(0.025f * skill + mapComp.MapThreat() / 400 - bounty / 40))
                            {
                                //social + shipstr vs bounty for piracy dialog
                                Find.WindowStack.Add(new Dialog_Pirate(__instance.Map.listerBuildings.allBuildingsColonist.Where(t => t.def == ResourceBank.ThingDefOf.ShipSalvageBay).Count(), __instance));
                                bounty += 4;
                            }
                            else
                            {
                                //check failed, ship is fleeing
                                bounty += 1;
                                if (__instance.Faction == Faction.OfEmpire)
                                    Faction.OfEmpire.TryAffectGoodwillWith(Faction.OfPlayer, -25, false, true, HistoryEventDefOf.AttackedCaravan, null);
                                DiaNode diaNode2 = new DiaNode(__instance.TraderName + "TradeShipTryingToFlee".Translate());
                                DiaOption diaOption21 = new DiaOption("TradeShipAttack".Translate());
                                diaOption21.action = delegate
                                {
                                    mapComp.StartShipEncounter(__instance);
                                };
                                diaOption21.resolveTree = true;
                                diaNode2.options.Add(diaOption21);
                                DiaOption diaOption22 = new DiaOption("TradeShipFlee".Translate());
                                diaOption22.action = delegate
                                {
                                    __instance.Depart();
                                };
                                diaOption22.resolveTree = true;
                                diaNode2.options.Add(diaOption22);
                                Find.WindowStack.Add(new Dialog_NodeTree(diaNode2, true, false, null));
                            }
                            ShipInteriorMod2.WorldComp.PlayerFactionBounty = bounty;
                        };
                        diaOption2.resolveTree = true;
                        diaNode.options.Add(diaOption2);
                    }
                }
                //pay bounty, gray if not enough money
                if (bounty > 1)
                {
                    DiaOption diaOption3 = new DiaOption("TradeShipPayBounty".Translate(2500 * bounty));
                    diaOption3.action = delegate
                    {
                        TradeUtility.LaunchThingsOfType(ThingDefOf.Silver, 2500 * bounty, __instance.Map, null);
                        bounty = 0;
                        ShipInteriorMod2.WorldComp.PlayerFactionBounty = bounty;
                    };
                    diaOption3.resolveTree = true;
                    diaNode.options.Add(diaOption3);
                    if (AmountSendableSilver(__instance.Map) < 2500 * bounty)
                    {
                        diaOption3.Disable("NotEnoughForBounty".Translate(2500 * bounty));
                    }
                }
            }
            //quit
            DiaOption diaOption4 = new DiaOption("(" + "Disconnect".Translate() + ")");
            diaOption4.resolveTree = true;
            diaNode.options.Add(diaOption4);
            Find.WindowStack.Add(new Dialog_NodeTree(diaNode, true, false, null));
        }
		private static int AmountSendableSilver(Map map)
		{
			return (from t in TradeUtility.AllLaunchableThingsForTrade(map, null)
					where t.def == ThingDefOf.Silver
					select t).Sum((Thing t) => t.stackCount);
		}
	}

	//ship
	[HarmonyPatch(typeof(ShipUtility), "ShipBuildingsAttachedTo")]
	public static class FindAllTheShipParts
	{
		public static bool Prefix()
		{
			return false;
		}
		public static void Postfix(Building root, ref List<Building> __result)
		{
			if (root == null || root.Destroyed)
			{
				__result = new List<Building>();
				return;
			}

			var map = root.Map;
			var containedBuildings = new HashSet<Building>();
			var cellsTodo = new HashSet<IntVec3>();
			var cellsDone = new HashSet<IntVec3>();

			cellsTodo.AddRange(GenAdj.CellsOccupiedBy(root));
			cellsTodo.AddRange(GenAdj.CellsAdjacentCardinal(root));

			while (cellsTodo.Count > 0)
			{
				var current = cellsTodo.First();
				cellsTodo.Remove(current);
				cellsDone.Add(current);
				var containedThings = current.GetThingList(map);
				if (!containedThings.Any(t => (t as Building)?.def.building.shipPart ?? false))
					continue;

				foreach (var t in containedThings)
				{
					if (t is Building b && containedBuildings.Add(b))
					{
						cellsTodo.AddRange(GenAdj.CellsOccupiedBy(b).Concat(GenAdj.CellsAdjacentCardinal(b)).Where(cell => !cellsDone.Contains(cell)));
					}
				}
			}
			__result = containedBuildings.ToList();
		}
	}
		
	[HarmonyPatch(typeof(ShipUtility), "LaunchFailReasons")]
	public static class FindLaunchFailReasons
	{
		public static bool Prefix()
		{
			return false;
		}
		public static void Postfix(Building rootBuilding, ref IEnumerable<string> __result)
		{
			List<string> newResult = new List<string>();
			var ship = ((Building_ShipBridge)rootBuilding).Ship;
			if (ship == null)
            {
                Log.Error("SOS2: ship is null in FindLaunchFailReasons");
				return;
            }

			if (ship.Engines.NullOrEmpty())
				newResult.Add(TranslatorFormattedStringExtensions.Translate("ShipReportMissingPart") + ": " + ThingDefOf.Ship_Engine.label);
			if (ship.FuelNeeded(true) < ship.MassActual)
				newResult.Add(TranslatorFormattedStringExtensions.Translate("ShipNeedsMoreFuel", ship.FuelNeeded(true), ship.MassActual));
			if (ship.Sensors.NullOrEmpty())
				newResult.Add(TranslatorFormattedStringExtensions.Translate("ShipReportMissingPart") + ": " + ThingDefOf.Ship_SensorCluster.label);
			if (!ship.HasMannedBridge())
				newResult.Add(TranslatorFormattedStringExtensions.Translate("ShipReportNeedPilot"));

			__result = newResult;
		}
	}

	[HarmonyPatch(typeof(ShipCountdown), "InitiateCountdown", new Type[] { typeof(Building) })]
	public static class SetShipGroundMap
	{
		public static bool Prefix(Building launchingShipRoot)
		{
			ShipInteriorMod2.shipOriginRoot = launchingShipRoot;
			return true;
		}
	}

	[HarmonyPatch(typeof(ShipCountdown), "CountdownEnded")]
	public static class LaunchShipToSpace
	{
		public static bool Prefix()
		{
			if (ShipInteriorMod2.SaveShipFlag)
			{
				ShipInteriorMod2.SaveShipToFile((Building_ShipBridge)ShipInteriorMod2.shipOriginRoot);
			}
			else if (ShipInteriorMod2.shipOriginRoot != null)
			{
				ScreenFader.StartFade(Color.clear, 1f);
				IntVec3 size = ShipInteriorMod2.shipOriginRoot.Map.Size;
                if (size.x < Find.World.info.initialMapSize.x && size.y < Find.World.info.initialMapSize.y)
				{
					size = Find.World.info.initialMapSize;
				}
				Map map = ShipInteriorMod2.GeneratePlayerShipMap(size);

				ShipInteriorMod2.MoveShip(ShipInteriorMod2.shipOriginRoot, map, IntVec3.Zero);
				map.weatherManager.TransitionTo(ResourceBank.WeatherDefOf.OuterSpaceWeather);
				Find.LetterStack.ReceiveLetter(TranslatorFormattedStringExtensions.Translate("LetterLabelOrbitAchieved"),
					TranslatorFormattedStringExtensions.Translate("LetterOrbitAchieved"), LetterDefOf.PositiveEvent);
				ShipInteriorMod2.shipOriginRoot = null;
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(GameConditionManager), "ConditionIsActive")]
	public static class SpacecraftAreHardenedAgainstSolarFlares
	{
		public static void Postfix(ref bool __result, GameConditionManager __instance, GameConditionDef def)
		{
			if (def == GameConditionDefOf.SolarFlare && __instance.ownerMap != null &&
				__instance.ownerMap.IsSpace())
				__result = false;
		}
	}

	[HarmonyPatch(typeof(GameConditionManager), "ElectricityDisabled", MethodType.Getter)]
	public static class SpacecraftAreAlsoHardenedInOnePointOne
	{
		public static void Postfix(GameConditionManager __instance, ref bool __result)
		{
			if (__instance.ownerMap.IsSpace()) __result = false;
		}
	}

	[HarmonyPatch(typeof(Designator_Dropdown), "GetDesignatorCost")]
	public class FixDropdownDisplay
	{
		public static void Postfix(Designator des, ref ThingDef __result)
		{
			Designator_Place designator_Place = des as Designator_Place;
			if (designator_Place != null)
			{
				BuildableDef placingDef = designator_Place.PlacingDef;
				if (placingDef.designationCategory.defName.Equals("Ship"))
				{
					__result = (ThingDef)placingDef;
				}
			}
		}
	}

	[HarmonyPatch(typeof(RoofGrid), "GetCellExtraColor")]
	public static class ShowHullTilesOnRoofGrid
	{
		public static void Postfix(RoofGrid __instance, int index, ref Color __result)
		{
			if (__instance.RoofAt(index) == ResourceBank.RoofDefOf.RoofShip)
				__result = Color.clear;
		}
	}

	[HarmonyPatch(typeof(WorkGiver_ConstructDeliverResources), "ShouldRemoveExistingFloorFirst")]
	public static class DontRemoveShipFloors
	{
		public static void Postfix(Blueprint blue, ref bool __result)
		{
			var t = blue.Map.terrainGrid.TerrainAt(blue.Position);
			if (t == ResourceBank.TerrainDefOf.FakeFloorInsideShip || t == ResourceBank.TerrainDefOf.FakeFloorInsideShipArchotech || t == ResourceBank.TerrainDefOf.FakeFloorInsideShipMech)
			{
				__result = false;
			}
		}
	}

	[HarmonyPatch(typeof(TerrainGrid), "DoTerrainChangedEffects")] //restores ship terrain after tile removal
	public static class RecreateShipTile
	{
		public static void Postfix(TerrainGrid __instance, IntVec3 c, Map ___map)
		{
			if (ShipInteriorMod2.AirlockBugFlag)
				return;
			if (___map.GetComponent<ShipHeatMapComp>()?.MapShipCells?.ContainsKey(c) ?? false)
            {
                foreach (Thing t in ___map.thingGrid.ThingsAt(c))
                {
                    var shipPart = t.TryGetComp<CompSoShipPart>();
                    if (shipPart != null && (shipPart.Props.isPlating || shipPart.Props.isHardpoint || shipPart.Props.isHull))
                    {
                        shipPart.SetShipTerrain(c);
                        break;
                    }
                }
            }
		}
	}

	[HarmonyPatch(typeof(RoofGrid), "SetRoof")] //roofing ship tiles makes ship roof
	public static class RebuildShipRoof
	{
		public static bool Prefix(IntVec3 c, RoofDef def, Map ___map, ref CellBoolDrawer ___drawerInt, ref RoofDef[] ___roofGrid)
		{
			if (def == null || def.isThickRoof)
				return true;
			foreach (Thing t in c.GetThingList(___map).Where(t => t is Building))
			{
				var shipPart = t.TryGetComp<CompSoShipPart>();
				if (shipPart != null && shipPart.Props.roof)
				{
					var cellIndex = ___map.cellIndices.CellToIndex(c);
					if (___roofGrid[cellIndex] == def)
					{
						return false;
					}

					if (ShipInteriorMod2.IsRoofDefAirtight(def))
						return true;
					//Log.Message(String.Format("Overriding roof at {0}. Set shipRoofDef instead of {1}", cellIndex, def.defName));
					___roofGrid[cellIndex] = ResourceBank.RoofDefOf.RoofShip;
					___map.glowGrid.MarkGlowGridDirty(c);
					Region validRegionAt_NoRebuild = ___map.regionGrid.GetValidRegionAt_NoRebuild(c);
					if (validRegionAt_NoRebuild != null)
					{
						validRegionAt_NoRebuild.District.Notify_RoofChanged();
					}
					if (___drawerInt != null)
					{
						___drawerInt.SetDirty();
					}
					___map.mapDrawer.MapMeshDirty(c, MapMeshFlag.Roofs);
					return false;
				}
			}
			return true;
		}
	}

    [HarmonyPatch(typeof(RoofCollapserImmediate), "DropRoofInCells", new Type[] { typeof(IEnumerable<IntVec3>), typeof(Map), typeof(List<Thing>) })]
    public static class SealHole
    {
        public static void Postfix(IEnumerable<IntVec3> cells, Map map)
        {
            if (!map.IsSpace())
                return;
            var mapComp = map.GetComponent<ShipHeatMapComp>();
            foreach (IntVec3 cell in cells)
            {
                if (!cell.Roofed(map))
                {
                    int shipIndex = mapComp.ShipIndexOnVec(cell);
                    if (shipIndex == -1)
                        continue;
                    var ship = mapComp.ShipsOnMapNew[shipIndex];
                    if (ship.FoamDistributors.Any())
                    {
                        foreach (CompHullFoamDistributor dist in ship.FoamDistributors)
                        {
                            if (dist.parent.TryGetComp<CompRefuelable>().Fuel > 0 && dist.parent.TryGetComp<CompPowerTrader>().PowerOn)
                            {
                                dist.parent.TryGetComp<CompRefuelable>().ConsumeFuel(1);
                                map.roofGrid.SetRoof(cell, ResourceBank.RoofDefOf.RoofShip);
                                //Log.Message("rebuilt roof at:" + cell);
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    //buildings
    [HarmonyPatch(typeof(Building), "SpawnSetup")] //adds normal building weight/count to ship
    public static class DoSpawn
    {
        [HarmonyPostfix]
        public static void OnSpawn(Building __instance, Map map, bool respawningAfterLoad)
        {
            if (respawningAfterLoad)
                return;
            var mapComp = map.GetComponent<ShipHeatMapComp>();
            if (mapComp.CacheOff || mapComp.ShipsOnMapNew.NullOrEmpty() || __instance.TryGetComp<CompSoShipPart>() != null)
                return;
            foreach (IntVec3 vec in GenAdj.CellsOccupiedBy(__instance)) //if any part spawned on ship
            {
                int shipIndex = mapComp.ShipIndexOnVec(vec);
                if (shipIndex != -1)
                {
                    mapComp.ShipsOnMapNew[shipIndex].AddToCache(__instance);
                    return;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Building), "DeSpawn")] //for comp calls and weight, after despawn, before base.despawn
    public static class DoPreDeSpawn
    {
        //can we have predespawn at home? no, we have despawn at home, despawn at home: postdespawn
        [HarmonyPrefix]
        public static bool PreDeSpawn(Building __instance, DestroyMode mode)
        {
            var mapComp = __instance.Map.GetComponent<ShipHeatMapComp>();
            if (mapComp.CacheOff)
                return true;
            var shipComp = __instance.TryGetComp<CompSoShipPart>();
            if (shipComp != null) //predespawn for ship parts
                shipComp.PreDeSpawn(mode);
            else if (!mapComp.ShipsOnMapNew.NullOrEmpty()) //rems normal building weight/count to ship
            {
                foreach (IntVec3 vec in GenAdj.CellsOccupiedBy(__instance))
                {
                    int shipIndex = mapComp.ShipIndexOnVec(vec);
                    if (shipIndex != -1)
                    {
                        var ship = mapComp.ShipsOnMapNew[mapComp.MapShipCells[vec].Item1];
                        if (ship.Buildings.Contains(__instance))
                        {
                            ship.RemoveFromCache(__instance, mode);
                        }
                    }
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SectionLayer_BuildingsDamage), "PrintDamageVisualsFrom")]
	public class FixBuildingDraw
	{
		public static bool Prefix(Building b)
		{
			if (b.Map == null)
				return false;
			return true;
		}
	}

	[HarmonyPatch(typeof(Room), "Notify_ContainedThingSpawnedOrDespawned")]
	public static class AirlockBugFix
	{
		public static bool Prefix(Room __instance, ref bool ___statsAndRoleDirty)
		{
			if (ShipInteriorMod2.AirlockBugFlag)
			{
				___statsAndRoleDirty = true;
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Building_Turret), "PreApplyDamage")]
	public static class HardpointsHelpTurrets
	{
		public static bool Prefix(ref DamageInfo dinfo, Building_Turret __instance)
		{
			foreach (Thing t in __instance.Position.GetThingList(__instance.Map))
			{
				var shipPart = t.TryGetComp<CompSoShipPart>();
				if (shipPart != null && shipPart.Props.isHardpoint)
				{
					dinfo.SetAmount(dinfo.Amount / 2);
					break;
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(ThingListGroupHelper), "Includes")]
	public static class ReactorsCanBeRefueled
	{
		public static void Postfix(ThingRequestGroup group, ThingDef def, ref bool __result)
		{
			if (group == ThingRequestGroup.Refuelable && def.HasComp(typeof(CompRefuelableOverdrivable)))
				__result = true;
		}
	}

	[HarmonyPatch(typeof(CompPower), "PowerNet", MethodType.Getter)]
	public static class FixPowerBug
	{
		public static void Postfix(CompPower __instance, ref PowerNet __result)
		{
			if (__instance.parent == null)
				return;
			if (!(__instance.parent.ParentHolder is MinifiedThing) && __instance.Props.transmitsPower && __result == null && (__instance?.parent?.Map?.GetComponent<ShipHeatMapComp>().InCombat ?? false))// || __instance.parent.Map.GetComponent<ShipHeatMapComp>().IsGraveyard))
			{
				__instance.transNet = __instance.parent.Map.powerNetGrid.TransmittedPowerNetAt(__instance.parent.Position);
				if (__instance.transNet != null)
				{
					__instance.transNet.connectors.Add(__instance);
					if (__instance is CompPowerBattery)
						__instance.transNet.batteryComps.Add((CompPowerBattery)__instance);
					else if (__instance is CompPowerTrader)
						__instance.transNet.powerComps.Add((CompPowerTrader)__instance);
					__result = __instance.transNet;
				}
			}
		}
	}

	[HarmonyPatch(typeof(ShortCircuitUtility), nameof(ShortCircuitUtility.DoShortCircuit))]
	public static class NoShortCircuitCapacitors
	{
		static bool Prepare()
		{
			return !ModLister.HasActiveModWithName("RT Fuse");
		}
		public static bool Prefix(Building culprit, out bool __state)
		{
			__state = false;
			PowerNet powerNet = culprit.PowerComp.PowerNet;
			if (powerNet.batteryComps.Any((CompPowerBattery x) =>
				x.parent.def == ThingDef.Named("ShipCapacitor") || x.parent.def == ThingDef.Named("ShipCapacitorSmall")))
			{
				__state = true;
				return false;
			}
			return true;
		}
		public static void Postfix(Building culprit, bool __state)
		{
			if (__state)
			{
				Find.LetterStack.ReceiveLetter(TranslatorFormattedStringExtensions.Translate("LetterLabelShortCircuit"), TranslatorFormattedStringExtensions.Translate("LetterLabelShortCircuitShipDesc"),
					LetterDefOf.NegativeEvent, new TargetInfo(culprit.Position, culprit.Map, false), null);
			}
		}
	}

	[HarmonyPatch(typeof(GenSpawn), "SpawningWipes")]
	public static class ConduitWipe
	{
		public static void Postfix(ref bool __result, BuildableDef newEntDef, BuildableDef oldEntDef)
		{
			ThingDef newDef = newEntDef as ThingDef;
			if (oldEntDef.defName == "ShipHeatConduit")
			{
				if (newDef != null)
				{
					foreach (CompProperties comp in newDef.comps)
					{
						if (comp is CompProperties_ShipHeat)
							__result = true;
					}
				}
			}
		}
	}

	[HarmonyPatch(typeof(CompScanner), "CanUseNow", MethodType.Getter)]
	public static class NoUseInSpace
	{
		public static bool Postfix(bool __result, CompScanner __instance)
		{
			if (__instance.parent.Map.IsSpace())
				return false;
			return __result;
		}
	}

	[HarmonyPatch(typeof(Building), "MaxItemsInCell", MethodType.Getter)]
	public static class DisableForMoveShelf
	{
		public static int Postfix(int __result, Building __instance)
		{
			if (__result > 1 && ShipInteriorMod2.AirlockBugFlag)
				return 1;
			return __result;
		}
	}

    [HarmonyPatch(typeof(CompGenepackContainer), "EjectContents")]
    public static class DisableForMoveGene
    {
        public static bool Prefix()
        {
            if (ShipInteriorMod2.AirlockBugFlag)
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(CompThingContainer), "PostDeSpawn")]
    public static class DisableForMoveContainer
    {
        public static bool Prefix()
        {
            if (ShipInteriorMod2.AirlockBugFlag)
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(Building_MechGestator), "EjectContentsAndRemovePawns")]
    public static class DisableForMoveGestator
    {
        public static bool Prefix()
        {
            if (ShipInteriorMod2.AirlockBugFlag)
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(CompWasteProducer), "ProduceWaste")]
    public static class DisableForMoveWaste
    {
        public static bool Prefix()
        {
            if (ShipInteriorMod2.AirlockBugFlag)
                return false;
            return true;
        }
    }

    /*[HarmonyPatch(typeof(CompBiosculpterPod), "EjectContents")] disabled due to move respawn issues
    public static class DisableForMoveSculpt
    {
        public static bool Prefix()
        {
            if (ShipInteriorMod2.AirlockBugFlag)
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(ThingOwner), "TryDropAll")] prevents drops but other things not set
    public static class DisableForMoveThingOwner
    {
        public static bool Prefix()
        {
            if (ShipInteriorMod2.AirlockBugFlag)
                return false;
            return true;
        }
    }*/

    [HarmonyPatch(typeof(CompAssignableToPawn), "PostSpawnSetup")] //beds?
    public static class DisableForMoveAssignableOn
    {
        public static bool Prefix()
        {
            if (ShipInteriorMod2.AirlockBugFlag)
                return false;
            return true;
        }
    }
    [HarmonyPatch(typeof(CompAssignableToPawn), "PostDeSpawn")]
    public static class DisableForMoveAssignableOff
    {
        public static bool Prefix()
        {
            if (ShipInteriorMod2.AirlockBugFlag)
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(CompDeathrestBindable), "PostSpawnSetup")] //deathrest
    public static class DisableForMoveDeathOn
    {
        public static bool Prefix()
        {
            if (ShipInteriorMod2.AirlockBugFlag)
                return false;
            return true;
        }
    }
    [HarmonyPatch(typeof(CompDeathrestBindable), "PostDeSpawn")]
    public static class DisableForMoveDeathOff
	{
		public static bool Prefix()
		{
			if (ShipInteriorMod2.AirlockBugFlag)
				return false;
			return true;
		}
	}

    [HarmonyPatch]
	public class PatchCharger
	{
		[HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
		[HarmonyPatch(typeof(Building), "DeSpawn")]
		public static void Snapshot(object instance, DestroyMode mode)
		{
		}
	}
	[HarmonyPatch(typeof(Building_MechCharger), "DeSpawn")]
	public static class DisableForMoveCharger
	{
		public static bool Prefix(Building_MechCharger __instance, DestroyMode mode)
		{
			if (ShipInteriorMod2.AirlockBugFlag)
			{
				PatchCharger.Snapshot(__instance, mode);
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch]
	public class PatchGrower
	{
		[HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
		[HarmonyPatch(typeof(Building), "DeSpawn")]
		public static void Snapshot(object instance, DestroyMode mode)
		{
		}
	}
	[HarmonyPatch(typeof(Building_PlantGrower), "DeSpawn")]
	public static class DisableForMoveGrower
	{
		public static bool Prefix(Building_PlantGrower __instance, DestroyMode mode)
		{
			if (ShipInteriorMod2.AirlockBugFlag)
			{
				PatchGrower.Snapshot(__instance, mode);
				return false;
			}
			return true;
		}
	}

    [HarmonyPatch(typeof(Designator_Deconstruct), "CanDesignateThing")]
    public static class ChangeReason
    {
        public static void Postfix(ref AcceptanceReport __result, Thing t)
        {
            if (!__result.Accepted && t.Map.IsSpace() && __result.Reason.Equals("MessageMustDesignateDeconstructibleMechCluster".Translate()))
                __result = new AcceptanceReport("Use salvage bay to claim after all enemies have been defeated.");
        }
    }

    //weapons
    [HarmonyPatch(typeof(BuildingProperties), "IsMortar", MethodType.Getter)]
	public static class TorpedoesCanBeLoaded
	{
		public static void Postfix(BuildingProperties __instance, ref bool __result)
		{
			if (__instance?.turretGunDef?.HasComp(typeof(CompChangeableProjectilePlural)) ?? false)
			{
				__result = true;
			}
		}
	}

	[HarmonyPatch(typeof(ITab_Shells), "SelStoreSettingsParent", MethodType.Getter)]
	public static class TorpedoesHaveShellTab
	{
		public static void Postfix(ITab_Shells __instance, ref IStoreSettingsParent __result)
		{
			Building_ShipTurret building_TurretGun = Find.Selector.SingleSelectedObject as Building_ShipTurret;
			if (building_TurretGun != null)
			{
				__result = __instance.GetThingOrThingCompStoreSettingsParent(building_TurretGun.gun);
				return;
			}
		}
	}

	[HarmonyPatch(typeof(Projectile), "CheckForFreeInterceptBetween")]
	public static class OnePointThreeSpaceProjectiles
	{
		public static void Postfix(Projectile __instance, ref bool __result)
		{
			if (__instance is Projectile_SoSFake)
				__result = false;
		}
	}

	[HarmonyPatch(typeof(Projectile), "Launch", new Type[] {
		typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo),
		typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef) })] //td? move this into ship turret/launch code
	public static class TransferAmplifyBonus
	{
		public static void Postfix(Projectile __instance, Thing equipment, ref float ___weaponDamageMultiplier)
		{
			if (__instance is Projectile_ExplosiveShipCombat && equipment is Building_ShipTurret turret &&
				turret.AmplifierDamageBonus > 0)
			{
				___weaponDamageMultiplier = 1 + turret.AmplifierDamageBonus;
			}
		}
	}

	//crypto
	[HarmonyPatch(typeof(Building_CryptosleepCasket), "FindCryptosleepCasketFor")]
	public static class AllowCrittersleepCaskets
	{
		public static bool Prefix()
		{
			return false;
		}
		public static void Postfix(ref Building_CryptosleepCasket __result, Pawn p, Pawn traveler, ThingOwner ___innerContainer,
			bool ignoreOtherReservations = false)
		{
			foreach (var current in GetCryptosleepDefs())
			{
				if (current == ResourceBank.ThingDefOf.Cryptonest)
					continue;
				var building_CryptosleepCasket =
					(Building_CryptosleepCasket)GenClosest.ClosestThingReachable(p.Position, p.Map,
						ThingRequest.ForDef(current), PathEndMode.InteractionCell,
						TraverseParms.For(traveler), 9999f,
						delegate (Thing x) {
							bool arg_33_0;
							if (x.def == ResourceBank.ThingDefOf.CrittersleepCasket &&
								p.BodySize <= ShipInteriorMod2.crittersleepBodySize && ___innerContainer.Count < 8 ||
								x.def == ResourceBank.ThingDefOf.CrittersleepCasketLarge &&
								p.BodySize <= ShipInteriorMod2.crittersleepBodySize && ___innerContainer.Count < 32)
							{
								var traveler2 = traveler;
								LocalTargetInfo target = x;
								var ignoreOtherReservations2 = ignoreOtherReservations;
								arg_33_0 = traveler2.CanReserve(target, 1, -1, null, ignoreOtherReservations2);
							}
							else
							{
								arg_33_0 = false;
							}

							return arg_33_0;
						});
				if (building_CryptosleepCasket != null)
				{
					__result = building_CryptosleepCasket;
					return;
				}

				building_CryptosleepCasket = (Building_CryptosleepCasket)GenClosest.ClosestThingReachable(
					p.Position, p.Map, ThingRequest.ForDef(current), PathEndMode.InteractionCell,
					TraverseParms.For(traveler), 9999f,
					delegate (Thing x) {
						bool arg_33_0;
						if (x.def != ResourceBank.ThingDefOf.CrittersleepCasket && x.def != ResourceBank.ThingDefOf.CrittersleepCasketLarge &&
							!((Building_CryptosleepCasket)x).HasAnyContents)
						{
							var traveler2 = traveler;
							LocalTargetInfo target = x;
							var ignoreOtherReservations2 = ignoreOtherReservations;
							arg_33_0 = traveler2.CanReserve(target, 1, -1, null, ignoreOtherReservations2);
						}
						else
						{
							arg_33_0 = false;
						}

						return arg_33_0;
					});
				if (building_CryptosleepCasket != null) __result = building_CryptosleepCasket;
			}
		}

		private static IEnumerable<ThingDef> GetCryptosleepDefs()
		{
			return ModLister.HasActiveModWithName("PsiTech")
				? DefDatabase<ThingDef>.AllDefs.Where(def =>
					def != ThingDef.Named("PTPsychicTraier") &&
					typeof(Building_CryptosleepCasket).IsAssignableFrom(def.thingClass))
				: DefDatabase<ThingDef>.AllDefs.Where(def =>
					typeof(Building_CryptosleepCasket).IsAssignableFrom(def.thingClass));
		}
	}

	[HarmonyPatch(typeof(JobDriver_CarryToCryptosleepCasket), "MakeNewToils")]
	public static class JobDriverFix
	{
		public static bool Prefix()
		{
			return false;
		}
		public static void Postfix(ref IEnumerable<Toil> __result,
			JobDriver_CarryToCryptosleepCasket __instance)
		{
			Pawn Takee = __instance.Takee;
			Building_CryptosleepCasket DropPod = __instance.DropPod;
			List<Toil> myResult = new List<Toil>();
			__instance.FailOnDestroyedOrNull(TargetIndex.A);
			__instance.FailOnDestroyedOrNull(TargetIndex.B);
			__instance.FailOnAggroMentalState(TargetIndex.A);
			__instance.FailOn(() => !DropPod.Accepts(Takee));
            Toil goToTakee = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell)
				.FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOnDespawnedNullOrForbidden(TargetIndex.B)
				.FailOn(() =>
					(DropPod.def != ResourceBank.ThingDefOf.CrittersleepCasket &&
					 DropPod.def != ResourceBank.ThingDefOf.CrittersleepCasketLarge) && DropPod.GetDirectlyHeldThings().Any)
				.FailOn(() => !Takee.Downed)
				.FailOn(() =>
					!__instance.pawn.CanReach(Takee, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn))
				.FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            Toil startCarryingTakee = Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false, true);
            Toil goToThing = Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.InteractionCell);
            myResult.Add(Toils_Jump.JumpIf(goToThing, () => __instance.pawn.IsCarryingPawn(Takee)));
            myResult.Add(goToTakee);
            myResult.Add(startCarryingTakee);
            myResult.Add(goToThing);
            Toil prepare = Toils_General.Wait(500, TargetIndex.B);
			prepare.FailOnCannotTouch(TargetIndex.B, PathEndMode.InteractionCell);
			prepare.WithProgressBarToilDelay(TargetIndex.B, false, -0.5f);
			myResult.Add(prepare);
			myResult.Add(new Toil
			{
				initAction = delegate { DropPod.TryAcceptThing(Takee, true); },
				defaultCompleteMode = ToilCompleteMode.Instant
			});
			__result = myResult;
		}
	}

	[HarmonyPatch(typeof(FloatMenuMakerMap), "AddHumanlikeOrders")]
	public static class EggFix
	{
		public static void Postfix(Vector3 clickPos, Pawn pawn, ref List<FloatMenuOption> opts)
		{
			if (pawn == null || clickPos == null)
				return;
			IntVec3 c = IntVec3.FromVector3(clickPos);
			if (pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
			{
				foreach (Thing current in c.GetThingList(pawn.Map))
				{
					if (current.def.IsWithinCategory(ThingCategoryDef.Named("EggsFertilized")) &&
						pawn.CanReserveAndReach(current, PathEndMode.OnCell, Danger.Deadly, 1, -1, null, true) &&
						findCryptonestFor(current, pawn, true) != null)
					{
						string text2 = "Carry to cryptonest";
						JobDef jDef = DefDatabase<JobDef>.GetNamed("CarryToCryptonest");
						Action action2 = delegate {
							Building_CryptosleepCasket building_CryptosleepCasket =
								findCryptonestFor(current, pawn, false);
							if (building_CryptosleepCasket == null)
							{
								building_CryptosleepCasket = findCryptonestFor(current, pawn, true);
							}

							if (building_CryptosleepCasket == null)
							{
								Messages.Message(
									TranslatorFormattedStringExtensions.Translate("CannotCarryToCryptosleepCasket") + ": " +
									TranslatorFormattedStringExtensions.Translate("NoCryptosleepCasket"), current, MessageTypeDefOf.RejectInput);
								return;
							}

							Job job = new Job(jDef, current, building_CryptosleepCasket);
							job.count = current.stackCount;
							int eggsAlreadyInNest = building_CryptosleepCasket.innerContainer.Count;
							if (job.count + eggsAlreadyInNest > 16)
								job.count = 16 - eggsAlreadyInNest;
							pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
						};
						string label = text2;
						Action action = action2;
						opts.Add(FloatMenuUtility.DecoratePrioritizedTask(
							new FloatMenuOption(label, action, MenuOptionPriority.Default, null, current, 0f, null,
								null), pawn, current, "ReservedBy"));
					}
				}
			}
		}

		static Building_CryptosleepCasket findCryptonestFor(Thing egg, Pawn p, bool ignoreOtherReservations)
		{
			Building_CryptosleepCasket building_CryptosleepCasket =
				(Building_CryptosleepCasket)GenClosest.ClosestThingReachable(p.Position, p.Map,
					ThingRequest.ForDef(ThingDef.Named("Cryptonest")), PathEndMode.InteractionCell,
					TraverseParms.For(p, Danger.Deadly, TraverseMode.ByPawn, false), 9999f, delegate (Thing x) {
						bool arg_33_0;
						if (((Building_CryptosleepCasket)x).innerContainer.TotalStackCount < 16)
						{
							LocalTargetInfo target = x;
							bool ignoreOtherReservations2 = ignoreOtherReservations;
							arg_33_0 = p.CanReserve(target, 1, -1, null, ignoreOtherReservations2);
						}
						else
						{
							arg_33_0 = false;
						}

						return arg_33_0;
					}, null, 0, -1, false, RegionType.Set_Passable, false);
			if (building_CryptosleepCasket != null)
			{
				return building_CryptosleepCasket;
			}

			return null;
		}
	}

	[HarmonyPatch(typeof(Building_Casket), "Tick")]
	public static class EggsDontHatch
	{
		public static bool Prefix(Building_Casket __instance, List<ThingComp> ___comps)
		{
			if (__instance.def == ResourceBank.ThingDefOf.Cryptonest)
			{
				if (___comps != null)
				{
					int i = 0;
					int count = ___comps.Count;
					while (i < count)
					{
						___comps[i].CompTick();
						i++;
					}
				}
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Building_CryptosleepCasket), "GetFloatMenuOptions")]
	public static class CantEnterCryptonest
	{
		public static bool Prefix(Building_CryptosleepCasket __instance)
		{
			if (__instance.def == ResourceBank.ThingDefOf.Cryptonest)
			{
				return false;
			}
			return true;
		}
		public static void Postfix(IEnumerable<FloatMenuOption> __result, Building_CryptosleepCasket __instance)
		{
			if (__instance.def == ResourceBank.ThingDefOf.Cryptonest)
			{
				__result = new List<FloatMenuOption>();
			}
		}
	}

	[HarmonyPatch(typeof(Building_CryptosleepCasket), "TryAcceptThing")]
	public static class UpdateCasketGraphicsA
	{
		public static void Postfix(Building_CryptosleepCasket __instance)
		{
			if (__instance.Map != null && __instance.Spawned)
				__instance.Map.mapDrawer.MapMeshDirty(__instance.Position,
					MapMeshFlag.Buildings | MapMeshFlag.Things);
		}
	}

	[HarmonyPatch(typeof(Building_CryptosleepCasket), "EjectContents")]
	public static class UpdateCasketGraphicsB
	{
		public static void Postfix(Building_CryptosleepCasket __instance)
		{
			if (__instance.Map != null && __instance.Spawned)
				__instance.Map.mapDrawer.MapMeshDirty(__instance.Position,
					MapMeshFlag.Buildings | MapMeshFlag.Things);
		}
	}

	//skyfaller related patches - shuttles
	[HarmonyPatch(typeof(FlyShipLeaving), "LeaveMap")]
	public static class LeavingPodFix
	{
		public static bool Prefix(ref FlyShipLeaving __instance)
		{
			if (__instance.def.defName.Equals("PersonalShuttleSkyfaller") || __instance.def.defName.Equals("CargoShuttleSkyfaller") || __instance.def.defName.Equals("HeavyCargoShuttleSkyfaller") || __instance.def.defName.Equals("DropshipShuttleSkyfaller"))
			{
				if ((bool)typeof(FlyShipLeaving).GetField("alreadyLeft", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance))
				{
					__instance.Destroy(DestroyMode.Vanish);
					return false;
				}
				if (__instance.groupID < 0)
				{
					Log.Error("Drop pod left the map, but its group ID is " + __instance.groupID);
					__instance.Destroy(DestroyMode.Vanish);
					return false;
				}
				if (__instance.destinationTile < 0)
				{
					Log.Error("Drop pod left the map, but its destination tile is " + __instance.destinationTile);
					__instance.Destroy(DestroyMode.Vanish);
					return false;
				}
				Lord lord = TransporterUtility.FindLord(__instance.groupID, __instance.Map);
				if (lord != null)
				{
					__instance.Map.lordManager.RemoveLord(lord);
				}
				TravelingTransportPods travelingTransportPods;
				if (__instance.def.defName.Equals("PersonalShuttleSkyfaller"))
					travelingTransportPods = (TravelingTransportPods)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("TravelingShuttlesPersonal"));
				else if (__instance.def.defName.Equals("CargoShuttleSkyfaller"))
					travelingTransportPods = (TravelingTransportPods)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("TravelingShuttlesCargo"));
				else if (__instance.def.defName.Equals("HeavyCargoShuttleSkyfaller"))
					travelingTransportPods = (TravelingTransportPods)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("TravelingShuttlesHeavy"));
				else
					travelingTransportPods = (TravelingTransportPods)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("TravelingShuttlesDropship"));
				travelingTransportPods.Tile = __instance.Map.Tile;

				Thing t = __instance.Contents.innerContainer.Where(p => p is Pawn).FirstOrDefault();
				if (__instance.Map.GetComponent<ShipHeatMapComp>().InCombat && t != null)
					travelingTransportPods.SetFaction(t.Faction);
				else
					travelingTransportPods.SetFaction(Faction.OfPlayer);
				travelingTransportPods.destinationTile = __instance.destinationTile;
				travelingTransportPods.arrivalAction = __instance.arrivalAction;
				Find.WorldObjects.Add(travelingTransportPods);

				List<Thing> pods = new List<Thing>();
				pods.AddRange(__instance.Map.listerThings.ThingsInGroup(ThingRequestGroup.ActiveDropPod));
				for (int i = 0; i < pods.Count; i++)
				{
					FlyShipLeaving dropPodLeaving = pods[i] as FlyShipLeaving;
					if (dropPodLeaving != null && dropPodLeaving.groupID == __instance.groupID)
					{
						typeof(FlyShipLeaving).GetField("alreadyLeft", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(dropPodLeaving, true);
						travelingTransportPods.AddPod(dropPodLeaving.Contents, true);
						dropPodLeaving.Contents = null;
						dropPodLeaving.Destroy(DestroyMode.Vanish);
					}
				}
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(DropPodUtility), "MakeDropPodAt")]
	public static class TravelingPodFix
	{
		public static bool Prefix(IntVec3 c, Map map, ActiveDropPodInfo info)
		{
			bool hasShuttle = false;
			//ThingDef shuttleDef = null;
			ThingDef skyfaller = null;
			Thing foundShuttle = null;
			foreach (Thing t in info.innerContainer)
			{
				if (t.TryGetComp<CompBecomeBuilding>() != null)
				{
					hasShuttle = true;
					//shuttleDef = t.def;
					skyfaller = t.TryGetComp<CompBecomeBuilding>().Props.skyfaller;
					foundShuttle = t;
					break;
				}
			}
			if (hasShuttle)
			{
				ActiveDropPod activeDropPod = (ActiveDropPod)ThingMaker.MakeThing(ThingDefOf.ActiveDropPod, null);
				activeDropPod.Contents = info;
				Skyfaller theShuttle = SkyfallerMaker.SpawnSkyfaller(skyfaller, activeDropPod, c, map);
				if (foundShuttle.TryGetComp<CompShuttleCosmetics>() != null)
				{
					Graphic_Single graphic = new Graphic_Single();
					CompProperties_ShuttleCosmetics Props = foundShuttle.TryGetComp<CompShuttleCosmetics>().Props;
					int whichVersion = foundShuttle.TryGetComp<CompShuttleCosmetics>().whichVersion;
					GraphicRequest req = new GraphicRequest(typeof(Graphic_Single), Props.graphicsHover[whichVersion].texPath + "_south", ShaderDatabase.Cutout, Props.graphics[whichVersion].drawSize, Color.white, Color.white, Props.graphics[whichVersion], 0, null, "");
					graphic.Init(req);
					typeof(Thing).GetField("graphicInt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(theShuttle, graphic);
				}
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(DropPodIncoming), "Impact")]
	public static class IncomingPodFix
	{
		public static bool Prefix(ref DropPodIncoming __instance)
		{
			//spawns pawns and shuttle at location
			if (__instance.def.defName.Equals("ShuttleIncomingPersonal") || __instance.def.defName.Equals("ShuttleIncomingCargo") || __instance.def.defName.Equals("ShuttleIncomingHeavy") || __instance.def.defName.Equals("ShuttleIncomingDropship"))
			{
				for (int i = 0; i < 6; i++)
				{
					Vector3 loc = __instance.Position.ToVector3Shifted() + Gen.RandomHorizontalVector(1f);
					FleckMaker.ThrowDustPuff(loc, __instance.Map, 1.2f);
				}
				FleckMaker.ThrowLightningGlow(__instance.Position.ToVector3Shifted(), __instance.Map, 2f);

				Pawn myShuttle = null;
				ThingOwner container = ((ActiveDropPod)__instance.innerContainer[0]).Contents.innerContainer;

				for (int i = container.Count - 1; i >= 0; i--)
				{
					if (container[i] is Pawn && container[i].TryGetComp<CompBecomeBuilding>() != null)
						myShuttle = (Pawn)container[i];
				}
				var mapComp = __instance.Map.GetComponent<ShipHeatMapComp>().ShipCombatOriginMap;
				ShipHeatMapComp playerMapComp = null;
				if (mapComp != null)
					playerMapComp = mapComp.GetComponent<ShipHeatMapComp>();
				for (int i = container.Count - 1; i >= 0; i--)
				{
					if (container[i] is Pawn)
					{
						GenPlace.TryPlaceThing(container[i], __instance.Position, __instance.Map, ThingPlaceMode.Near, delegate (Thing thing, int count) {
							PawnUtility.RecoverFromUnwalkablePositionOrKill(thing.Position, thing.Map);
							if (thing.Faction != Faction.OfPlayer && playerMapComp != null && playerMapComp.ShipLord != null)
								playerMapComp.ShipLord.AddPawn((Pawn)thing);
							if (thing.TryGetComp<CompShuttleCosmetics>() != null)
								CompShuttleCosmetics.ChangeShipGraphics((Pawn)thing, ((Pawn)thing).TryGetComp<CompShuttleCosmetics>().Props);
						});
					}
					else if (myShuttle != null)
						myShuttle.inventory.innerContainer.TryAddOrTransfer(container[i]);
				}

				__instance.innerContainer.ClearAndDestroyContents(DestroyMode.Vanish);
				CellRect cellRect = __instance.OccupiedRect();

				for (int j = 0; j < cellRect.Area * __instance.def.skyfaller.motesPerCell; j++)
				{
					FleckMaker.ThrowDustPuff(cellRect.RandomVector3, __instance.Map, 2f);
				}
				if (__instance.def.skyfaller.cameraShake > 0f && __instance.Map == Find.CurrentMap)
				{
					Find.CameraDriver.shaker.DoShake(__instance.def.skyfaller.cameraShake);
				}
				if (__instance.def.skyfaller.impactSound != null)
				{
					__instance.def.skyfaller.impactSound.PlayOneShot(SoundInfo.InMap(new TargetInfo(__instance.Position, __instance.Map, false), MaintenanceType.None));
				}
				__instance.Destroy(DestroyMode.Vanish);

				if (myShuttle.Faction != Faction.OfPlayer)
				{
					if (myShuttle.Position.Roofed(myShuttle.Map) && Rand.Chance(0.5f))
					{
						Traverse.Create(myShuttle.TryGetComp<CompRefuelable>()).Field("fuel").SetValue(0);
						myShuttle.Destroy();
					}
					else
						myShuttle.GetComp<CompBecomeBuilding>().transform();
				}
				else if (myShuttle.Position.Fogged(myShuttle.Map))
					FloodFillerFog.FloodUnfog(myShuttle.Position, myShuttle.Map);
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Pawn), "GetGizmos")]
	public static class ShuttleGizmoFix
	{
		public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
		{
			if (__instance == null || __result == null)
				return;
			if (__instance.TryGetComp<CompBecomeBuilding>() != null)
			{
				List<Gizmo> newList = new List<Gizmo>();
				foreach (Gizmo g in __result)
				{
					newList.Add(g);
				}
				if (__instance.drafter == null)
				{
					__instance.drafter = new Pawn_DraftController(__instance);
					__instance.equipment = new Pawn_EquipmentTracker(__instance);
				}
				IEnumerable<Gizmo> draftGizmos = (IEnumerable<Gizmo>)typeof(Pawn_DraftController).GetMethod("GetGizmos", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance.drafter, new object[] { });
				foreach (Gizmo c2 in draftGizmos)
				{
					newList.Add(c2);
				}
				foreach (ThingComp comp in __instance.AllComps)
				{
					foreach (Gizmo com in comp.CompGetGizmosExtra())
					{
						newList.Add(com);
					}
				}
				__result = newList;
			}
		}
	}

	[HarmonyPatch(typeof(Pawn), "IsColonyMech", MethodType.Getter)] //1.4
	public static class MechGizmoFix
	{
		public static bool Postfix(bool __result, Pawn __instance)
		{
			if (AccessExtensions.Utility.shuttleCache.Contains(__instance)) return false;
			return __result;
		}
	}

	[HarmonyPatch(typeof(Pawn_DraftController), "ShowDraftGizmo", MethodType.Getter)] //1.4
	public static class GizmoFix
	{
		public static void Postfix(Pawn_DraftController __instance, ref bool __result)
		{
			if (__instance.pawn.TryGetComp<CompBecomeBuilding>() != null)
				__result = true;
		}
	}

	[HarmonyPatch(typeof(FloatMenuMakerMap), "CanTakeOrder")]
	public static class OrderFix
	{
		public static void Postfix(Pawn pawn, ref bool __result)
		{
			if (pawn.TryGetComp<CompBecomeBuilding>() != null)
				__result = true;
		}
	}

	[HarmonyPatch(typeof(Caravan), "GetGizmos")]
	public static class OtherGizmoFix
	{
		public static void Postfix(Caravan __instance, ref IEnumerable<Gizmo> __result)
		{
			if (__instance == null || __result == null)
				return;

			List<Gizmo> newList = new List<Gizmo>();
			foreach (Gizmo g in __result)
			{
				newList.Add(g);
			}

			float shuttleCarryWeight = 0;
			float pawnWeight = 0;
			float minRange = float.MaxValue;
			bool allFullyFueled = true;
			List<Pawn> shuttlesToRefuel = new List<Pawn>();
			List<Thing> CaravanThings = CaravanInventoryUtility.AllInventoryItems(__instance);
			foreach (Pawn p in __instance.pawns)
			{
				if (p.TryGetComp<CompBecomeBuilding>() != null)
				{
					shuttleCarryWeight += p.TryGetComp<CompBecomeBuilding>().Props.buildingDef.GetCompProperties<CompProperties_Transporter>().massCapacity;
					if (p.TryGetComp<CompRefuelable>() != null && p.TryGetComp<CompRefuelable>().Fuel / p.TryGetComp<CompBecomeBuilding>().Props.buildingDef.GetCompProperties<CompProperties_ShuttleLaunchable>().fuelPerTile < minRange)
					{
						minRange = p.TryGetComp<CompRefuelable>().Fuel / p.TryGetComp<CompBecomeBuilding>().Props.buildingDef.GetCompProperties<CompProperties_ShuttleLaunchable>().fuelPerTile;
					}
					if (p.TryGetComp<CompRefuelable>() != null && p.TryGetComp<CompRefuelable>().FuelPercentOfMax < 0.8f)
					{
						foreach (Thing t in CaravanThings)
						{
							if (p.TryGetComp<CompRefuelable>().Props.fuelFilter.Allows(t.def))
							{
								shuttlesToRefuel.Add(p);
								break;
							}
						}
						allFullyFueled = false;
					}
				}
				else if (p.TryGetComp<CompShuttleLaunchable>() == null)
				{
					pawnWeight += p.def.BaseMass;
				}
			}
			if (shuttleCarryWeight > 0)
			{
				float totalMass = pawnWeight + __instance.MassUsage;
				Gizmo launchGizmo = new Command_Action
				{
					defaultLabel = "Launch Caravan",
					defaultDesc = "Load this caravan into shuttle(s) and launch it",
					icon = CompShuttleLaunchable.LaunchCommandTex,
					action = delegate
					{
						ShuttleCaravanUtility.LaunchMe(__instance, minRange, allFullyFueled);
					}
				};

				if (totalMass > shuttleCarryWeight)
					launchGizmo.Disable("Caravan is too heavy for shuttle(s) to carry: " + totalMass + "/" + shuttleCarryWeight);

				newList.Add(launchGizmo);
			}
			if (shuttlesToRefuel.Count > 0)
			{
				Gizmo refuelGizmo = new Command_Action
				{
					defaultLabel = "Refuel Shuttles",
					defaultDesc = "Use caravan inventory to refuel shuttle(s)",
					icon = CompShuttleLaunchable.SetTargetFuelLevelCommand,
					action = delegate {
						ShuttleCaravanUtility.RefuelMe(__instance, shuttlesToRefuel);
					}
				};

				newList.Add(refuelGizmo);
			}

			List<MinifiedThing> inactiveShuttles = new List<MinifiedThing>();
			foreach (Thing t in __instance.AllThings)
			{
				if (t is MinifiedThing)
				{
					MinifiedThing building = (MinifiedThing)t;
					if (building.InnerThing.TryGetComp<CompShuttleLaunchable>() != null)
					{
						inactiveShuttles.Add(building);
					}
				}
			}
			List<MinifiedThing> fuelableShuttles = new List<MinifiedThing>();
			foreach (MinifiedThing building in inactiveShuttles)
			{
				if (building.InnerThing.TryGetComp<CompRefuelable>() == null)
				{
					fuelableShuttles.Add(building);
				}
				else if (building.InnerThing.TryGetComp<CompRefuelable>().HasFuel)
				{
					fuelableShuttles.Add(building);
				}
				else
				{
					foreach (Thing tee in CaravanInventoryUtility.AllInventoryItems(__instance))
					{
						if (building.InnerThing.TryGetComp<CompRefuelable>().Props.fuelFilter.Allows(tee.def))
						{
							fuelableShuttles.Add(building);
							break;
						}
					}
				}
			}
			if (fuelableShuttles.Count > 0)
			{
				Gizmo activateGizmo = new Command_Action
				{
					defaultLabel = "Activate Shuttles",
					defaultDesc = "Activate shuttle(s) and refuel them if possible",
					icon = CompShuttleLaunchable.SetTargetFuelLevelCommand,
					action = delegate {
						ShuttleCaravanUtility.ActivateMe(__instance, fuelableShuttles);
					}
				};

				newList.Add(activateGizmo);
			}

			__result = newList;
		}
	}

	[HarmonyPatch(typeof(MassUtility), "Capacity")]
	public static class FixShuttleCarryCap
	{
		public static void Postfix(ref float __result, Pawn p)
		{
			if (p.TryGetComp<CompBecomeBuilding>() != null)
			{
				__result = p.TryGetComp<CompBecomeBuilding>().Props.buildingDef.GetCompProperties<CompProperties_Transporter>().massCapacity;
			}
		}
	}

	[HarmonyPatch(typeof(CaravanUIUtility), "AddPawnsSections")]
	public static class UIFix
	{
		public static void Postfix(TransferableOneWayWidget widget, List<TransferableOneWay> transferables)
		{
			if (Find.WorldSelector.FirstSelectedObject == null || !(Find.WorldSelector.FirstSelectedObject is MapParent) || ((MapParent)Find.WorldSelector.FirstSelectedObject).Map == null || !((MapParent)Find.WorldSelector.FirstSelectedObject).Map.IsPlayerHome)
			{
				IEnumerable<TransferableOneWay> source = from x in transferables
														 where x.ThingDef.category == ThingCategory.Pawn
														 select x;
				widget.AddSection(TranslatorFormattedStringExtensions.Translate("SoSShuttles"), from x in source
																								where (((Pawn)x.AnyThing).TryGetComp<CompBecomeBuilding>() != null)
																								select x);
			}
		}
	}

	[HarmonyPatch(typeof(TravelingTransportPods), "Start", MethodType.Getter)]
	public static class FromSpaceship
	{
		public static void Postfix(TravelingTransportPods __instance, ref Vector3 __result)
		{
			int initialTile = (int)typeof(TravelingTransportPods).GetField("initialTile", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);
			foreach (WorldObject ship in Find.World.worldObjects.AllWorldObjects.Where(o => o is WorldObjectOrbitingShip))
				if (ship.Tile == initialTile)
					__result = ship.DrawPos;
			foreach (WorldObject site in Find.World.worldObjects.AllWorldObjects.Where(o => o is SpaceSite || o is MoonBase))
				if (site.Tile == initialTile)
					__result = site.DrawPos;
		}
	}

	[HarmonyPatch(typeof(TravelingTransportPods), "End", MethodType.Getter)]
	public static class ToSpaceship
	{
		public static void Postfix(TravelingTransportPods __instance, ref Vector3 __result)
		{
			int destTile = __instance.destinationTile;
			foreach (WorldObject ship in Find.World.worldObjects.AllWorldObjects.Where(o => o is WorldObjectOrbitingShip))
				if (ship.Tile == destTile)
					__result = ship.DrawPos;
			foreach (WorldObject site in Find.World.worldObjects.AllWorldObjects.Where(o => o is SpaceSite || o is MoonBase))
				if (site.Tile == destTile)
					__result = site.DrawPos;
		}
	}

	[HarmonyPatch(typeof(TransportPodsArrivalAction_GiveToCaravan), "StillValid")]
	public static class MakeSureNotToLoseYourShuttle
	{
		static bool hasShuttle = false;
		public static bool Prefix(IEnumerable<IThingHolder> pods)
		{
			hasShuttle = false;
			foreach (IThingHolder pod in pods)
			{
				foreach (Thing t in pod.GetDirectlyHeldThings())
				{
					if (t.TryGetComp<CompBecomeBuilding>() != null)
					{
						hasShuttle = true;
						return false;
					}
				}
			}
			return true;
		}
		public static void Postfix(ref FloatMenuAcceptanceReport __result)
		{
			if (hasShuttle)
				__result = true;
		}
	}

	[HarmonyPatch(typeof(PawnCapacitiesHandler), "CapableOf")]
	public static class ShuttlesCannotConstruct //This is slow and shitty, but Tynan didn't leave us many options to avoid a nullref
	{
		public static void Postfix(PawnCapacityDef capacity, PawnCapacitiesHandler __instance, ref bool __result)
		{
			if (capacity == PawnCapacityDefOf.Manipulation && __instance.pawn.TryGetComp<CompBecomeBuilding>() != null)
			{
				__result = false;
			}
		}
	}

	[HarmonyPatch(typeof(Pawn_MeleeVerbs), "ChooseMeleeVerb")]
	public static class ThatWasAnOldBug
	{
		public static bool Prefix(Pawn_MeleeVerbs __instance)
		{
			return __instance.Pawn.TryGetComp<CompBecomeBuilding>() == null;
		}
	}

	[HarmonyPatch(typeof(Skyfaller), "HitRoof")]
	public static class ShuttleBayAcceptsShuttle
	{
		public static bool Prefix(Skyfaller __instance)
		{
			if (__instance.Position.GetThingList(__instance.Map).Any(t =>
				t.def == ResourceBank.ThingDefOf.ShipShuttleBay || t.def == ResourceBank.ThingDefOf.ShipShuttleBayLarge || t.def == ResourceBank.ThingDefOf.ShipSalvageBay))
			{
				return false;
			}
			if (__instance.Map.GetComponent<ShipHeatMapComp>().InCombat && __instance.def.defName.Equals("ShuttleIncomingPersonal"))
			{
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(TransportPodsArrivalActionUtility), "DropTravelingTransportPods")]
	public static class ShuttleBayArrivalPrecision
	{
		public static bool Prefix(List<ActiveDropPodInfo> dropPods, IntVec3 near, Map map)
		{
			if (map.Parent != null && map.Parent.def == ResourceBank.WorldObjectDefOf.ShipOrbiting)
			{
				TransportPodsArrivalActionUtility.RemovePawnsFromWorldPawns(dropPods);
				for (int i = 0; i < dropPods.Count; i++)
				{
					DropPodUtility.MakeDropPodAt(near, map, dropPods[i]);
				}

				return false;
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(ShipLandingArea), "RecalculateBlockingThing")]
	public static class ShipLandingAreaUnderShipRoof
	{
		public static bool Prefix(Map ___map, CellRect ___rect, ref bool ___blockedByRoof, ref Thing ___firstBlockingThing)
		{
			___blockedByRoof = false;
			foreach (IntVec3 c in ___rect)
			{
				if (c.Roofed(___map) && ___map.roofGrid.RoofAt(c) == ResourceBank.RoofDefOf.RoofShip)
				{
					List<Thing> thingList = c.GetThingList(___map);
					for (int i = 0; i < thingList.Count; i++)
					{
						if ((!(thingList[i] is Pawn) && (thingList[i].def.Fillage != FillCategory.None || thingList[i].def.IsEdifice() || thingList[i] is Skyfaller)) && thingList[i].def != ResourceBank.ThingDefOf.ShipShuttleBay && thingList[i].def != ResourceBank.ThingDefOf.ShipShuttleBayLarge && !(thingList[i].TryGetComp<CompSoShipPart>()?.Props.isPlating ?? false))
						{
							___firstBlockingThing = thingList[i];
							return false;
						}
					}
				}
				else
					return true;
			}
			___firstBlockingThing = null;
			return false;
		}
	}

	[HarmonyPatch(typeof(DropCellFinder), "TradeDropSpot")]
	public static class InSpaceDropStuffInsideMe
	{
		public static void Postfix(Map map, ref IntVec3 __result)
		{
			//find first salvagebay
			Building b = map.listerBuildings.allBuildingsColonist.Where(x => x.def == ResourceBank.ThingDefOf.ShipSalvageBay).FirstOrDefault();
			if (map.IsSpace() && b != null)
				__result = b.Position;
		}
	}

	[HarmonyPatch(typeof(Dialog_LoadTransporters), "AddPawnsToTransferables", null)]
	public static class TransportPrisoners_Patch
	{
		public static bool Prefix(Dialog_LoadTransporters __instance)
		{
			List<Pawn> list = CaravanFormingUtility.AllSendablePawns(
				(Map)typeof(Dialog_LoadTransporters)
					.GetField("map", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance), true,
				true);
			for (int i = 0; i < list.Count; i++)
			{
				typeof(Dialog_LoadTransporters)
					.GetMethod("AddToTransferables", BindingFlags.NonPublic | BindingFlags.Instance)
					.Invoke(__instance, new object[1] { list[i] });
			}

			return false;
		}
	}

	[HarmonyPatch(typeof(QuestPart_DropPods), "GetRandomDropSpot")]
	public static class DropIntoShuttleBay
	{
		public static void Postfix(QuestPart_DropPods __instance, ref IntVec3 __result)
		{
			if (__instance.mapParent.Map.IsSpace())
			{
				IEnumerable<Thing> bays = __instance.mapParent.Map.listerThings.AllThings.Where(t => t.def == ResourceBank.ThingDefOf.ShipShuttleBay || t.def == ResourceBank.ThingDefOf.ShipShuttleBayLarge || t.def == ResourceBank.ThingDefOf.ShipSalvageBay);
				if (bays.Any())
				{
					__result = bays.RandomElement().Position;
				}
			}
		}
	}

    /*causes lag
	[HarmonyPatch(typeof(ShipLandingBeaconUtility), "GetLandingZones")]
    public static class RoyaltyShuttlesLandOnBays
    {
        public static void Postfix(Map map, ref List<ShipLandingArea> __result)
        {
            foreach (Building landingSpot in map.listerBuildings.AllBuildingsColonistOfDef(ResourceBank.ThingDefOf.ShipShuttleBay))
            {
                ShipLandingArea area = new ShipLandingArea(landingSpot.OccupiedRect(), map);
                area.RecalculateBlockingThing();
                __result.Add(area);
            }
            foreach (Building landingSpot in map.listerBuildings.AllBuildingsColonistOfDef(ResourceBank.ThingDefOf.ShipShuttleBayLarge))
            {
                ShipLandingArea area = new ShipLandingArea(landingSpot.OccupiedRect(), map);
                area.RecalculateBlockingThing();
                __result.Add(area);
            }
        }
    }*/

    /*[HarmonyPatch(typeof(ActiveDropPod),"PodOpen")]
	public static class ActivePodFix{
		public static bool Prefix (ref ActiveDropPod __instance)
		{
			if(__instance.def.defName.Equals("ActiveShuttle"))
			{
				ThingOwner stuffInPod = ((ActiveDropPodInfo)typeof(ActiveDropPod).GetField ("contents", BindingFlags.Instance | BindingFlags.NonPublic).GetValue (__instance)).innerContainer;
				Pawn shuttleLanded = null;
				List<Thing> fillTheShuttle = new List<Thing> ();
				for (int i = stuffInPod.Count - 1; i >= 0; i--)
				{
					Thing thing = stuffInPod[i];
					if (thing is Pawn) {
						Pawn pawn = (Pawn)thing;
						GenPlace.TryPlaceThing (thing, __instance.Position, __instance.Map, ThingPlaceMode.Near);
						if (thing.TryGetComp<CompBecomeBuilding> () != null)
							shuttleLanded = pawn;
						if (pawn.RaceProps.Humanlike) {
							TaleRecorder.RecordTale (TaleDefOf.LandedInPod, new object[] {
								pawn
							});
						}
						if (pawn.IsColonist && pawn.Spawned && !__instance.Map.IsPlayerHome) {
							pawn.drafter.Drafted = true;
						}
					} else
						fillTheShuttle.Add (thing);
				}
				if (shuttleLanded != null) {
					ThingOwner shuttleInventory = shuttleLanded.inventory.innerContainer;
					foreach (Thing thing in fillTheShuttle) {
						stuffInPod.Remove (thing);
						shuttleInventory.TryAdd (thing);
					}
				}
				stuffInPod.ClearAndDestroyContents(DestroyMode.Vanish);
				SoundDef.Named("DropPodOpen").PlayOneShot(new TargetInfo(__instance.Position, __instance.Map, false));
				__instance.Destroy(DestroyMode.Vanish);
				return false;
			}
			return true;
		}
	}*/

    /*[HarmonyPatch(typeof(Pawn))]
	[HarmonyPatch("IsColonist",MethodType.Getter)]
	public static class GizmoFix{
		public static void Postfix(Pawn __instance, ref bool __result)
		{
			if (__instance.TryGetComp<CompBecomeBuilding> () != null && !System.Environment.StackTrace.Contains("AllMapsCaravansAndTravelingTransportPods_Colonists")) {
				__result=true;
				if (__instance.drafter == null) {
					__instance.drafter = new Pawn_DraftController (__instance);
				}
				if (__instance.equipment == null) {
					__instance.equipment = new Pawn_EquipmentTracker (__instance);
				}
			}
		}
	}*/

    //EVA
    [HarmonyPatch(typeof(Pawn_PathFollower), "SetupMoveIntoNextCell")]
	public static class EVAMovesFastInSpace
	{
		public static void Postfix(Pawn_PathFollower __instance, Pawn ___pawn)
		{
			if (___pawn.Map.terrainGrid.TerrainAt(__instance.nextCell) == ResourceBank.TerrainDefOf.EmptySpace &&
				ShipInteriorMod2.EVAlevel(___pawn) > 6)
			{
				__instance.nextCellCostLeft /= 4;
				__instance.nextCellCostTotal /= 4;
			}
		}
	}

	[HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Notify_ApparelAdded))]
	public static class ApparelTracker_Notify_Added
	{
		internal static void Postfix(Pawn_ApparelTracker __instance)
		{
			ShipInteriorMod2.WorldComp.PawnsInSpaceCache.RemoveAll(p => p.Key == __instance?.pawn?.thingIDNumber);
		}
	}

	[HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Notify_ApparelRemoved))]
	public static class ApparelTracker_Notify_Removed
	{
		internal static void Postfix(Pawn_ApparelTracker __instance)
		{
			ShipInteriorMod2.WorldComp.PawnsInSpaceCache.RemoveAll(p => p.Key == __instance?.pawn?.thingIDNumber);
		}
	}

	[HarmonyPatch(typeof(Recipe_InstallArtificialBodyPart), "ApplyOnPawn")]
	public static class LungInstall
	{
		internal static void Postfix(Pawn pawn, BodyPartRecord part, Recipe_InstallArtificialBodyPart __instance)
		{
			if (__instance.recipe.addsHediff == ResourceBank.HediffDefOf.SoSArchotechLung)
				ShipInteriorMod2.WorldComp.PawnsInSpaceCache.RemoveAll(p => p.Key == pawn?.thingIDNumber);
		}
	}

	[HarmonyPatch(typeof(Recipe_RemoveBodyPart), "ApplyOnPawn")]
	public static class LungRemove
	{
		internal static void Postfix(Pawn pawn, BodyPartRecord part)
		{
			if (part.def.defName.Equals("SoSArchotechLung"))
				ShipInteriorMod2.WorldComp.PawnsInSpaceCache.RemoveAll(p => p.Key == pawn?.thingIDNumber);
		}
	}

	[HarmonyPatch(typeof(Recipe_InstallImplant), "ApplyOnPawn")]
	public static class SkinInstall
	{
		internal static void Postfix(Pawn pawn, BodyPartRecord part, Recipe_InstallImplant __instance)
		{
			if (__instance.recipe.addsHediff == ResourceBank.HediffDefOf.SoSArchotechSkin)
				ShipInteriorMod2.WorldComp.PawnsInSpaceCache.RemoveAll(p => p.Key == pawn?.thingIDNumber);
		}
	}

	[HarmonyPatch(typeof(Recipe_RemoveImplant), "ApplyOnPawn")]
	public static class SkinRemove
	{
		internal static void Postfix(Pawn pawn, BodyPartRecord part)
		{
			if (part.def.defName.Equals("SoSArchotechSkin"))
				ShipInteriorMod2.WorldComp.PawnsInSpaceCache.RemoveAll(p => p.Key == pawn?.thingIDNumber);
		}
	}

	[HarmonyPatch(typeof(Pawn), "Kill")]
	public static class DeathRemove
	{
		internal static void Postfix(Pawn __instance)
		{
			ShipInteriorMod2.WorldComp.PawnsInSpaceCache.RemoveAll(p => p.Key == __instance.thingIDNumber);
		}
	}

    [HarmonyPatch(typeof(ThingDef), "SpecialDisplayStats")] //would be better as an actual stat display but this ll do
    public static class AddEVADescription
    {
        internal static void Postfix(ref IEnumerable<StatDrawEntry> __result, ThingDef __instance)
        {
			if (__instance.IsApparel)
            {
                //bool eva = false;
                if (__instance.apparel.tags != null && __instance.apparel.tags.Contains("EVA"))
                {
                    //eva = true;
					if (!__instance.description.EndsWith("EVA capable."))
						__instance.description += "\n\nEVA capable.";
                }
                //__result.AddItem(new StatDrawEntry(StatCategoryDefOf.Apparel, "EVA", eva.ToString(), "EVAdesc", 2756, null, null, false));
            }
        }
    }

    //pawns
    [HarmonyPatch(typeof(PreceptComp_Apparel), "GiveApparelToPawn")]
	public static class PreventIdeoApparel
	{
		public static bool Prefix(Pawn pawn)
		{
			if (pawn.kindDef.defName.StartsWith("Apparel_Space"))
			{
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(PawnRelationWorker), "CreateRelation")]
	public static class PreventRelations
	{
		public static bool Prefix(Pawn generated, Pawn other)
		{
			if (!generated.RaceProps.Humanlike || !other.RaceProps.Humanlike || generated.kindDef.defName.Contains("Space") || other.kindDef.defName.Contains("Space"))
			{
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Trigger_UrgentlyHungry), "ActivateOn")]
	public static class MechsDontEat
	{
		public static bool Prefix(Lord lord, out bool __state)
		{
			__state = false;
			foreach (Pawn p in lord.ownedPawns)
			{
				if (p.RaceProps.IsMechanoid)
				{
					__state = true;
					return false;
				}
			}
			return true;
		}
		public static void Postfix(ref bool __result, bool __state)
		{
			if (__state)
				__result = false;
		}
	}

	[HarmonyPatch(typeof(TransferableUtility), "CanStack")]
	public static class MechsCannotStack
	{
		public static bool Prefix(Thing thing, ref bool __result)
		{
			if (thing is Pawn && ((Pawn)thing).RaceProps.IsMechanoid)
			{
				__result = false;
				return false;
			}

			return true;
		}
	}

	/*No longer necessary in 1.4
	[HarmonyPatch(typeof(Pawn), "GetGizmos")]
	public static class AnimalsHaveGizmosToo
	{
		public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
		{
			if (__instance.TryGetComp<CompArcholife>() != null)
			{
				List<Gizmo> giz = new List<Gizmo>();
				giz.AddRange(__result);
				giz.AddRange(__instance.TryGetComp<CompArcholife>().CompGetGizmosExtra());
				__result = giz;
			}
		}
	}*/

	[HarmonyPatch(typeof(CompSpawnerPawn), "TrySpawnPawn")]
	public static class SpaceCreaturesAreHungry
	{
		public static void Postfix(ref Pawn pawn, bool __result)
		{
			if (__result && (pawn?.Map?.IsSpace() ?? false) && pawn.needs?.food?.CurLevel != null)
				pawn.needs.food.CurLevel = 0.2f;
		}
	}

	[HarmonyPatch(typeof(Pawn_FilthTracker), "GainFilth", new Type[] { typeof(ThingDef), typeof(IEnumerable<string>) })]
	public static class RadioactiveAshIsRadioactive
	{
		public static void Postfix(ThingDef filthDef, Pawn_FilthTracker __instance, Pawn ___pawn)
		{
			if (filthDef.defName.Equals("Filth_SpaceReactorAsh"))
			{
				int damage = Rand.RangeInclusive(1, 2);
				___pawn.TakeDamage(new DamageInfo(DamageDefOf.Burn, damage));
				float num = 0.025f;
				num *= (1 - ___pawn.GetStatValue(StatDefOf.ToxicResistance, true));
				if (num != 0f)
				{
					HealthUtility.AdjustSeverity(___pawn, HediffDefOf.ToxicBuildup, num);
				}
			}
		}
	}

	[HarmonyPatch(typeof(MapPawns), "AllPawns", MethodType.Getter)]
	public class FixCaravanThreading
	{
		public static void Postfix(ref List<Pawn> __result)
		{
			__result = __result.ListFullCopy();
		}
	}

	[HarmonyPatch(typeof(Pawn_MindState), "Notify_DamageTaken")]
	public static class ShipTurretIsNull
	{
		public static bool Prefix(DamageInfo dinfo, Pawn_MindState __instance)
		{
			if (dinfo.Instigator is Building_ShipTurret)
			{
				if (Traverse.Create<Pawn_MindState>().Method("CanStartFleeingBecauseOfPawnAction", __instance.pawn).GetValue<bool>())
				{
					__instance.StartFleeingBecauseOfPawnAction(dinfo.Instigator);
					return false;
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(RCellFinder), "CanSelfShutdown")]
	public static class AllowMechSleepShipFloor
	{
		public static bool Prefix(ref bool __result, Pawn pawn, IntVec3 c, Map map, bool allowForbidden)
		{
			if (c.GetFirstBuilding(map) != null && (c.GetFirstBuilding(map).TryGetComp<CompSoShipPart>()?.Props.isPlating ?? false))
			{
				//check all except building
				__result = true;
				if (!pawn.CanReserve(c, 1, -1, null, false))
				{
					__result = false;
					return false;
				}
				if (!pawn.CanReach(c, PathEndMode.OnCell, Danger.Some, false, false, TraverseMode.ByPawn))
				{
					__result = false;
					return false;
				}
				if (!c.Standable(map))
				{
					__result = false;
					return false;
				}
				if (!allowForbidden && c.IsForbidden(pawn))
				{
					__result = false;
					return false;
				}
				Room room = c.GetRoom(map);
				if (room != null && room.IsPrisonCell)
				{
					__result = false;
					return false;
				}
				for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
				{
					List<Thing> thingList = (c + GenAdj.CardinalDirections[i]).GetThingList(map);
					for (int j = 0; j < thingList.Count; j++)
					{
						if (thingList[j].def.hasInteractionCell && thingList[j].InteractionCell == c)
						{
							__result = false;
							return false;
						}
					}
				}
				return false;
			}
			return true;
		}
	}

    [HarmonyPatch(typeof(PawnGraphicSet), "SetAllGraphicsDirty")]
    public static class PreserveCosmetics
    {
        public static void Postfix(PawnGraphicSet __instance)
        {
            CompArcholifeCosmetics cosmetics = __instance.pawn.TryGetComp<CompArcholifeCosmetics>();
            if (cosmetics != null)
                CompArcholifeCosmetics.ChangeAnimalGraphics(__instance.pawn, cosmetics.Props, cosmetics);
        }
    }

    [HarmonyPatch(typeof(ComplexThreatWorker_SleepingInsects), "GetPawnKindsForPoints")]
    public static class NoArchoSpiderSpawnInComplexes
    {
        public static bool Prefix()
        {
            return false;
        }
        public static void Postfix(ref IEnumerable<PawnKindDef>__result, float points)
        {
			__result = PawnUtility.GetCombatPawnKindsForPoints((PawnKindDef k) => k.RaceProps.Insect && !k.defName.Equals("Archospider"), points, null);
        }
	}

    // Formgels - simpler than holograms!
    [HarmonyPatch(typeof(Pawn), "Kill")]
	public static class CorpseRemoval
	{
		public static void Postfix(Pawn __instance)
		{
			if (ShipInteriorMod2.IsHologram(__instance))
			{
				if (__instance.Corpse != null)
					__instance.Corpse.Destroy();
				if (!__instance.health.hediffSet.GetFirstHediff<HediffPawnIsHologram>().consciousnessSource.Destroyed)
					ResurrectionUtility.Resurrect(__instance);
			}
		}
	}

	[HarmonyPatch(typeof(ThoughtWorker_AgeReversalDemanded), "CanHaveThought")]
	public static class NoHologramAgeReversal
	{
		public static void Postfix(ref bool __result, Pawn pawn)
		{
			if (ShipInteriorMod2.IsHologram(pawn))
				__result = false;
		}
	}

	[HarmonyPatch(typeof(SkillRecord), "Interval")]
	public static class MachineHologramsPerfectMemory
	{
		public static bool Prefix(SkillRecord __instance)
		{
			return !ShipInteriorMod2.IsHologram(__instance.Pawn);
		}
	}

	[HarmonyPatch(typeof(Pawn_StoryTracker), "get_SkinColor")]
	public static class SkinColorPostfixPostfix
	{
		[HarmonyPriority(Priority.Last)]
		public static void Postfix(Pawn ___pawn, ref Color __result, Pawn_StoryTracker __instance)
		{
			if (ShipInteriorMod2.IsHologram(___pawn) && __instance.skinColorOverride.HasValue)
				__result = __instance.skinColorOverride.Value;
		}
	}

	[HarmonyPatch(typeof(Recipe_BloodTransfusion), "AvailableOnNow")]
	public static class FormgelsHaveNoBlood
	{
		public static void Postfix(ref bool __result, Thing thing)
		{
			if (thing is Pawn && ShipInteriorMod2.IsHologram(((Pawn)thing)))
				__result = false;
		}
	}

	[HarmonyPatch(typeof(Recipe_ExtractHemogen), "AvailableOnNow")]
	public static class FormgelsStillHaveNoBlood
	{
		public static void Postfix(ref bool __result, Thing thing)
		{
			if (thing is Pawn && ShipInteriorMod2.IsHologram(((Pawn)thing)))
				__result = false;
		}
	}

	[HarmonyPatch(typeof(Recipe_InstallArtificialBodyPart), "GetPartsToApplyOn")]
	public static class FormgelsCannotUseBionics
	{
		public static void Postfix(ref IEnumerable<BodyPartRecord> __result, Pawn pawn)
		{
			if (ShipInteriorMod2.IsHologram(pawn))
				__result = new List<BodyPartRecord>();
		}
	}

	[HarmonyPatch(typeof(Recipe_InstallImplant), "GetPartsToApplyOn")]
	public static class FormgelsCannotUseImplants
	{
		public static void Postfix(ref IEnumerable<BodyPartRecord> __result, Pawn pawn)
		{
			if (ShipInteriorMod2.IsHologram(pawn))
				__result = new List<BodyPartRecord>();
		}
	}

	[HarmonyPatch(typeof(Recipe_RemoveImplant), "GetPartsToApplyOn")]
	public static class FormgelsStillCannotUseImplants
	{
		public static void Postfix(ref IEnumerable<BodyPartRecord> __result, Pawn pawn)
		{
			if (ShipInteriorMod2.IsHologram(pawn))
				__result = new List<BodyPartRecord>();
		}
	}

	[HarmonyPatch(typeof(Recipe_InstallNaturalBodyPart), "GetPartsToApplyOn")]
	public static class FormgelsCannotUseOrgans
	{
		public static void Postfix(ref IEnumerable<BodyPartRecord> __result, Pawn pawn)
		{
			if (ShipInteriorMod2.IsHologram(pawn))
				__result = new List<BodyPartRecord>();
		}
	}

	[HarmonyPatch(typeof(Recipe_RemoveBodyPart), "GetPartsToApplyOn")]
	public static class FormgelsHaveNoOrgans
	{
		public static void Postfix(ref IEnumerable<BodyPartRecord> __result, Pawn pawn)
		{
			if (ShipInteriorMod2.IsHologram(pawn))
				__result = new List<BodyPartRecord>();
		}
	}

	[HarmonyPatch(typeof(GenStep_Fog), "Generate")]
	public static class UnfogVault
	{
		public static void Postfix(Map map)
		{
			foreach (Thing casket in map.listerThings.ThingsOfDef(ThingDef.Named("Ship_AvatarCasket")))
			{
				FloodFillerFog.FloodUnfog(casket.Position, map);
			}
		}
	}

	[HarmonyPatch(typeof(MechanitorUtility), "IsMechanitor")]
	public static class AICoreIsMechanitor //AI cores control mechanoids
	{
		public static void Postfix(Pawn pawn, ref bool __result)
		{
			if (pawn.health.hediffSet.HasHediff(HediffDef.Named("SoSHologramMachine")) || pawn.health.hediffSet.HasHediff(HediffDef.Named("SoSHologramArchotech")))
				__result = true;
		}
	}
	
	//archotech
	[HarmonyPatch(typeof(IncidentWorker_FarmAnimalsWanderIn), "TryFindRandomPawnKind")]
	public static class NoArchoCritters
	{
		public static void Postfix(ref PawnKindDef kind, ref bool __result, Map map)
		{
			__result = DefDatabase<PawnKindDef>.AllDefs.Where((PawnKindDef x) => x.RaceProps.Animal && x.RaceProps.wildness < 0.35f && (!x.race.tradeTags?.Contains("AnimalInsectSpace") ?? true) && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(x.race)).TryRandomElementByWeight((PawnKindDef k) => 0.420000017f - k.RaceProps.wildness, out kind);
		}
	}

	[HarmonyPatch(typeof(ScenPart_StartingAnimal), "RandomPets")]
	public static class NoArchotechPets
	{
		public static void Postfix(ref IEnumerable<PawnKindDef> __result)
		{
			List<PawnKindDef> newResult = new List<PawnKindDef>();
			foreach (PawnKindDef def in __result)
			{
				if (!def.race.HasComp(typeof(CompArcholife)))
					newResult.Add(def);
			}
			__result = newResult;
		}
	}

	[HarmonyPatch(typeof(MainTabWindow_Research), "PostOpen")]
	public static class HideArchoStuff
	{
		public static void Postfix(MainTabWindow_Research __instance, IEnumerable ___tabs)
		{
			if (!ShipInteriorMod2.WorldComp.Unlocks.Contains("ArchotechUplink"))
			{
				TabRecord archoTab = null;
				foreach (TabRecord tab in ___tabs)
				{
					if (tab.label.Equals("Archotech"))
						archoTab = tab;
				}
				___tabs.GetType().GetMethod("Remove").Invoke(___tabs, new object[] { archoTab });
			}
		}
	}

	[HarmonyPatch(typeof(Widgets), "RadioButtonLabeled")]
	public static class HideArchoStuffToo
	{
		public static bool Prefix(string labelText)
		{
			if (labelText.Equals("Sacrifice to archotech spore") && !ShipInteriorMod2.WorldComp.Unlocks.Contains("ArchotechUplink"))
			{
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(MainTabWindow_Research), "DrawUnlockableHyperlinks")]
	public static class DrawArchotechGifts
	{
		public static void Postfix(ref float __result, ref Rect rect, ResearchProjectDef project)
		{
			float yMin = rect.yMin;
			bool first = false;
			foreach (ArchotechGiftDef def in DefDatabase<ArchotechGiftDef>.AllDefs)
			{
				if (def.research == project)
				{
					if (!first)
					{
						first = true;
						Widgets.LabelCacheHeight(ref rect, TranslatorFormattedStringExtensions.Translate("ArchoGift") + ":");
						rect.yMin += 24f;
					}
					Widgets.HyperlinkWithIcon(hyperlink: new Dialog_InfoCard.Hyperlink(def.thing), rect: new Rect(rect.x, rect.yMin, rect.width, 24f));
					rect.yMin += 24f;
				}
			}
			__result = rect.yMin - yMin + __result;
		}
	}

	[HarmonyPatch(typeof(JobDriver_Meditate), "MeditationTick")]
	public static class MeditateToArchotechs
	{
		public static void Postfix(JobDriver_Meditate __instance)
		{
			int num = GenRadial.NumCellsInRadius(MeditationUtility.FocusObjectSearchRadius);
			for (int i = 0; i < num; i++)
			{
				IntVec3 c = __instance.pawn.Position + GenRadial.RadialPattern[i];
				if (c.InBounds(__instance.pawn.Map))
				{
					Building_ArchotechSpore spore = c.GetFirstThing<Building_ArchotechSpore>(__instance.pawn.Map);
					if (spore != null)
					{
						spore.MeditationTick();
					}
				}
			}
		}
	}

	[HarmonyPatch(typeof(RitualObligationTargetWorker_GraveWithTarget), "LabelExtraPart")]
	public static class NoDeathSpam
	{
		public static bool Prefix(RitualObligation obligation)
		{
			return obligation.targetA.Thing != null && obligation.targetA.Thing is Corpse && ((Corpse)obligation.targetA.Thing).InnerPawn != null;

		}
	}

	[HarmonyPatch(typeof(RitualObligationTargetWorker_Altar), "GetTargetsWorker")]
	public static class ArchotechSporesAreHoly
	{
		public static void Postfix(RitualObligation obligation, Map map, Ideo ideo, ref IEnumerable<TargetInfo> __result)
		{
			if (ideo.memes.Contains(ResourceBank.MemeDefOf.Structure_Archist) && map.listerThings.ThingsOfDef(ResourceBank.ThingDefOf.ShipArchotechSpore).Any())
			{
				List<TargetInfo> newResult = new List<TargetInfo>();
				newResult.AddRange(__result);
				foreach (Thing spore in map.listerThings.ThingsOfDef(ResourceBank.ThingDefOf.ShipArchotechSpore))
				{
					newResult.Add(spore);
				}
				__result = newResult;
			}
		}
	}

	[HarmonyPatch(typeof(IdeoBuildingPresenceDemand), "BuildingPresent")]
	public static class ArchotechSporesCountAsAltars
	{
		public static void Postfix(ref bool __result, Map map, IdeoBuildingPresenceDemand __instance)
		{
			if (__instance.parent.ideo.memes.Contains(ResourceBank.MemeDefOf.Structure_Archist) && map.listerThings.ThingsOfDef(ResourceBank.ThingDefOf.ShipArchotechSpore).Any())
				__result = true;
		}
	}

	[HarmonyPatch(typeof(IdeoBuildingPresenceDemand), "RequirementsSatisfied")]
	public static class ArchotechSporesCountAsAltarsToo
	{
		public static void Postfix(ref bool __result, Map map, IdeoBuildingPresenceDemand __instance)
		{
			if (__instance.parent.ideo.memes.Contains(ResourceBank.MemeDefOf.Structure_Archist) && map.listerThings.ThingsOfDef(ResourceBank.ThingDefOf.ShipArchotechSpore).Any())
				__result = true;
		}
	}

	[HarmonyPatch(typeof(ExecutionUtility), "DoExecutionByCut")]
	public static class ArchotechSporesAbsorbBrains
	{
		public static void Postfix(Pawn victim)
		{
			Building_ArchotechSpore ArchotechSpore = victim.Corpse.Position.GetFirstThing<Building_ArchotechSpore>(victim.Corpse.Map);
			if (ArchotechSpore != null)
			{
				SoundDefOf.PsychicPulseGlobal.PlayOneShotOnCamera(Find.CurrentMap);
				FleckMaker.Static(ArchotechSpore.Position, victim.Corpse.Map, FleckDefOf.PsycastAreaEffect, 10f);
				victim.health.AddHediff(HediffDefOf.MissingBodyPart, victim.RaceProps.body.GetPartsWithTag(BodyPartTagDefOf.ConsciousnessSource).First());
				ArchotechSpore.AbsorbMind(victim);
			}
		}
	}

	[HarmonyPatch(typeof(FactionDialogMaker), "FactionDialogFor")]
	public static class AddArchoDialogOption
	{
		public static void Postfix(Pawn negotiator, Faction faction, ref DiaNode __result)
		{
			if (faction.def.CanEverBeNonHostile && Find.ResearchManager.GetProgress(ResourceBank.ResearchProjectDefOf.ArchotechBroadManipulation) >= ResourceBank.ResearchProjectDefOf.ArchotechBroadManipulation.CostApparent)
			{
				Building_ArchotechSpore spore = null;
				foreach (Map map in Find.Maps)
				{
					if (map.IsSpace())
					{
						foreach (Thing t in map.spawnedThings)
						{
							if (t is Building_ArchotechSpore s)
							{
								spore = s;
								break;
							}
						}
					}
				}
				DiaOption increase = new DiaOption(TranslatorFormattedStringExtensions.Translate("ArchotechGoodwillPlus", 10));
				DiaOption decrease = new DiaOption(TranslatorFormattedStringExtensions.Translate("ArchotechGoodwillMinus", 10));
				increase.action = delegate
				{
					faction.TryAffectGoodwillWith(Faction.OfPlayer, 10, canSendMessage: false);
					spore.fieldStrength -= 10;
				};
				increase.linkLateBind = (() => FactionDialogMaker.FactionDialogFor(negotiator, faction));
				if (spore == null || spore.fieldStrength < 10)
				{
					increase.disabled = true;
					increase.disabledReason = "Insufficient psychic field strength";
				}
				decrease.action = delegate
				{
					faction.TryAffectGoodwillWith(Faction.OfPlayer, -10, canSendMessage: false);
					spore.fieldStrength -= 10;
				};
				decrease.linkLateBind = (() => FactionDialogMaker.FactionDialogFor(negotiator, faction));
				if (spore == null || spore.fieldStrength < 10)
				{
					decrease.disabled = true;
					decrease.disabledReason = "Insufficient psychic field strength";
				}
				if (spore != null)
				{
					__result.options.Add(increase);
					__result.options.Add(decrease);
				}
			}
		}
	}

	//mechanite "fire"
	[HarmonyPatch(typeof(Fire), "TrySpread")]
	public static class SpreadMechanites
	{
		public static bool Prefix(Fire __instance)
		{
			if (__instance is MechaniteFire)
				return false;
			return true;
		}

		public static void Postfix(Fire __instance)
		{
			if (__instance is MechaniteFire)
			{
				IntVec3 position = __instance.Position;
				bool flag;
				if (Rand.Chance(0.8f))
				{
					position = __instance.Position + GenRadial.ManualRadialPattern[Rand.RangeInclusive(1, 8)];
					flag = true;
				}
				else
				{
					position = __instance.Position + GenRadial.ManualRadialPattern[Rand.RangeInclusive(10, 20)];
					flag = false;
				}
				if (!position.InBounds(__instance.Map))
				{
					return;
				}
				if (!flag)
				{
					CellRect startRect = CellRect.SingleCell(__instance.Position);
					CellRect endRect = CellRect.SingleCell(position);
					if (GenSight.LineOfSight(__instance.Position, position, __instance.Map, startRect, endRect))
					{
						((MechaniteSpark)GenSpawn.Spawn(ThingDef.Named("MechaniteSpark"), __instance.Position, __instance.Map)).Launch(__instance, position, position, ProjectileHitFlags.All);
					}
				}
				else
				{
					MechaniteFire existingFire = position.GetFirstThing<MechaniteFire>(__instance.Map);
					if (existingFire != null)
					{
						existingFire.fireSize += 0.1f;
					}
					else
					{
						MechaniteFire obj = (MechaniteFire)ThingMaker.MakeThing(ResourceBank.ThingDefOf.MechaniteFire);
						obj.fireSize = Rand.Range(0.1f, 0.2f);
						GenSpawn.Spawn(obj, position, __instance.Map, Rot4.North);
					}
				}
			}
		}
	}

	[HarmonyPatch(typeof(Fire), "DoComplexCalcs")]
	public static class ComplexFlammability
	{
		public static bool Prefix(Fire __instance)
		{
			if (__instance is MechaniteFire)
				return false;
			return true;
		}
		public static void Postfix(Fire __instance)
		{
			if (__instance is MechaniteFire)
			{
				bool flag = false;
				List<Thing> flammableList = new List<Thing>();
				if (__instance.parent == null)
				{
					List<Thing> list = __instance.Map.thingGrid.ThingsListAt(__instance.Position);
					for (int i = 0; i < list.Count; i++)
					{
						Thing thing = list[i];
						if (thing is Building_Door)
						{
							flag = true;
						}
						if (!(thing is MechaniteFire) && thing.def.useHitPoints)
						{
							flammableList.Add(list[i]);
							if (__instance.parent == null && __instance.fireSize > 0.4f && list[i].def.category == ThingCategory.Pawn && Rand.Chance(FireUtility.ChanceToAttachFireCumulative(list[i], 150f)))
							{
								list[i].TryAttachFire(__instance.fireSize * 0.2f);
							}
						}
					}
				}
				else
				{
					flammableList.Add(__instance.parent);
				}
				if (flammableList.Count == 0 && __instance.Position.GetTerrain(__instance.Map).extinguishesFire)
				{
					__instance.Destroy();
					return;
				}
				Thing thing2 = (__instance.parent != null) ? __instance.parent : ((flammableList.Count <= 0) ? null : flammableList.RandomElement());
				if (thing2 != null && (!(__instance.fireSize < 0.4f) || thing2 == __instance.parent || thing2.def.category != ThingCategory.Pawn))
				{
					IntVec3 pos = __instance.Position;
					Map map = __instance.Map;
					((MechaniteFire)__instance).DoFireDamage(thing2);
					if (thing2.Destroyed)
						GenExplosion.DoExplosion(pos, map, 1.9f, DefDatabase<DamageDef>.GetNamed("BombMechanite"), null);
				}
				if (__instance.Spawned)
				{
					float num = __instance.fireSize * 16f;
					if (flag)
					{
						num *= 0.15f;
					}
					GenTemperature.PushHeat(__instance.Position, __instance.Map, num);
					if (Rand.Value < 0.4f)
					{
						float radius = __instance.fireSize * 3f;
						SnowUtility.AddSnowRadial(__instance.Position, __instance.Map, radius, 0f - __instance.fireSize * 0.1f);
					}
					__instance.fireSize += 0.1f;
					if (__instance.fireSize > 1.75f)
					{
						__instance.fireSize = 1.75f;
					}
					if (__instance.Map.weatherManager.RainRate > 0.01f && Rand.Value < 6f)
					{
						__instance.TakeDamage(new DamageInfo(DamageDefOf.Extinguish, 10f));
					}
				}
			}
		}
	}

	[HarmonyPatch(typeof(ThingOwner), "NotifyAdded")]
	public static class FixFireBugA
	{
		public static void Postfix(Thing item)
		{
			if (item.HasAttachment(ResourceBank.ThingDefOf.MechaniteFire))
			{
				item.GetAttachment(ResourceBank.ThingDefOf.MechaniteFire).Destroy();
			}
		}
	}

	[HarmonyPatch(typeof(Pawn_JobTracker), "IsCurrentJobPlayerInterruptible")]
	public static class FixFireBugB
	{
		public static void Postfix(Pawn_JobTracker __instance, ref bool __result, Pawn ___pawn)
		{
			if (___pawn.HasAttachment(ResourceBank.ThingDefOf.MechaniteFire))
			{
				__result = false;
			}
		}
	}

	//[HarmonyPatch(typeof(JobGiver_FightFiresNearPoint),"TryGiveJob")]
	public class FixFireBugC //Manually patched since *someone* made this an internal class!
	{
		public void Postfix(ref Job __result, Pawn pawn)
		{
			Thing thing = GenClosest.ClosestThingReachable(pawn.GetLord().CurLordToil.FlagLoc, pawn.Map, ThingRequest.ForDef(ResourceBank.ThingDefOf.MechaniteFire), PathEndMode.Touch, TraverseParms.For(pawn), 25);
			if (thing != null)
			{
				__result = JobMaker.MakeJob(JobDefOf.BeatFire, thing);
			}
		}
	}

	[HarmonyPatch(typeof(JobGiver_ExtinguishSelf), "TryGiveJob")]
	public static class FixFireBugD
	{
		public static void Postfix(Pawn pawn, ref Job __result)
		{
			if (Rand.Value < 0.1f)
			{
				Fire fire = (Fire)pawn.GetAttachment(ResourceBank.ThingDefOf.MechaniteFire);
				if (fire != null)
				{
					__result = JobMaker.MakeJob(JobDefOf.ExtinguishSelf, fire);
				}
			}
		}
	}

	[HarmonyPatch(typeof(ThinkNode_ConditionalBurning), "Satisfied")]
	public static class FixFireBugE
	{
		public static void Postfix(Pawn pawn, ref bool __result)
		{
			__result = __result || pawn.HasAttachment(ResourceBank.ThingDefOf.MechaniteFire);
		}
	}

	[HarmonyPatch(typeof(Fire), "SpawnSmokeParticles")]
	public static class FixFireBugF
	{
		public static bool Prefix(Fire __instance)
		{
			return !(__instance is MechaniteFire);
		}
	}

	//ideology
	[HarmonyPatch(typeof(IdeoManager), "CanRemoveIdeo")]
	public static class IdeosDoNotDisappear
	{
		public static void Postfix(Ideo ideo, ref bool __result)
		{
			foreach (Faction faction in Find.FactionManager.allFactions)
			{
				if (faction.ideos != null && faction.ideos.AllIdeos.Contains(ideo))
				{
					__result = false;
					return;
				}
			}
		}
	}

	[HarmonyPatch(typeof(Scenario), "PostIdeoChosen")]
	public static class NotNowIdeology
	{
		public static bool ArchoFlag = false;

		public static bool Prefix()
		{
			if (ArchoFlag)
			{
				ArchoFlag = false;
				return false;
			}
			return true;
		}
	}

	//ship loading, start
	[HarmonyPatch(typeof(Scenario), "GetFullInformationText")]
	public static class RemoveUnwantedScenPartText
	{
		public static bool Prefix(Scenario __instance)
		{
			return __instance.AllParts.Where(part => part is ScenPart_LoadShip && ((ScenPart_LoadShip)part).HasValidFilename()).Count() == 0;
		}

		public static void Postfix(Scenario __instance, ref string __result)
		{
			if (__instance.AllParts.Where(part => part is ScenPart_LoadShip && ((ScenPart_LoadShip)part).HasValidFilename()).Any())
			{
				try
				{
					StringBuilder stringBuilder = new StringBuilder();
					foreach (ScenPart allPart in __instance.AllParts)
					{
						allPart.summarized = false;
					}
					foreach (ScenPart item in from p in __instance.AllParts
											  orderby p.def.summaryPriority descending, p.def.defName
											  where p.visible
											  select p)
					{
						if (ShipInteriorMod2.CompatibleWithShipLoad(item))
						{
							string text = item.Summary(__instance);
							if (!text.NullOrEmpty())
							{
								stringBuilder.AppendLine(text);
							}
						}
					}
					__result = stringBuilder.ToString().TrimEndNewlines();
					return;
				}
				catch (Exception ex)
				{
					Log.ErrorOnce("Exception in Scenario.GetFullInformationText():\n" + ex.ToString(), 10395878);
					__result = "Cannot read data.";
				}
			}
		}
	}

	[HarmonyPatch(typeof(Scenario), "GetFirstConfigPage")]
	public static class LoadTheUniqueIDs
	{
		public static void Postfix(Scenario __instance)
		{
			foreach (ScenPart part in __instance.AllParts)
			{
				if (part is ScenPart_LoadShip p && p.HasValidFilename())
				{
					p.DoEarlyInit();
				}
				else if (part is ScenPart_StartInSpace s)
				{
					s.DoEarlyInit();
				}
			}
		}
    }
    
    [HarmonyPatch(typeof(Scenario), "Category", MethodType.Getter)]
	public static class FixThatBugInParticular
	{
		public static bool Prefix(Scenario __instance, ref ScenarioCategory ___categoryInt)
		{
			if (___categoryInt == ScenarioCategory.Undefined)
				___categoryInt = ScenarioCategory.CustomLocal;
			return true;
		}
	}

    [HarmonyPatch(typeof(Page_ChooseIdeoPreset), "PostOpen")]
    public static class DoNotRemoveMyIdeo
    {
        public static bool Prefix()
        {
            return !ShipInteriorMod2.LoadShipFlag;
        }

        public static void Postfix(Page_ChooseIdeoPreset __instance)
        {
            if (ShipInteriorMod2.LoadShipFlag)
            {
                foreach (Faction allFaction in Find.FactionManager.AllFactions)
                {
                    if (allFaction != Faction.OfPlayer && allFaction.ideos != null && allFaction.ideos.PrimaryIdeo.memes.NullOrEmpty())
                    {
                        allFaction.ideos.ChooseOrGenerateIdeo(new IdeoGenerationParms(allFaction.def));
                    }
                }
                ScenPart_LoadShip scen = (ScenPart_LoadShip)Current.Game.Scenario.parts.FirstOrDefault(s => s is ScenPart_LoadShip);
                Faction.OfPlayer.ideos.SetPrimary(scen.playerFactionIdeo);
                IdeoUIUtility.selected = scen.playerFactionIdeo;
                ScenPart_LoadShip.AddIdeo(Faction.OfPlayer.ideos.PrimaryIdeo);
                Page_ConfigureIdeo page_ConfigureIdeo = new Page_ConfigureIdeo();
                page_ConfigureIdeo.prev = __instance.prev;
                page_ConfigureIdeo.next = __instance.next;
                if (__instance.next != null)
                    __instance.next.prev = page_ConfigureIdeo;
                Find.WindowStack.Add(page_ConfigureIdeo);
                __instance.Close();
            }
        }
    }

    [HarmonyPatch(typeof(Page_ConfigureStartingPawns), "PreOpen")]
    public static class NoNeedForMorePawns
    {
        public static bool Prefix()
        {
            return !ShipInteriorMod2.LoadShipFlag;
        }

        public static void Postfix(Page_ConfigureStartingPawns __instance)
        {
            if (ShipInteriorMod2.LoadShipFlag)
            {
                if (__instance.next != null)
                {
                    __instance.prev.next = __instance.next;
                    __instance.next.prev = __instance.prev;
                    Find.WindowStack.Add(__instance.next);
                }
                if (__instance.nextAct != null)
                {
                    __instance.nextAct();
                }
                __instance.Close();
            }
        }
    }

    [HarmonyPatch(typeof(GameInitData), "PrepForMapGen")]
    public static class FixPawnGen
    {
        public static bool Prefix()
        {
            return !ShipInteriorMod2.LoadShipFlag;
        }
    }

    [HarmonyPatch(typeof(MapGenerator), "GenerateMap")]
    public static class GenerateSpaceMapInstead
    {
        public static bool Prefix()
        {
            if (ShipInteriorMod2.LoadShipFlag || ShipInteriorMod2.StartShipFlag)
                return false;
            return true;
        }

        public static void Postfix(MapParent parent, ref Map __result)
        {
            if (ShipInteriorMod2.LoadShipFlag)
            {
                parent.Destroy();
                ShipInteriorMod2.LoadShipFlag = false;
                __result = ScenPart_LoadShip.GenerateShipSpaceMap();
            }
            else if (ShipInteriorMod2.StartShipFlag)
            {
                parent.Destroy();
                ShipInteriorMod2.StartShipFlag = false;
                __result = ScenPart_StartInSpace.GenerateShipSpaceMap();
            }
        }
    }

    //quests, events
    [HarmonyPatch(typeof(NaturalRandomQuestChooser), "ChooseNaturalRandomQuest")]
	public static class QuestsInSpace //if player has space home map and no ground home map pick from whitelisted questdefs only
	{
		public static bool Prefix(out bool __state)
		{
			__state = false;
			if (Find.Maps.Any(m => m.IsPlayerHome && m.IsSpace()) && !Find.Maps.Any(m => m.IsPlayerHome && !m.IsSpace()))
			{
				//Log.Warning("SOS2 quest override: only space home map found, switching to SOS2 whitelisted quests.");
				__state = true;
			}
			if (__state)
				return false;
			return true;
		}
		public static void Postfix(ref QuestScriptDef __result, float points, IIncidentTarget target, bool __state)
		{
			if (!__state)
				return;
			if (TryGetSpaceQuest(false, out var chosen3))
			{
				//Log.Warning("SOS2 quest override: new quest is: " + chosen3.defName);
				__result = chosen3;
			}
			else
			{
				//Log.Warning("SOS2 quest override: Couldn't find any random quest for space.");
				__result = null;
			}
			return;
			bool TryGetSpaceQuest(bool incPop, out QuestScriptDef chosen)
			{
				return DefDatabase<QuestScriptDef>.AllDefs.Where((QuestScriptDef x) => x.IsRootRandomSelected && x.rootIncreasesPopulation == incPop && ShipInteriorMod2.allowedQuests.Contains(x.defName) && x.CanRun(points)).TryRandomElementByWeight((QuestScriptDef x) => NaturalRandomQuestChooser.GetNaturalRandomSelectionWeight(x, points, target.StoryState), out chosen);
			}
		}
	}

	[HarmonyPatch(typeof(QuestGen_Get), "GetMap")] //called for some quests via TestRunInt in CanRun above
	public static class PreferGroundMapsForQuests //if more than one home map exists prefer that instead of space home
	{
		public static void Postfix(ref Map __result, int? preferMapWithMinFreeColonists)
		{
			if (__result != null && Find.Maps.Count > 1 && __result.IsSpace())
			{
				//int minCount = preferMapWithMinFreeColonists ?? 1;
				Map map = Find.Maps.Where(m => m.IsPlayerHome && !m.IsSpace())?.FirstOrDefault() ?? null; // && m.mapPawns.FreeColonists.Count >= minCount
				if (map != null)
                {
					//Log.Warning("SOS2 quest override: changed target map from: " + __result + " to: " + map);
					__result = map;
				}
			}
		}
	}

	[HarmonyPatch(typeof(QuestNode_GetMap), "IsAcceptableMap")]
	public static class IsAcceptableMapNotInspace //if a quest is using this it wont run on a space map
	{
		public static void Postfix(Map map, Slate slate, ref bool __result)
		{
			if (map.IsSpace())
			{
				//if player has space home map and no ground home map whitelist was already checked
				if (Find.Maps.Any(m => m.IsPlayerHome && m.IsSpace()) && !Find.Maps.Any(m => m.IsPlayerHome && !m.IsSpace()))
				{
					return;
				}
				//Log.Warning("SOS2 quest override: random quest called QuestNode_GetMap on space map, returning false");
				__result = false;
			}
		}
    }

    [HarmonyPatch(typeof(QuestNode_Root_PollutionRaid), "TestRunInt")]
    public static class NoPollutionRaidsInspace
    {
        public static void Postfix(Slate slate, ref bool __result)
        {
            if (__result)
            {
                Map map = slate.Get<Map>("map", null, false);
				if (map.IsSpace())
					__result = false;
            }
        }
    }

    [HarmonyPatch(typeof(RoyalTitlePermitWorker_CallAid), "CallAid")]
	public static class CallAidInSpace
	{
		public static bool Prefix(RoyalTitlePermitWorker_CallAid __instance, Pawn caller, Map map, IntVec3 spawnPos, Faction faction, bool free, float biocodeChance = 1f)
		{
			if (map != null && map.IsSpace())
			{
                IncidentParms incidentParms = new IncidentParms
                {
                    target = map,
                    faction = faction,
                    raidArrivalModeForQuickMilitaryAid = true,
                    biocodeApparelChance = biocodeChance,
                    biocodeWeaponsChance = biocodeChance,
                    spawnCenter = spawnPos
                };
                if (__instance.def.royalAid.pawnKindDef != null)
				{
					incidentParms.pawnKind = __instance.def.royalAid.pawnKindDef;
					//if (incidentParms.pawnKind == PawnKindDefOf.Empire_Fighter_Trooper)
					//return false;
					if (incidentParms.pawnKind == PawnKindDefOf.Empire_Fighter_Janissary)
						incidentParms.pawnKind = DefDatabase<PawnKindDef>.GetNamed("Empire_Fighter_Marine_Space");
					else if (incidentParms.pawnKind == PawnKindDefOf.Empire_Fighter_Cataphract)
						incidentParms.pawnKind = DefDatabase<PawnKindDef>.GetNamed("Empire_Fighter_Cataphract_Space");
					incidentParms.pawnCount = __instance.def.royalAid.pawnCount;
				}
				else
				{
					incidentParms.points = (float)__instance.def.royalAid.points;
				}
				faction.lastMilitaryAidRequestTick = Find.TickManager.TicksGame;
				if (IncidentDefOf.RaidFriendly.Worker.TryExecute(incidentParms))
				{
					if (!free)
					{
						caller.royalty.TryRemoveFavor(faction, __instance.def.royalAid.favorCost);
					}
					caller.royalty.GetPermit(__instance.def, faction).Notify_Used();
					return false;
				}
				Log.Error(string.Concat(new object[] { "Could not send aid to map ", map, " from faction ", faction }));
				return false;
			}
			else
				return true;
		}
	}

	[HarmonyPatch(typeof(RoyalTitlePermitWorker_CallLaborers), "CallLaborers")]
	public static class CallLaborersInSpace
	{
		public static bool Prefix(RoyalTitlePermitWorker_CallAid __instance, Pawn pawn, Map map, Faction faction, bool free)
		{
			if (map != null && map.IsSpace())
			{
				if (faction.HostileTo(Faction.OfPlayer))
				{
					return false;
				}
				QuestScriptDef permit_CallLaborers = QuestScriptDefOf.Permit_CallLaborers;
				Slate slate = new Slate();
				slate.Set<Map>("map", map, false);
				slate.Set<int>("laborersCount", __instance.def.royalAid.pawnCount, false);
				slate.Set<Faction>("permitFaction", faction, false);
				slate.Set<PawnKindDef>("laborersPawnKind", DefDatabase<PawnKindDef>.GetNamed("Empire_Space_Laborer"), false);
				slate.Set<float>("laborersDurationDays", __instance.def.royalAid.aidDurationDays, false);
				QuestUtility.GenerateQuestAndMakeAvailable(permit_CallLaborers, slate);
				pawn.royalty.GetPermit(__instance.def, faction).Notify_Used();
				if (!free)
				{
					pawn.royalty.TryRemoveFavor(faction, __instance.def.royalAid.favorCost);
				}
				return false;
			}
			else
				return true;
		}
	}

    [HarmonyPatch(typeof(IncidentWorker_Raid), "ResolveRaidArriveMode")] //on ground immediat attack if ship turrets, space pod in
    public static class RaidsInspace
    {
        public static void Postfix(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (map.IsSpace())
                parms.raidArrivalMode = PawnsArrivalModeDefOf.CenterDrop;
            else if (map.GetComponent<ShipHeatMapComp>().ShipsOnMapNew.Values.Any(s => s.Turrets.Any(t => t.heatComp.Props.groundDefense)))
                parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
        }
    }

    [HarmonyPatch(typeof(QuestPart_EndGame), "Notify_QuestSignalReceived")]
    public static class ReplaceEndGame
    {
        public static bool Prefix(Signal signal, QuestPart_EndGame __instance)
        {
            if (signal.tag == __instance.inSignal)
            {
                List<Pawn> list;
                if (!signal.args.TryGetArg<List<Pawn>>("SENTCOLONISTS", out list))
                {
                    list = null;
                }
                Map originMap = Find.CurrentMap;
                Map map;
                EnemyShipDef shipDef = DefDatabase<EnemyShipDef>.GetNamed("RewardEmpireDestroyer");
                List<Building> cores = new List<Building>();
                if (ShipInteriorMod2.FindPlayerShipMap() != null)
                {
                    map = GetOrGenerateMapUtility.GetOrGenerateMap(ShipInteriorMod2.FindWorldTilePlayer(), new IntVec3(250, 1, 250), ResourceBank.WorldObjectDefOf.ShipEnemy);
                    map.GetComponent<ShipHeatMapComp>().IsGraveyard = true;
                    ((WorldObjectOrbitingShip)map.Parent).radius = 150f;
                    ((WorldObjectOrbitingShip)map.Parent).theta = -3 - 0.1f + 0.002f * Rand.Range(0, 20);
                    ((WorldObjectOrbitingShip)map.Parent).phi = 0 - 0.01f + 0.001f * Rand.Range(-20, 20);
                }
                else
                {
                    map = ShipInteriorMod2.GeneratePlayerShipMap(originMap.Size);
                }
                ShipInteriorMod2.GenerateShip(shipDef, map, null, Faction.OfPlayer, null, out cores, false, false, 0, (map.Size.x - shipDef.sizeX) / 2, (map.Size.z - shipDef.sizeZ) / 2);
                map.fogGrid.ClearAllFog();
                
				if (list != null)
                {
                    IntVec3 bay = map.listerBuildings.allBuildingsColonist.Where(b => b.def == ResourceBank.ThingDefOf.ShipShuttleBay).Last().Position;
                    foreach (Pawn p in list) //drop off player pawns on ship
                    {
                        Thing t;
                        if (p.Faction == Faction.OfPlayer && !p.kindDef.defName.StartsWith("Empire_Fighter_StellicGuard"))
                            p.holdingOwner.TryDrop(p, bay, map, ThingPlaceMode.Near, out t);
                    }
					//make fake skyfaller leave //td? make the whole thing travel, land, unload and leave
                    /*thing.Position = bay;
                    thing.SpawnSetup(map, false);
                    Thing thing = ThingMaker.MakeThing(ThingDefOf.Shuttle);
					//CompTransporter tr = thing.TryGetComp<CompTransporter>();
                    FlyShipLeaving flyShipLeaving = (FlyShipLeaving)SkyfallerMaker.MakeSkyfaller(ThingDefOf.ShuttleLeaving);
                    //flyShipLeaving.groupID = tr.groupID;
                    flyShipLeaving.createWorldObject = false;
					flyShipLeaving.Contents = null;
					//flyShipLeaving.ticksToDiscard = 1000;
                    GenSpawn.Spawn(flyShipLeaving, bay, map, WipeMode.Vanish);
                    thing.Destroy(DestroyMode.Vanish);*/
                }
				//original bellow
                /*if (!Find.TickManager.Paused)
				{
					Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
				}
				List<Pawn> list;
				if (!signal.args.TryGetArg<List<Pawn>>("SENTCOLONISTS", out list))
				{
					list = null;
				}
				StringBuilder stringBuilder = new StringBuilder();
				if (list != null)
				{
					for (int i = 0; i < list.Count; i++)
					{
						stringBuilder.AppendLine("   " + list[i].LabelCap);
					}
					Find.StoryWatcher.statsRecord.colonistsLaunched += list.Count;
				}
				//ShipCountdown.InitiateCountdown(GameVictoryUtility.MakeEndCredits(this.introText, this.endingText, stringBuilder.ToString(), "GameOverColonistsEscaped", null));
				if (list != null)
				{
					for (int j = 0; j < list.Count; j++)
					{
						if (!list[j].Destroyed)
						{
							list[j].Destroy(DestroyMode.Vanish);
						}
					}
				}*/
            }
            return false;
        }
    }

    //progression
    [HarmonyPatch(typeof(MapParent), "RecalculateHibernatableIncidentTargets")]
	public static class GiveMeRaidsPlease
	{
		public static void Postfix(MapParent __instance, ref HashSet<IncidentTargetTagDef> ___hibernatableIncidentTargets)
		{
			foreach (ThingWithComps current in __instance.Map.listerThings
				.ThingsOfDef(ThingDef.Named("JTDriveSalvage")).OfType<ThingWithComps>())
			{
				CompHibernatableSoS compHibernatable = current.TryGetComp<CompHibernatableSoS>();
				if (compHibernatable != null && compHibernatable.State == HibernatableStateDefOf.Starting &&
					compHibernatable.Props.incidentTargetWhileStarting != null)
				{
					if (___hibernatableIncidentTargets == null)
					{
						___hibernatableIncidentTargets = new HashSet<IncidentTargetTagDef>();
					}
					___hibernatableIncidentTargets.Add(compHibernatable.Props.incidentTargetWhileStarting);
				}
			}
		}
	}

	[HarmonyPatch(typeof(Designator_Build)), HarmonyPatch("Visible", MethodType.Getter)]
	public static class UnlockBuildings
	{
		public static void Postfix(ref bool __result, Designator_Build __instance)
		{
			if (__instance.PlacingDef is ThingDef && ((ThingDef)__instance.PlacingDef).HasComp(typeof(CompSoSUnlock)))
			{
				if (ShipInteriorMod2.WorldComp.Unlocks.Contains(((ThingDef)__instance.PlacingDef).GetCompProperties<CompProperties_SoSUnlock>().unlock) || DebugSettings.godMode)
					__result = true;
				else
					__result = false;
			}
		}
	}

	[HarmonyPatch(typeof(Page_SelectStartingSite), "CanDoNext")]
	public static class LetMeLandOnMyOwnBase
	{
		public static bool Prefix()
		{
			return false;
		}
		public static void Postfix(ref bool __result)
		{
			int selectedTile = Find.WorldInterface.SelectedTile;
			if (selectedTile < 0)
			{
				Messages.Message(TranslatorFormattedStringExtensions.Translate("MustSelectLandingSite"), MessageTypeDefOf.RejectInput);
				__result = false;
			}
			else
			{
				StringBuilder stringBuilder = new StringBuilder();
				if (!TileFinder.IsValidTileForNewSettlement(selectedTile, stringBuilder) &&
					(Find.World.worldObjects.SettlementAt(selectedTile) == null ||
					 Find.World.worldObjects.SettlementAt(selectedTile).Faction != Faction.OfPlayer))
				{
					Messages.Message(stringBuilder.ToString(), MessageTypeDefOf.RejectInput);
					__result = false;
				}
				else
				{
					Tile tile = Find.WorldGrid[selectedTile];
					__result = true;
				}
			}
		}
	}

	[HarmonyPatch(typeof(IncidentWorker_PsychicEmanation), "TryExecuteWorker")]
	public static class TogglePsychicAmplifierQuest
	{
		public static void Postfix(IncidentParms parms)
		{
			if (!ShipInteriorMod2.WorldComp.Unlocks.Contains("ArchotechSpore"))
			{
				foreach (Map map in Find.Maps)
				{
					if (map.IsSpace() && map.spawnedThings.Where(t => t.def == ThingDefOf.Ship_ComputerCore && t.Faction == Faction.OfPlayer).Any())
                    {
                        Find.LetterStack.ReceiveLetter(TranslatorFormattedStringExtensions.Translate("SoSPsychicAmplifier"), TranslatorFormattedStringExtensions.Translate("SoSPsychicAmplifierDesc"), LetterDefOf.PositiveEvent);
                        AttackableShip ship = new AttackableShip();
                        ship.attackableShip = DefDatabase<EnemyShipDef>.GetNamed("MechPsychicAmp");
                        ship.spaceNavyDef = DefDatabase<SpaceNavyDef>.GetNamed("Mechanoid_SpaceNavy");
                        ship.shipFaction = Faction.OfMechanoids;
                        map.passingShipManager.AddShip(ship);
						break;
                    }
				}
			}
		}
	}

	[HarmonyPatch(typeof(ResearchManager), "FinishProject")]
	public static class TriggerPillarMissions
	{
		public static void Postfix(ResearchProjectDef proj)
		{
			if (proj == ResourceBank.ResearchProjectDefOf.ArchotechPillarA)
				ShipInteriorMod2.WorldComp.Unlocks.Add("ArchotechPillarAMission"); //Handled in Building_ShipBridge
			else if (proj == ResourceBank.ResearchProjectDefOf.ArchotechPillarB)
				ShipInteriorMod2.WorldComp.Unlocks.Add("ArchotechPillarBMission"); //Handled in Building_ShipBridge
			else if (proj == ResourceBank.ResearchProjectDefOf.ArchotechPillarC)
			{
				ShipInteriorMod2.WorldComp.Unlocks.Add("ArchotechPillarCMission");
				ShipInteriorMod2.GenerateSite("TribalPillarSite");
			}
			else if (proj == ResourceBank.ResearchProjectDefOf.ArchotechPillarD)
			{
				ShipInteriorMod2.WorldComp.Unlocks.Add("ArchotechPillarDMission");
				ShipInteriorMod2.GenerateSite("InsectPillarSite");
			}
		}
	}

	[HarmonyPatch(typeof(Window), "PostClose")]
	public static class CreditsAreTheRealEnd
	{
		public static void Postfix(Window __instance)
		{
			if (__instance is Screen_Credits && ShipInteriorMod2.WorldComp.SoSWin)
			{
				ShipInteriorMod2.WorldComp.SoSWin = false;
				GenScene.GoToMainMenu();
			}
		}
	}

	//storyteller
	[HarmonyPatch(typeof(Map), "get_PlayerWealthForStoryteller")]
	public static class TechIsWealth
	{
		static SimpleCurve wealthCurve = new SimpleCurve(new CurvePoint[] { new CurvePoint(0, 0), new CurvePoint(3800, 0), new CurvePoint(150000, 400000f), new CurvePoint(420000, 700000f), new CurvePoint(666666, 1000000f) });
		static SimpleCurve componentCurve = new SimpleCurve(new CurvePoint[] { new CurvePoint(0, 0), new CurvePoint(10, 5000), new CurvePoint(100, 25000), new CurvePoint(1000, 150000) });

		public static void Postfix(Map __instance, ref float __result)
		{
			if (Find.Storyteller.def != ResourceBank.StorytellerDefOf.Sara)
				return;
			float num = ResearchToWealth();
			int numComponents = 0;
			foreach (Building building in __instance.listerBuildings.allBuildingsColonist.Where(b => b.def.costList != null))
			{
				if (building.def.costList.Any(tdc => tdc.thingDef == ThingDefOf.ComponentIndustrial))
					numComponents++;
				if (building.def.costList.Any(tdc => tdc.thingDef == ThingDefOf.ComponentSpacer))
					numComponents += 10;
			}
			num += componentCurve.Evaluate(numComponents);
			//Log.Message("Sara Spacer calculates threat points should be " + wealthCurve.Evaluate(num) + " based on " + ResearchToWealth() + " research and " + numComponents + " component-based buildings");
			__result = wealthCurve.Evaluate(num);
		}

		static float ResearchToWealth()
		{
			float num = 0;
			foreach (ResearchProjectDef proj in DefDatabase<ResearchProjectDef>.AllDefs)
			{
				if (proj.IsFinished)
					num += proj.baseCost;
			}
			if (num > 100000)
				num = 100000;
			return num;
		}
	}

	//should be in vanilla RW section
	[HarmonyPatch(typeof(CompTempControl), "CompGetGizmosExtra")]
	public static class CannotControlEnemyRadiators
	{
		public static void Postfix(CompTempControl __instance, ref IEnumerable<Gizmo> __result)
		{
			if (__instance.parent.Faction != Faction.OfPlayer)
				__result = new List<Gizmo>();
		}
	}

	[HarmonyPatch(typeof(CompLaunchable), "CompGetGizmosExtra")]
	public static class CannotControlEnemyPods
	{
		public static void Postfix(CompTempControl __instance, ref IEnumerable<Gizmo> __result)
		{
			if (__instance.parent.Faction != Faction.OfPlayer)
				__result = new List<Gizmo>();
		}
	}

	[HarmonyPatch(typeof(CompTransporter), "CompGetGizmosExtra")]
	public static class CannotControlEnemyPodsB
	{
		public static void Postfix(CompTempControl __instance, ref IEnumerable<Gizmo> __result)
		{
			if (__instance.parent.Faction != Faction.OfPlayer)
				__result = new List<Gizmo>();
		}
	}

	[HarmonyPatch(typeof(CompRefuelable), "CompGetGizmosExtra")]
	public static class CannotControlEnemyFuel
	{
		public static void Postfix(CompTempControl __instance, ref IEnumerable<Gizmo> __result)
		{
			if (__instance.parent.Faction != Faction.OfPlayer)
				__result = new List<Gizmo>();
		}
    }

    //other
    [HarmonyPatch(typeof(Thing), "SmeltProducts")]
	public static class PerfectEfficiency
	{
		public static bool Prefix(float efficiency)
		{
			if (efficiency == 0)
				return false;
			return true;
		}

		public static void Postfix(float efficiency, ref IEnumerable<Thing> __result, Thing __instance)
		{
			if (efficiency == 0)
			{
				List<Thing> actualResult = new List<Thing>();
				List<ThingDefCountClass> costListAdj = __instance.def.CostListAdjusted(__instance.Stuff);
				for (int j = 0; j < costListAdj.Count; j++)
				{
					int num = GenMath.RoundRandom((float)costListAdj[j].count);
					if (num > 0)
					{
						Thing thing = ThingMaker.MakeThing(costListAdj[j].thingDef);
						thing.stackCount = num;
						actualResult.Add(thing);
					}
				}
				__result = actualResult;
			}
		}
	}

    [HarmonyPatch(typeof(InfestationCellFinder), "DebugDraw")] //override infestation draw in space to show ships
    public static class DrawShipsInSpace
    {
        public static bool Prefix()
        {
            if (DebugViewSettings.drawInfestationChance)
            {
                Map currentMap = Find.CurrentMap;
				if (!currentMap.IsSpace())
					return true;
				var mapComp = currentMap.GetComponent<ShipHeatMapComp>();
                if (InfestationCellFinder.tmpCachedInfestationChanceCellColors == null)
                {
                    InfestationCellFinder.tmpCachedInfestationChanceCellColors = new List<Pair<IntVec3, float>>();
                }
                //if (Time.frameCount % 6 == 0)
                {
                    InfestationCellFinder.tmpCachedInfestationChanceCellColors.Clear();
					var cells = mapComp.MapShipCells;
                    foreach (IntVec3 v in cells.Keys)
                    {
                        InfestationCellFinder.tmpCachedInfestationChanceCellColors.Add(new Pair<IntVec3, float>(v, cells[v].Item1));
                    }
                    /*foreach (SoShipCache s in mapComp.ShipsOnMapNew.Values)
                    {
                        foreach (IntVec3 v in s.AreaDestroyed)
                        {
                            InfestationCellFinder.tmpCachedInfestationChanceCellColors.Add(new Pair<IntVec3, float>(v, 0));
                        }
                    }*/
                }
                for (int m = 0; m < InfestationCellFinder.tmpCachedInfestationChanceCellColors.Count; m++)
                {
                    IntVec3 v = InfestationCellFinder.tmpCachedInfestationChanceCellColors[m].First;
					int sec = (int)InfestationCellFinder.tmpCachedInfestationChanceCellColors[m].Second;

                    if (sec == -1)
                    {
                        CellRenderer.RenderCell(v, SolidColorMaterials.SimpleSolidColorMaterial(new Color(1, 0, 0, 0.99f), false));
                        continue;
                    }
                    else if (sec == 0)
                    {
                        CellRenderer.RenderCell(v, SolidColorMaterials.SimpleSolidColorMaterial(new Color(0, 1, 0, 0.99f), false));
						continue;
                    }

                    int index = (int)InfestationCellFinder.tmpCachedInfestationChanceCellColors[m].Second % 1000;
                    float r = index / 1000f;
					index %= 100;
                    float g = index / 100f;
                    index %= 10;
                    float b = index / 10f;
                    CellRenderer.RenderCell(v, SolidColorMaterials.SimpleSolidColorMaterial(new Color(r, g, b, 0.5f), false));
                }
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MapPawns), "DeRegisterPawn")]
	public class MapPawnRegisterPatch //PsiTech "patch"
	{
		public static bool Prefix(Pawn p)
		{
			//This patch does literally nothing... and yet, somehow, it fixes a compatibility issue with PsiTech. Weird, huh?
			return true;
		}
	}

	[HarmonyPatch(typeof(District), "get_Map")]
	public static class FixMapIssue //This is the most horrible hack that has ever been hacked, it *MUST* be removed before release
	{
		public static bool Prefix(District __instance)
		{
			var maps = Find.Maps;
			for (int i = maps.Count; i-- > 0;) if (i == __instance.mapIndex) return true;
			return false;
		}

		public static void Postfix(District __instance, ref Map __result)
		{
			var maps = Find.Maps;
			bool found = false;
			for (int i = maps.Count; i-- > 0;)
			{
				if (i == __instance.mapIndex)
				{
					found = true;
					break;
				}
			}
			if (!found) __result = Find.Maps.FirstOrDefault();
		}
	}

    /*[HarmonyPatch(typeof(CompShipPart),"PostSpawnSetup")]
	public static class RemoveVacuum{
		public static void Postfix (CompShipPart __instance)
		{
			if (__instance.parent.Map.terrainGrid.TerrainAt (__instance.parent.Position).defName.Equals ("EmptySpace"))
				__instance.parent.Map.terrainGrid.SetTerrain (__instance.parent.Position,TerrainDef.Named("FakeFloorInsideShip"));
		}
	}*/
    /*[HarmonyPatch(typeof(GenConstruct), "BlocksConstruction")]
	public static class HullTilesDontWipe
	{
		public static void Postfix(Thing constructible, Thing t, ref bool __result)
		{
			if (constructible.def.defName.Contains("ShipHullTile") ^ t.def.defName.Contains("ShipHullTile"))
				__result = false;
		}
	}

	[HarmonyPatch(typeof(TravelingTransportPods))]
	[HarmonyPatch("TraveledPctStepPerTick", MethodType.Getter)]
	public static class InstantShuttleArrival
	{
		public static void Postfix(int ___initialTile, TravelingTransportPods __instance, ref float __result)
		{
			if (Find.TickManager.TicksGame % 60 == 0)
			{
				var mapComp = Find.WorldObjects.MapParentAt(___initialTile).Map.GetComponent<ShipHeatMapComp>();
				if ((mapComp.InCombat && (__instance.destinationTile == mapComp.ShipCombatOriginMap.Tile ||
					__instance.destinationTile == mapComp.ShipCombatMasterMap.Tile)) || 
					__instance.arrivalAction is TransportPodsArrivalAction_MoonBase)
				{
					__result = 1f;
				}
			}

		}
	}*/

    //Space crib - disabled, good transpiler example
    /*[HarmonyPatch(typeof(GenTemperature), "TryGetTemperatureForCell")]
	public static class BabiesAreSafeInSpaceCaskets
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
			var editor = new CodeMatcher(instructions);
			// --------------------------ORIGINAL--------------------------
			//for (int i = 0; i < list.Count; i++)
			//{
			//if (list[i].def.passability == Traversability.Impassable)
			editor.Start().MatchStartForward(
				new CodeMatch(OpCodes.Ldloc_0),
				new CodeMatch(OpCodes.Ldloc_1),
				new CodeMatch(OpCodes.Callvirt),
				//Jump point...
				new CodeMatch(OpCodes.Ldfld),
				new CodeMatch(OpCodes.Ldfld),
				new CodeMatch(OpCodes.Ldc_I4_2),
				new CodeMatch(OpCodes.Bne_Un_S)
			);
			
			var thing = generator.DeclareLocal(typeof(Thing)); //Store the list[i] into here
			var label = generator.DefineLabel(); //Prepare a new label
			var codeWithLabel = new CodeInstruction(OpCodes.Ldloc_S, thing); //This will be injected into the "Jump point" above.
			codeWithLabel.labels.Add(label); //Record its label position for the return to go to.

			if (!editor.IsInvalid)
			{
				// --------------------------MODIFIED--------------------------
				//for (int i = 0; i < list.Count; i++)
				//{
				//var item = list[i];
				//if (AdjustTemperatureForCrib(item, ref tempResult) return true;)
				//if (item.def.passability == Traversability.Impassable)
				return editor
				.Advance(3)
				.InsertAndAdvance(new CodeInstruction(OpCodes.Stloc_S, thing)) //Store the thing as a new variable
				.InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, thing)) //thing
				.InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_2)) //float tempResult
				.InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BabiesAreSafeInSpaceCaskets), nameof(AdjustTemperatureForCrib))))
				.InsertAndAdvance(new CodeInstruction(OpCodes.Brfalse_S, label)) //If it's false, move onto the next part of the loop like normal
				.InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4_1)) //Otherwise push a true and return
				.InsertAndAdvance(new CodeInstruction(OpCodes.Ret))
				.Insert(codeWithLabel)
				.InstructionEnumeration();
			}
			
			Log.Error("[SoS2] BabiesAreSafeInSpaceCaskets transpiler failed to find its target. Did RimWorld update?");
			return editor.InstructionEnumeration();	
        }

		public static bool AdjustTemperatureForCrib(Thing thing, ref float tempResult)
		{
			if (thing is Building_SpaceCrib)
			{
				tempResult = 21f;
				return true;
			}
			return false;
		}
	}*/

    // explosion patch disabled till fixed
    /*[HarmonyPatch(typeof(DamageWorker))]
	[HarmonyPatch("ExplosionCellsToHit", new Type[] { typeof(IntVec3), typeof(Map), typeof(float), typeof(IntVec3), typeof(IntVec3) })]
	public static class FasterExplosions
	{
		public static bool Prefix(Map map, float radius)
		{
			return !map.GetComponent<ShipHeatMapComp>().InCombat || radius > 25; //Ludicrously large explosions cause a stack overflow
		}

		public static void Postfix(ref IEnumerable<IntVec3> __result, DamageWorker __instance, IntVec3 center, Map map, float radius)
		{
			if (map.GetComponent<ShipHeatMapComp>().InCombat && radius <= 25)
			{
				HashSet<IntVec3> cells = new HashSet<IntVec3>();
				List<ExplosionCell> cellsToRun = new List<ExplosionCell>();
				cellsToRun.Add(new ExplosionCell(center, new bool[4], 0));
				ExplosionCell curCell;
				while (cellsToRun.Count > 0)
				{
					curCell = cellsToRun.Pop();
					cells.Add(curCell.pos);
					if (curCell.dist <= radius)
					{
						Building edifice = null;
						if (curCell.pos.InBounds(map))
							edifice = curCell.pos.GetEdifice(map);
						if (edifice != null && edifice.HitPoints >= __instance.def.defaultDamage / 2)
							continue;
						if (!curCell.checkedDir[0]) //up
						{
							bool[] newDir = (bool[])curCell.checkedDir.Clone();
							newDir[1] = true;
							cellsToRun.Add(new ExplosionCell(curCell.pos + new IntVec3(0, 0, 1), newDir, curCell.dist + 1));
						}
						if (!curCell.checkedDir[1]) //down
						{
							bool[] newDir = (bool[])curCell.checkedDir.Clone();
							newDir[0] = true;
							cellsToRun.Add(new ExplosionCell(curCell.pos + new IntVec3(0, 0, -1), newDir, curCell.dist + 1));
						}
						if (!curCell.checkedDir[2]) //right
						{
							bool[] newDir = (bool[])curCell.checkedDir.Clone();
							newDir[3] = true;
							cellsToRun.Add(new ExplosionCell(curCell.pos + new IntVec3(1, 0, 0), newDir, curCell.dist + 1));
						}
						if (!curCell.checkedDir[3]) //left
						{
							bool[] newDir = (bool[])curCell.checkedDir.Clone();
							newDir[2] = true;
							cellsToRun.Add(new ExplosionCell(curCell.pos + new IntVec3(-1, 0, 0), newDir, curCell.dist + 1));
						}
					}
				}
				__result = cells;
			}
		}

		public struct ExplosionCell
		{
			public IntVec3 pos;
			public bool[] checkedDir;
			public int dist;

			public ExplosionCell(IntVec3 myPos, bool[] myCheckedDir, int myDist)
			{
				checkedDir = myCheckedDir;
				pos = myPos;
				dist = myDist;
			}
		}
	}
	*/
    /*[HarmonyPatch(typeof(Building), "Destroy")] //obs by newcache
    public static class NotifyCombatManager
    {
        public static bool Prefix(Building __instance, DestroyMode mode, out Tuple<IntVec3, Faction, Map> __state)
        {
            __state = null;
            //only print or foam if destroyed normally
            if (!(mode == DestroyMode.KillFinalize || mode == DestroyMode.KillFinalizeLeavingsOnly))
                return true;
            if (!__instance.def.CanHaveFaction || __instance is Frame)
                return true;
            var mapComp = __instance.Map.GetComponent<ShipHeatMapComp>();
            int shipIndex = mapComp.ShipIndexOnVec(__instance.Position);
            if (shipIndex != -1) //is this on a ship
            {
                var shipPart = __instance.TryGetComp<CompSoShipPart>();
                var ship = mapComp.ShipsOnMapNew[shipIndex];
                if (ship.FoamDistributors.Any() && (shipPart.Props.isHull || shipPart.Props.isPlating))
                {
                    foreach (CompHullFoamDistributor dist in ship.FoamDistributors)
                    {
                        if (dist.parent.TryGetComp<CompRefuelable>().Fuel > 0 && dist.parent.TryGetComp<CompPowerTrader>().PowerOn)
                        {
                            dist.parent.TryGetComp<CompRefuelable>().ConsumeFuel(1);
                            __state = new Tuple<IntVec3, Faction, Map>(__instance.Position, __instance.Faction, __instance.Map);
                            return true;
                        }
                    }
                }
                //move to post, add ship area
                //if (__instance.Faction == Faction.OfPlayer && __instance.def.blueprintDef != null && __instance.def.researchPrerequisites.All(r => r.IsFinished)) //place blueprints
                //GenConstruct.PlaceBlueprintForBuild(__instance.def, __instance.Position, __instance.Map, __instance.Rotation, Faction.OfPlayer, __instance.Stuff);
            }
            return true;
        }
        public static void Postfix(Tuple<IntVec3, Faction, Map> __state)
        {
            if (__state != null)
            {
                Thing newWall = ThingMaker.MakeThing(ThingDef.Named("HullFoamWall"));
                newWall.SetFaction(__state.Item2);
                GenPlace.TryPlaceThing(newWall, __state.Item1, __state.Item3, ThingPlaceMode.Direct);
            }
        }
    }*/
    /*vacuum pathfinding - disabled, not working
    [HarmonyPatch(typeof(PathFinder), "FindPath", typeof(IntVec3), typeof(LocalTargetInfo), typeof(TraverseParms),
        typeof(PathEndMode), typeof(PathFinderCostTuning))]
    public static class H_Vacuum_PathFinder
    {
        private const int SpaceTileCostUnsuited = 10000;
        private const int SpaceTileCostSuited = 100;

        // The purpose of this transpiler is to add the pathfinding costs for space into the pathfinding code
        // We're looking for a line at the end of the calculation of the cost of a tile that looks like:
        //     int num15 = num14 + PathFinder.calcGrid[index3].knownCost;
        // We want to patch our pathfinding cost right above that line
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var patched = false;
            var gotIndex = false;
            var gotCost = false;

            var indexOperand = new object();
            var costOperand = new object();

            CodeInstruction lastCode = null;

            var blueprintField = AccessTools.Field(typeof(PathFinder), "blueprintGrid");
            var signalField = AccessTools.Field(typeof(PathFinder), "calcGrid");

            foreach (var code in instructions)
            {
                // Need to get some operands - specifically, the operands for index5 (cell location) and
                // num14 (cell cost)

                // Retrieve num14 (cell cost) operand from a const addition above our injection point
                if (!gotCost && lastCode?.opcode == OpCodes.Ldloc_S && code.LoadsConstant(600))
                {
                    costOperand = lastCode.operand;
                    gotCost = true;
                }

                // Retrieve index5 (cell location) operand from blueprint grid just above injection point
                if (!gotIndex && code.opcode == OpCodes.Ldloc_S && lastCode.LoadsField(blueprintField))
                {
                    indexOperand = code.operand;
                    gotIndex = true;
                }

                // Our injection point is the first access to PathFinder.calcGrid directly after num14 is loaded
                // Note that the total cell cost (num14) is already loaded onto the stack by now, which is fine because
                // we need to add to it anyway
                if (!patched && lastCode?.opcode == OpCodes.Ldloc_S && (lastCode?.OperandIs(costOperand) ?? false) &&
                    code.LoadsField(signalField))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load this
                    var mapField = AccessTools.Field(typeof(PathFinder), "map");
                    yield return new CodeInstruction(OpCodes.Ldfld, mapField); // Load map
                    yield return new CodeInstruction(OpCodes.Ldarg_3); // Load TraverseParms
                    yield return new CodeInstruction(OpCodes.Ldloc_S, indexOperand); // Load tile index
                    var costMethod = AccessTools.Method(typeof(H_Vacuum_PathFinder), nameof(AdditionalPathCost));
                    yield return new CodeInstruction(OpCodes.Call, costMethod); // Call method to get tile cost
                    yield return new CodeInstruction(OpCodes.Add); // Add num14 and our cost
                    yield return new CodeInstruction(OpCodes.Stloc_S, costOperand); // Store updated tile cost
                    yield return new CodeInstruction(OpCodes.Ldloc_S, costOperand); // Load cost to replace one we took

                    patched = true;
                }

                lastCode = code;
                yield return code;
            }
        }

        // Generate additional pathfinding costs for tiles that are in space
        public static int AdditionalPathCost(Map map, TraverseParms parms, int index)
        {
            // Only run in space, and if pawn doesn't have a space suit
            if (!map.IsSpace() || (!SaveOurShip2.ModSettings_SoS.useVacuumPathfinding && parms.pawn.Faction.IsPlayer)) return 0;

            // Find tile room
            var room = map.cellIndices.IndexToCell(index).GetRoom(map);

            // If room isn't space, zero extra cost
            if (!room?.IsSpace() ?? true) return 0;

            // If room is space, cost depending on whether pawn is suited or not
            return ShipInteriorMod2.EVAlevel(parms.pawn) > 6 ? SpaceTileCostSuited : SpaceTileCostUnsuited;
        }
    }
    [HarmonyPatch(typeof(Region), "DangerFor")]
    public static class H_Vacuum_Region_Danger
    {

        // The purpose of this transpiler is to increase the danger of vacuum regions
        // We're looking for a line right before the danger is cached and returned that looks like:
        //     if (Current.ProgramState == ProgramState.Playing)
        // We want to patch our additional danger into that if statement
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var patched = false;

            CodeInstruction lastLastCode = null;
            CodeInstruction lastCode = null;

            var signalMethod = AccessTools.Method(typeof(Current), "get_ProgramState");

            foreach (var code in instructions)
            {
                // Our injection point is after the call to program state right after danger (local variable 1) is
                // stored (essentially, in the middle of an if statement, but need to dodge labels)
                if (!patched && (lastLastCode?.opcode == OpCodes.Stloc_1) && (lastCode?.Calls(signalMethod) ?? false))
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_1); // Load danger
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load this
                    var roomProperty = AccessTools.Method(typeof(Region), "get_Room");
                    yield return new CodeInstruction(OpCodes.Call, roomProperty); // Load room
                    yield return new CodeInstruction(OpCodes.Ldarg_1); // Load pawn
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load this
                    var mapProperty = AccessTools.Method(typeof(Region), "get_Map");
                    yield return new CodeInstruction(OpCodes.Call, mapProperty); // Load map
                    var addDangerMethod = AccessTools.Method(typeof(VacuumExtensions),
                        nameof(VacuumExtensions.ExtraDangerFor));
                    yield return new CodeInstruction(OpCodes.Call, addDangerMethod); // Call method to get danger
                    yield return new CodeInstruction(OpCodes.Stloc_1); // Store updated danger

                    patched = true;
                }

                lastLastCode = lastCode;
                lastCode = code;
                yield return code;
            }
        }
    }
    public static class VacuumExtensions
    {
        public static Danger ExtraDangerFor(Danger original, Room room, Pawn p, Map map)
        {
            // Always pass through deadly, if tile or map isn't space, return normal danger
            if (original == Danger.Deadly || !map.IsSpace() || (!SaveOurShip2.ModSettings_SoS.useVacuumPathfinding && p.Faction.IsPlayer) || (!room?.IsSpace() ?? true))
                return original;

            return ShipInteriorMod2.EVAlevel(p) > 3 ? Danger.Some : Danger.Deadly;
        }

        public static bool IsSpace(this Room room)
        {
            return room.FirstRegion.type != RegionType.Portal && (room.OpenRoofCount > 0 || room.TouchesMapEdge);
        }
    }*/
}
