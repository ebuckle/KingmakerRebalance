﻿using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.UnitLogic.Parts;
using UnityEngine;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.RuleSystem;
using Kingmaker.Visual.HitSystem;
using System;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Controllers.Units;
using Kingmaker.Designers;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Enums;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Blueprints.Items.Ecnchantments;
using Newtonsoft.Json;
using Kingmaker.Utility;
using Kingmaker.UI.GenericSlot;
using Kingmaker.Items;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.EntitySystem.Entities;
using System.Collections.Generic;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Class.Kineticist;
using Kingmaker.Blueprints.Validation;
using Kingmaker.Blueprints.Root;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.ElementsSystem;
using Kingmaker.Controllers;
using Kingmaker;
using static Kingmaker.UnitLogic.Abilities.Components.AbilityCustomMeleeAttack;
using Kingmaker.UnitLogic.Mechanics.ContextData;
using Kingmaker.Controllers.Projectiles;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.EntitySystem;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.UnitLogic.ActivatableAbilities.Restrictions;
using Kingmaker.UnitLogic.Alignments;
using Kingmaker.UnitLogic.Mechanics.Properties;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.EntitySystem.Persistence.Versioning;
using JetBrains.Annotations;
using Kingmaker.Enums.Damage;
using Kingmaker.Inspect;
using Kingmaker.UI.ActionBar;
using Kingmaker.UI.UnitSettings;
using Kingmaker.UI.Constructor;
using Kingmaker.UI.Tooltip;
using Kingmaker.Blueprints.Root.Strings;

namespace CallOfTheWild.OnCastMechanics
{
    [AllowedOn(typeof(Kingmaker.Blueprints.Facts.BlueprintUnitFact))]
    public class HealAfterSpellCast : RuleInitiatorLogicComponent<RuleCastSpell>
    {
        public BlueprintSpellbook spellbook;
        public SpellSchool school = SpellSchool.None;
        public ContextValue multiplier;

        public override void OnEventAboutToTrigger(RuleCastSpell evt)
        {

        }

        public override void OnEventDidTrigger(RuleCastSpell evt)
        {
            if (!evt.Success)
            {
                return;
            }
            var spellbook_blueprint = evt.Spell?.Spellbook?.Blueprint;
            if (spellbook_blueprint == null)
            {
                return;
            }

            if (evt.Spell.StickyTouch != null)
            {
                return;
            }

            if (spellbook != null && spellbook_blueprint != spellbook)
            {
                return;
            }

            if (school != SpellSchool.None && evt.Spell.Blueprint.School != school)
            {
                return;
            }

            int amount = evt.Context.SpellLevel * multiplier.Calculate(this.Fact.MaybeContext);
         
            evt.Context.TriggerRule<RuleHealDamage>(new RuleHealDamage(evt.Context.MaybeCaster, evt.Context.MaybeCaster, DiceFormula.Zero, amount));
        }
    }


    [AllowedOn(typeof(Kingmaker.Blueprints.Facts.BlueprintUnitFact))]
    public class ApplyBuffAfterSpellCast : RuleInitiatorLogicComponent<RuleCastSpell>
    {
        public BlueprintSpellbook spellbook;
        public SpellSchool school = SpellSchool.None;
        public DurationRate rate = DurationRate.Rounds;
        public BlueprintBuff buff;

        public override void OnEventAboutToTrigger(RuleCastSpell evt)
        {

        }

        public override void OnEventDidTrigger(RuleCastSpell evt)
        {
            if (!evt.Success)
            {
                return;
            }
            var spellbook_blueprint = evt.Spell?.Spellbook?.Blueprint;
            if (spellbook_blueprint == null)
            {
                return;
            }

            if (evt.Spell.StickyTouch != null)
            {
                return;
            }

            if (spellbook != null && spellbook_blueprint != spellbook)
            {
                return;
            }

            if (school != SpellSchool.None && evt.Spell.Blueprint.School != school)
            {
                return;
            }

            int amount = evt.Context.SpellLevel;
            TimeSpan? duration = new TimeSpan?(amount.Rounds().Seconds);

            this.Owner.Unit.Descriptor.AddBuff(buff, this.Owner.Unit, duration);
        }
    }


    [ComponentName("Buff on spawned unit")]
    [AllowedOn(typeof(BlueprintUnitFact))]
    public class OnSpawnBuff : RuleInitiatorLogicComponent<RuleSummonUnit>
    {
        public BlueprintBuff buff;
        public SpellDescriptorWrapper spell_descriptor = SpellDescriptor.None;
        public bool IsInfinity;
        [HideIf("IsInfinity")]
        public ContextDurationValue duration_value;
        public BlueprintSpellbook spellbook;
        public SpellSchool school = SpellSchool.None;

        public override void OnEventAboutToTrigger(RuleSummonUnit evt)
        {
        }

