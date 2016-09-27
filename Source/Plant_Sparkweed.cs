﻿using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RemoteExplosives {
	/**
	 * A plant that has a chance to set itself of fire when walked over by a pawn.
	 */
	public class Plant_Sparkweed : Plant {
		private SparkweedPlantDef CustomDef {
			get {
				if (def is SparkweedPlantDef) return def as SparkweedPlantDef;
				throw new Exception("Plant_Sparkweed requires ThingDef of type SparkweedPlantDef");
			}
		}

		private List<Pawn> touchingPawns = new List<Pawn>(1);
		
		public override void SpawnSetup() {
			base.SpawnSetup();
			DistributedTickScheduler.Instance.RegisterTickability(CustomTick, CustomDef.detectEveryTicks);
		}

		public override void DeSpawn() {
			base.DeSpawn();
			DistributedTickScheduler.Instance.UnregisterTickability(CustomTick, CustomDef.detectEveryTicks);
		}

		public override void ExposeData() {
			base.ExposeData();
			Scribe_Collections.LookList(ref touchingPawns, "touchingPawns", LookMode.MapReference);
			if (Scribe.mode == LoadSaveMode.LoadingVars && touchingPawns == null) {
				touchingPawns = new List<Pawn>();
			}
		}

		private void CustomTick() {
			var thingsInCell = Find.ThingGrid.ThingsListAtFast(Position);
			// detect pawns
			for (int i = 0; i < thingsInCell.Count; i++) {
				if (!(thingsInCell[i] is Pawn)) continue;
				var pawn = thingsInCell[i] as Pawn;
				if (touchingPawns.Contains(pawn)) continue;
				touchingPawns.Add(pawn);
				OnNewPawnDetected(pawn);
			}
			// clear known pawns
			for (int i = touchingPawns.Count-1; i >= 0; i--) {
				if (thingsInCell.Contains(touchingPawns[i])) continue;
				touchingPawns.RemoveAt(i);
			}
		}

		private void OnNewPawnDetected(Pawn pawn) {
			if(Growth<CustomDef.minimumIgnitePlantGrowth) return;
			var doEffects = false;
			if (Rand.Range(0f, 1f) < CustomDef.ignitePlantChance) {
				FireUtility.TryStartFireIn(Position, Rand.Range(0.15f, 0.4f));
				doEffects = true;
			}
			if (Rand.Range(0f, 1f) < CustomDef.ignitePawnChance) {
				pawn.TryAttachFire(Rand.Range(0.15f, 0.25f));
				doEffects = true;
			}
			if (doEffects) {
				if (CustomDef.igniteEffecter != null) CustomDef.igniteEffecter.Spawn().Trigger(this, pawn);
			}
		}

	}
}