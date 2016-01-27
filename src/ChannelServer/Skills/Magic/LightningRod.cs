﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.Network.Sending;
using Aura.Channel.Skills.Base;
using Aura.Channel.Skills.Combat;
using Aura.Channel.World.Entities;
using Aura.Channel.World;
using Aura.Data.Database;
using Aura.Mabi.Const;
using Aura.Mabi.Network;
using Aura.Shared.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aura.Mabi;

namespace Aura.Channel.Skills.Magic
{
	/// <summary>
	/// Lightning Rod Handler
	/// </summary>
	/// Var1: Min Damage
	/// Var2: Max Damage
	/// Var3: Max Chargning Time (ms)
	/// Var4: Max Charge Damage Bonus (%)
	/// Var5: ?
	[Skill(SkillId.LightningRod)]
	public class LightningRod : ISkillHandler, IPreparable, IUseable, ICompletable, ICancelable, IInitiableSkillHandler
	{
		/// <summary>
		/// Length of attack area; unconfirmed.
		/// </summary>
		/// <remarks>
		/// "http://wiki.mabinogiworld.com/view/Lightning_Rod#Summary"
		/// </remarks>
		private const int skillLength = 1400;

		/// <summary>
		/// Width of attack area; unconfirmed.
		/// </summary>
		/// <remarks>
		/// "http://wiki.mabinogiworld.com/view/Lightning_Rod#Summary"
		/// </remarks>
		private const int skillWidth = 200;

		/// <summary>
		/// Length of target's stun
		/// </summary>
		private const int targetStun = 2000;

		/// <summary>
		/// Distance target gets knocked back
		/// </summary>
		private const int knockbackDistance = 720;

		/// <summary>
		/// Subscribes handlers to events required for training.
		/// </summary>
		public void Init()
		{
			ChannelServer.Instance.Events.CreatureAttackedByPlayer += this.OnCreatureAttackedByPlayer;
			ChannelServer.Instance.Events.CreatureAttacks += this.OnCreatureAttacks;
		}

		/// <summary>
		/// Prepares the skill
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		/// <returns></returns>
		public bool Prepare(Creature creature, Skill skill, Packet packet)
		{
			if (creature.RightHand == null)
				Send.SkillPrepareSilentCancel(creature, skill.Info.Id);

			creature.StopMove();

			skill.State = SkillState.Prepared;

			Send.MotionCancel2(creature, 0);
			Send.Effect(creature, Effect.LightningRod, 2, 0); // Magic Circle?

			Send.SkillReady(creature, skill.Info.Id);
			skill.State = SkillState.Ready;

			/* Locks -----------------------
			Walk|Run
			---------------------------- - */
			creature.Lock(Locks.Walk | Locks.Run);

			creature.Temp.LightningRodPrepareTime = DateTime.Now;

			return true;
		}