        public override void OnEventDidTrigger(RuleSummonUnit evt)
        {
            if (spellbook != null && evt.Context?.SourceAbilityContext?.Ability?.Spellbook?.Blueprint != spellbook)
            {
                return;
            }
            
            if (spell_descriptor != SpellDescriptor.None && (evt.Context.SpellDescriptor & spell_descriptor) != 0)
            {
                return;
            }
            if (school != SpellSchool.None && school != evt.Context.SpellSchool)
            {
                return;
            }
            Main.logger.Log("Spawn ok");
            TimeSpan? duration = !this.IsInfinity ? new TimeSpan?(duration_value.Calculate(this.Fact.MaybeContext).Seconds) : new TimeSpan?();
            evt.SummonedUnit.Descriptor.AddBuff(this.buff, evt.Initiator, duration, (AbilityParams)null);
        }
    }



    [AllowedOn(typeof(BlueprintUnitFact))]
    public class RangedSpellAttackRollBonusRangeAttackRollMetamagic : RuleInitiatorLogicComponent<RuleAttackRoll>
    {
        public BlueprintSpellbook spellbook;
        public ContextValue bonus;
        public ModifierDescriptor descriptor;


        public override void OnEventAboutToTrigger(RuleAttackRoll evt)
        {
            var ability = evt.Reason?.Ability;
            if (ability == null)
            {
                return;
            }
            var blueprint_spellbook = ability.Spellbook?.Blueprint;
            if (spellbook != null && blueprint_spellbook != spellbook)
            {
                return;
            }

            if (!ability.HasMetamagic((Metamagic)MetamagicFeats.MetamagicExtender.RangedAttackRollBonus))
            {
                return;
            }
            if (evt.AttackType != AttackType.RangedTouch)
            {
                return;
            }
            evt.AddTemporaryModifier(evt.Initiator.Stats.AdditionalAttackBonus.AddModifier(this.bonus.Calculate(this.Fact.MaybeContext), (GameLogicComponent)this, this.descriptor));
        }

        public override void OnEventDidTrigger(RuleAttackRoll evt)
        {
           
        }
    }


    [AllowMultipleComponents]
    [AllowedOn(typeof(BlueprintUnitFact))]
    public class IncreaseDurationBy1RoundIfMetamagic : RuleInitiatorLogicComponent<RuleApplyBuff>
    {
        public BlueprintSpellbook spellbook;
        public override void OnEventAboutToTrigger(RuleApplyBuff evt)
        {
            var ability = evt.Context.SourceAbilityContext?.Ability;
            if (ability == null)
            {
                return;
            }
            if (spellbook != null && ability.Spellbook?.Blueprint != spellbook)
            {
                return;
            }

            if (!ability.HasMetamagic((Metamagic)MetamagicFeats.MetamagicExtender.ExtraRoundDuration))
            {
                return;
            }
            
            TimeSpan round = 6.Seconds();

            if (!evt.Duration.HasValue)
            {
                return;
            }
            Harmony12.Traverse.Create(evt).Property("Duration").SetValue(evt.Duration + round);
        }

        public override void OnEventDidTrigger(RuleApplyBuff evt)
        {

        }
    }


    public class ForceFocusSpellDamageDiceIncrease : OwnedGameLogicComponent<UnitDescriptor>, IInitiatorRulebookHandler<RuleCalculateDamage>, IRulebookHandler<RuleCalculateDamage>, IInitiatorRulebookSubscriber
    {
        public SpellDescriptorWrapper SpellDescriptor;
        public BlueprintSpellbook spellbook;

        public void OnEventAboutToTrigger(RuleCalculateDamage evt)
        {
            MechanicsContext context = evt.Reason.Context;
            if (context?.SourceAbility == null || !context.SpellDescriptor.HasAnyFlag((Kingmaker.Blueprints.Classes.Spells.SpellDescriptor)this.SpellDescriptor) || !context.SourceAbility.IsSpell)
                return;

            if (spellbook != null && context.SourceAbilityContext?.Ability?.Spellbook?.Blueprint != spellbook)
            {
                return;
            }

            if (!context.HasMetamagic((Metamagic)MetamagicFeats.MetamagicExtender.ForceFocus))
            {
                return;
            }

            foreach (BaseDamage baseDamage in evt.DamageBundle)
            {
                var dice_formula = baseDamage.Dice;
                if (dice_formula.Dice == DiceType.Zero || dice_formula.Rolls <= 0)
                {
                    continue;
                }

                var dice = dice_formula.Dice;
                switch(dice)
                {
                    case DiceType.D4:
                                    {
                                        dice = DiceType.D6;
                                        break;
                                    }
                    case DiceType.D6:
                                    {
                                        dice = DiceType.D8;
                                        break;
                                    }
                    case DiceType.D8:
                                    {
                                        dice = DiceType.D10;
                                        break;
                                    }
                    case DiceType.D10:
                                    {
                                        dice = DiceType.D12;
                                        break;
                                    }
                }

                baseDamage.ReplaceDice(new DiceFormula(dice_formula.Rolls, dice));
            }
                
        }

        public void OnEventDidTrigger(RuleCalculateDamage evt)
        {
        }
    }
}
