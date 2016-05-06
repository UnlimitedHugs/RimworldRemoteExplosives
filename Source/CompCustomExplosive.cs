﻿using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RemoteExplosives {
	/*
	 * An explosive with a timer. Can be triggered silently, but will revert to the vanilla wick if it takes enough damage.
	 */

	public class CompCustomExplosive : ThingComp {
		private static readonly SoundDef WickStartSound = SoundDef.Named("MetalHitImportant");
		private static readonly SoundDef WickLoopSound = SoundDef.Named("HissSmall");
		private static readonly int PawnNotifyCellCount = GenRadial.NumCellsInRadius(4.5f);
		private static MethodInfo pawnNotifyMethod;
		private bool wickStarted;
		private int wickTicksLeft;
		private Sustainer wickSoundSustainer;
		private bool detonated;
		private int wickTotalTicks;
		private bool wickIsSilent;

		private CompProperties_Explosive ExplosiveProps {
			get { return (CompProperties_Explosive)props; }
		}

		public int WickTotalTicks {
			get { return wickTotalTicks; }
		}

		public int WickTicksLeft {
			get { return wickTicksLeft; }
		}

		public override void Initialize(CompProperties p) {
			base.Initialize(p);
			// some reflection to get access to an internal method
			pawnNotifyMethod = typeof(Pawn_MindState).GetMethod("Notify_DangerousWickStarted", BindingFlags.NonPublic | BindingFlags.Instance);
		}

		protected int StartWickThreshold {
			get {
				return Mathf.RoundToInt(ExplosiveProps.startWickHitPointsPercent * parent.MaxHitPoints);
			}
		}

		public override void PostExposeData() {
			base.PostExposeData();
			Scribe_Values.LookValue(ref wickStarted, "wickStarted", false);
			Scribe_Values.LookValue(ref wickTicksLeft, "wickTicksLeft", 0);
			Scribe_Values.LookValue(ref wickTotalTicks, "wickTotalTicks", 0);
			Scribe_Values.LookValue(ref wickIsSilent, "wickIsSilent", false);
		}

		public override void CompTick() {
			if (!wickStarted) return;
			if (wickSoundSustainer == null) {
				if (!wickIsSilent) {
					StartWickSustainer();
				}
			} else {
				wickSoundSustainer.Maintain();
			}
			wickTicksLeft--;
			if (wickTicksLeft <= 0) {
				Detonate();
			}
		}

		private void StartWickSustainer() {
			WickStartSound.PlayOneShot(parent.Position);
			SoundInfo info = SoundInfo.InWorld(parent, MaintenanceType.PerTick);
			wickSoundSustainer = WickLoopSound.TrySpawnSustainer(info);
		}

		public override void PostDraw() {
			if (wickStarted && !wickIsSilent) {
				OverlayDrawer.DrawOverlay(parent, OverlayTypes.BurningWick);
			}
		}

		public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt) {
			if (parent.HitPoints <= 0) {
				if (dinfo.Def.externalViolence) {
					Detonate();
				}
			} else if (wickStarted && (dinfo.Def == DamageDefOf.Stun || (wickIsSilent && dinfo.Def == DamageDefOf.EMP))) { // silent wick can be stopped by EMP
				StopWick();
			} else if (!wickStarted && StartWickThreshold!=0 && parent.HitPoints <= StartWickThreshold && dinfo.Def.externalViolence) {
				StartWick(false);
			}
		}

		public void StartWick(bool silent) {
			if (wickStarted && !silent) wickIsSilent = false;
			if (wickStarted) return;
			wickIsSilent = silent;
			wickStarted = true;
			wickTotalTicks = wickTicksLeft = ExplosiveProps.wickTicks.RandomInRange;
			NotifyNearbyPawns();
		}

		public void StopWick() {
			wickStarted = false;
		}

		public bool WickStarted {
			get { return wickStarted; }
		}

		private void NotifyNearbyPawns() {
			Room room = parent.GetRoom();
			for (int i = 0; i < PawnNotifyCellCount; i++) {
				IntVec3 c = parent.Position + GenRadial.RadialPattern[i];
				if (!c.InBounds()) continue;
				List<Thing> thingList = c.GetThingList();
				for (int j = 0; j < thingList.Count; j++) {
					var pawn = thingList[j] as Pawn;
					if (pawn != null && 
						pawn.RaceProps.intelligence >= Intelligence.Humanlike && 
						pawn.Position.GetRoom() == room && 
						GenSight.LineOfSight(parent.Position, pawn.Position, true)) {
						pawnNotifyMethod.Invoke(pawn.mindState, new object[] {parent});
					}
				}
			}
		}

		protected virtual void Detonate() {
			if (detonated) return;
			detonated = true;
			if (!parent.Destroyed) {
				parent.Destroy(DestroyMode.Kill);
			}
			var exProps = ExplosiveProps;
			float num = exProps.explosiveRadius;
			if (parent.stackCount > 1 && exProps.explosiveExpandPerStackcount > 0f) {
				num += Mathf.Sqrt((parent.stackCount - 1) * exProps.explosiveExpandPerStackcount);
			}
			GenExplosion.DoExplosion(parent.Position, num, exProps.explosiveDamageType, parent);
		}

		
	}
}