		/// <summary>
		/// Uses LightningRod
		/// </summary>
		/// <param name="attacker"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		public void Use(Creature attacker, Skill skill, Packet packet)
		{
			// Set full charge variable
			if (DateTime.Now >= attacker.Temp.LightningRodPrepareTime.AddMilliseconds(skill.RankData.Var3))
				attacker.Temp.LightningRodFullCharge = true;

			/* Locks -----------------------
			Walk|Run
			---------------------------- - */
			attacker.Lock(Locks.Walk | Locks.Run);

			// Get direction for target Area
			var direction = Mabi.MabiMath.ByteToRadian(attacker.Direction);

			var attackerPos = attacker.GetPosition();

			// Calculate polygon points
			var r = MabiMath.ByteToRadian(attacker.Direction);
			var poe = attackerPos.GetRelative(r, 800);
			var pivot = new Point(poe.X, poe.Y);
			var p1 = new Point(pivot.X - skillLength / 2, pivot.Y - skillWidth / 2);
			var p2 = new Point(pivot.X - skillLength / 2, pivot.Y + skillWidth / 2);
			var p3 = new Point(pivot.X + skillLength / 2, pivot.Y + skillWidth / 2);
			var p4 = new Point(pivot.X + skillLength / 2, pivot.Y - skillWidth / 2);
			p1 = this.RotatePoint(p1, pivot, r);
			p2 = this.RotatePoint(p2, pivot, r);
			p3 = this.RotatePoint(p3, pivot, r);
			p4 = this.RotatePoint(p4, pivot, r);

			// Debug ----
			/*
			var prop1 = new Prop(10, attacker.RegionId, p1.X, p1.Y, 1, 0.5f);
			var prop2 = new Prop(10, attacker.RegionId, p2.X, p2.Y, 1, 0.5f);
			var prop3 = new Prop(10, attacker.RegionId, p3.X, p3.Y, 1, 0.5f);
			var prop4 = new Prop(10, attacker.RegionId, p4.X, p4.Y, 1, 0.5f);
			attacker.Region.AddProp(prop1);
			attacker.Region.AddProp(prop2);
			attacker.Region.AddProp(prop3);
			attacker.Region.AddProp(prop4);

			Task.Delay(10000).ContinueWith(_ =>
			{
				attacker.Region.RemoveProp(prop1);
				attacker.Region.RemoveProp(prop2);
				attacker.Region.RemoveProp(prop3);
				attacker.Region.RemoveProp(prop4);
			});
			*/
			// ----------

			// TargetProp
			var LProp = new Prop(280, attacker.RegionId, poe.X, poe.Y, MabiMath.ByteToRadian(attacker.Direction), 1f, 0f, "single");
			attacker.Region.AddProp(LProp);

			// Prepare Combat Actions
			var cap = new CombatActionPack(attacker, skill.Info.Id);

			var targetAreaId = new Location(attacker.RegionId, poe).ToLocationId();

			var aAction = new AttackerAction(CombatActionType.SpecialHit, attacker, targetAreaId);
			aAction.Set(AttackerOptions.KnockBackHit1 | AttackerOptions.UseEffect);
			aAction.PropId = LProp.EntityId;
			cap.Add(aAction);

			// Get targets in Polygon
			var targets = attacker.Region.GetCreaturesInPolygon(p1, p2, p3, p4).Where(x => attacker.CanTarget(x)).ToList();

			foreach (var target in targets)
			{
				var tAction = new TargetAction(CombatActionType.TakeHit, target, attacker, SkillId.CombatMastery);
				tAction.Set(TargetOptions.None);
				tAction.AttackerSkillId = skill.Info.Id;
				cap.Add(tAction);

				var damage = attacker.GetRndMagicDamage(skill, skill.RankData.Var1, skill.RankData.Var2);

				// Add damage if the skill is fully charged
				var dmgMultiplier = skill.RankData.Var4 / 100f;
				if (attacker.Temp.LightningRodFullCharge == true)
				{
					damage += (damage * dmgMultiplier);
				}

				// Critical Hit
				var critChance = attacker.GetRightCritChance(target.MagicProtection);
				CriticalHit.Handle(attacker, critChance, ref damage, tAction);

				// MDef and MProt
				SkillHelper.HandleMagicDefenseProtection(target, ref damage);

				// Mana Shield
				ManaShield.Handle(target, ref damage, tAction);

				// Mana Deflector
				var delayReduction = ManaDeflector.Handle(attacker, target, ref damage, tAction);

				// Apply Damage
				target.TakeDamage(tAction.Damage = damage, attacker);

				// Stun Time
				tAction.Stun = targetStun;

				// Reduce stun, based on ping
				if (delayReduction > 0)
					tAction.Stun = (short)Math.Max(0, tAction.Stun - (tAction.Stun / 100 * delayReduction));

				// Death or Knockback
				if (target.IsDead)
				{
					tAction.Set(TargetOptions.FinishingKnockDown);
					attacker.Shove(target, knockbackDistance);
				}
				else
				{
					// Always knock down
					if (target.Is(RaceStands.KnockDownable))
					{
						tAction.Set(TargetOptions.KnockDown);
						attacker.Shove(target, knockbackDistance);
					}
				}
				tAction.Creature.Stun = tAction.Stun;
			}
			cap.Handle();

			Send.Effect(attacker, Effect.LightningRod, 3, poe.X, poe.Y); // Lightning Shooting Effect?

			Send.SkillUse(attacker, skill.Info.Id, targetAreaId, 0, 1);
			skill.Train(1); // Use the Skill

			attacker.Region.RemoveProp(LProp);
		}

		public void Complete(Creature creature, Skill skill, Packet packet)
		{
			/* Unlocks ---------------------
			Walk|Run
			---------------------------- - */
			creature.Unlock(Locks.Walk | Locks.Run);

			creature.Temp.LightningRodFullCharge = false;

			Send.Effect(creature, Effect.LightningRod, 0); // End Effect
			Send.SkillComplete(creature, skill.Info.Id);
		}

		public void Cancel(Creature creature, Skill skill)
		{
			/* Unlocks ---------------------
			Walk|Run
			---------------------------- - */
			creature.Unlock(Locks.Walk | Locks.Run);

			creature.Temp.LightningRodFullCharge = false;

			Send.Effect(creature, Effect.LightningRod, 0); // End Effect
		}

		/// <summary>
		/// Training, called when someone attacks something.
		/// </summary>
		/// <param name="action"></param>
		public void OnCreatureAttackedByPlayer(TargetAction action)
		{
			// Check if skill used is LightningRod
			if (action.AttackerSkillId != SkillId.LightningRod)
				return;

			// Get skill
			var attackerSkill = action.Attacker.Skills.Get(SkillId.LightningRod);
			if (attackerSkill == null) return; // Should be impossible.

			// Learning by attacking
			switch (attackerSkill.Info.Rank)
			{
				case SkillRank.RF:
				case SkillRank.RE:
					attackerSkill.Train(2); // Attack an enemy
					break;
				case SkillRank.RD:
				case SkillRank.RC:
					attackerSkill.Train(2); // Attack an enemy
					if (action.Attacker.Temp.LightningRodFullCharge == true) attackerSkill.Train(3); // Attack an Enemy with a Max Charge
					break;
				case SkillRank.RB:
				case SkillRank.RA:
				case SkillRank.R9:
				case SkillRank.R8:
				case SkillRank.R7:
                case SkillRank.R6:
				case SkillRank.R5:
				case SkillRank.R4:
				case SkillRank.R3:
				case SkillRank.R2:
				case SkillRank.R1:
					if (action.Creature.IsDead) attackerSkill.Train(2); // Defeat an enemy
					if (action.Creature.IsDead && action.Attacker.Temp.LightningRodFullCharge == true) attackerSkill.Train(3); // Defeat an Enemy with a Max Charge
					break;
			}
		}

		/// <summary>
		/// Training, called when a creature attacks another creature(s)
		/// </summary>
		/// <param name="aAction"></param>
		public void OnCreatureAttacks(AttackerAction aAction)
		{
			// Handles the multiple target training requirements

			// Check if skill used is LightningRod
			if (aAction.SkillId != SkillId.LightningRod)
				return;

			// Get skill
			var attackerSkill = aAction.Creature.Skills.Get(SkillId.LightningRod);
			if (attackerSkill == null) return; // Should be impossible.

			// Get targets
			var targets = aAction.Pack.GetTargets();

			// Learning by attacking
			switch (attackerSkill.Info.Rank)
			{
				case SkillRank.RF:
				case SkillRank.RE:
				case SkillRank.RD:
				case SkillRank.RC:
				case SkillRank.RB:
				case SkillRank.RA:
					break;
				case SkillRank.R9:
				case SkillRank.R8:
				case SkillRank.R7:
					if (targets.Length >= 2) // Defeat 2 or more enemies
					{
						var killCount = 0;
						foreach (Creature target in targets)
						{
							if (target.IsDead)
							{
								killCount++;
							}
						}
						if (killCount >= 2)
							attackerSkill.Train(4);
					}
					break;
				case SkillRank.R6:
				case SkillRank.R5:
				case SkillRank.R4:
					if (targets.Length >= 3) // Defeat 3 or more enemies
					{
						var killCount = 0;
						foreach (Creature target in targets)
						{
							if (target.IsDead)
							{
								killCount++;
							}
						}
						if (killCount >= 3)
							attackerSkill.Train(4);
					}
					break;
				case SkillRank.R3:
				case SkillRank.R2:
					if (targets.Length >= 4) // Defeat 4 or more enemies
					{
						var killCount = 0;
						foreach (Creature target in targets)
						{
							if (target.IsDead)
							{
								killCount++;
							}
						}
						if (killCount >= 4)
							attackerSkill.Train(4);

						if (killCount >= 4 && aAction.Creature.Temp.LightningRodFullCharge == true) // Defeat 4 or more Enemies with a Max Charge
							attackerSkill.Train(5);
					}
					break;
				case SkillRank.R1:
					if (targets.Length >= 5) // Defeat 5 or more enemies
					{
						var killCount = 0;
						foreach (Creature target in targets)
						{
							if (target.IsDead)
							{
								killCount++;
							}
						}
						if (killCount >= 5)
							attackerSkill.Train(4);

						if (killCount >= 5 && aAction.Creature.Temp.LightningRodFullCharge == true) // Defeat 5 or more Enemies with a Max Charge
							attackerSkill.Train(5);
					}
					break;
			}
		}

		private Point RotatePoint(Point point, Point pivot, double radians)
		{
			var cosTheta = Math.Cos(radians);
			var sinTheta = Math.Sin(radians);

			var x = (int)(cosTheta * (point.X - pivot.X) - sinTheta * (point.Y - pivot.Y) + pivot.X);
			var y = (int)(sinTheta * (point.X - pivot.X) + cosTheta * (point.Y - pivot.Y) + pivot.Y);

			return new Point(x, y);
		}
	}
}
