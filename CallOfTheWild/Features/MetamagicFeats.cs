﻿using CallOfTheWild.NewMechanics.EnchantmentMechanics;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Area;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Items;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Blueprints.Root;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Enums.Damage;
using Kingmaker.Items;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UI.Common;
using Kingmaker.UI.Tooltip;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.AreaEffects;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.ActivatableAbilities.Restrictions;
using Kingmaker.UnitLogic.Alignments;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Mechanics.Conditions;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CallOfTheWild
{
    public class MetamagicFeats
    {
        [Flags]
        public enum MetamagicExtender
        {
            //in game metamagic is used up to 32, which is 0x00000020
            Intensified = 0x40000000,
            Dazing = 0x20000000,
            Persistent = 0x10000000,
            Rime = 0x08000000,
            Toppling = 0x04000000,
            Selective = 0x02000000,
            ElementalFire = 0x01000000,
            ElementalCold = 0x00800000,
            ElementalElectricity = 0x00400000,
            ElementalAcid = 0x00200000,
            Elemental = ElementalFire | ElementalCold | ElementalElectricity | ElementalAcid,
            BloodIntensity = 0x00100000,
            IntensifiedGeneral = BloodIntensity | Intensified,           
            Piercing = 0x00080000,
            ForceFocus = 0x00040000,
            RangedAttackRollBonus = 0x00020000,
            ExtraRoundDuration = 0x00010000,
            ImprovedSpellSharing = 0x00008000,
            BypassUndeadMindAffectingImmunity = 0x00004000,
            FreeMetamagic = ForceFocus | RangedAttackRollBonus | BloodIntensity | ExtraRoundDuration | ImprovedSpellSharing | BypassUndeadMindAffectingImmunity,
        }

        static public bool test_mode = false;
        static LibraryScriptableObject library => Main.library;

        static public BlueprintFeature intensified_metamagic;
        static public BlueprintFeature dazing_metamagic;
        static public BlueprintFeature toppling_metamagic;
        static public BlueprintFeature rime_metamagic;
        static public BlueprintFeature persistent_metamagic;
        static public BlueprintFeature selective_metamagic;
        static public BlueprintFeature piercing_metamagic;
        static public Dictionary<Metamagic, (SpellDescriptor, DamageEnergyType, BlueprintFeature)>  elemental_metamagic = new Dictionary<Metamagic, (SpellDescriptor, DamageEnergyType, BlueprintFeature)>();

        static readonly int[][] metamagic_rod_costs = new int[][] {
                                                                    new int[] { 3000, 11000, 24500 },
                                                                    new int[] { 9000, 32500, 73000 },
                                                                    new int[] { 14000, 54000, 121500 },
                                                                };

        public static void load()
        {
            createPiercingSpell();
            createIntensifiedSpell();
            createTopplingSpell();
            createRimeSpell();
            createDazingSpell();
            createPersistentSpell();
            createSelectiveSpell();
            createElementalMetamagic();

            //add metamagic text to spells 
            var original = Harmony12.AccessTools.Method(typeof(UIUtilityTexts), "GetMetamagicList");
            var patch = Harmony12.AccessTools.Method(typeof(UIUtilityTexts_GetMetamagicList_Patch), "Postfix");
            Main.harmony.Patch(original, postfix: new Harmony12.HarmonyMethod(patch));


            setFreeMetamagicFlags();
        }

        static void setFreeMetamagicFlags()
        {
            //force focus
            var spells = library.GetAllBlueprints().OfType<BlueprintAbility>().Where(b => b.IsSpell && (b.SpellDescriptor & SpellDescriptor.Force) != 0).ToArray();
            foreach (var s in spells)
            {
                s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)MetamagicExtender.ForceFocus;
                if (s.Parent != null)
                {
                    s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)MetamagicExtender.ForceFocus;
                }
            }


            //attack roll spells
            var spells_attack_roll = library.GetAllBlueprints().OfType<BlueprintAbility>().Where(b =>
            {
                if (!b.IsSpell)
                {
                    return false;
                }
                var c = b.GetComponent<AbilityDeliverProjectile>();
                if (c == null)
                {
                    return false;
                }

                return c.NeedAttackRoll;
            }
            ).ToArray();

            foreach (var s in spells_attack_roll)
            {
                s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)MetamagicExtender.RangedAttackRollBonus;
                if (s.Parent != null)
                {
                    s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)MetamagicExtender.RangedAttackRollBonus;
                }
            }
        }


        static void createSelectiveSpell()
        {
            selective_metamagic = library.CopyAndAdd<BlueprintFeature>("a1de1e4f92195b442adb946f0e2b9d4e", "SelectiveSpellFeature", "");
            selective_metamagic.SetNameDescriptionIcon("Metamagic (Selective Spell)",
                                                         "When casting a selective spell with an area effect and a duration of instantaneous, you can exclude your allies from the effects of your spell.\n"
                                                         + "Level Increase: +1 (a selective spell uses up a spell slot one level higher than the spell’s actual level.)\n"
                                                         + "Spells that do not have an area of effect or a duration of instantaneous do not benefit from this feat.",
                                                         LoadIcons.Image2Sprite.Create(@"FeatIcons/SelectiveSpell.png")
                                                        );
            selective_metamagic.AddComponent(Helpers.PrerequisiteStatValue(StatType.SkillKnowledgeArcana, 10));

            selective_metamagic.ReplaceComponent<AddMetamagicFeat>(a => a.Metamagic = (Metamagic)MetamagicExtender.Selective);
            AddMetamagicToFeatSelection(selective_metamagic);

            var spells = library.GetAllBlueprints().OfType<BlueprintAbility>().Where(b => b.IsSpell && b.LocalizedDuration.ToString().Empty() && b.HasAreaEffect() && b.EffectOnEnemy == AbilityEffectOnUnit.Harmful).Cast<BlueprintAbility>().ToArray();
            foreach (var s in spells)
            {
                s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)MetamagicExtender.Selective;
                if (s.Parent != null)
                {
                    s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)MetamagicExtender.Selective;
                }
            }
        }



        static void createPiercingSpell()
        {
            piercing_metamagic = library.CopyAndAdd<BlueprintFeature>("a1de1e4f92195b442adb946f0e2b9d4e", "PiercingSpellFeature", "");
            piercing_metamagic.SetNameDescriptionIcon("Metamagic (Piercing Spell)",
                                                        "When you cast a piercing spell against a target with spell resistance, it treats the spell resistance of the target as 5 lower than its actual SR.\n"
                                                        + "Level Increase: +1 (a piercing spell uses up a spell slot one level higher than the spell’s actual level.)\n",
                                                        LoadIcons.Image2Sprite.Create(@"FeatIcons/PiercingSpell.png")
                                                        );

            piercing_metamagic.ReplaceComponent<AddMetamagicFeat>(a => a.Metamagic = (Metamagic)MetamagicExtender.Piercing);
            AddMetamagicToFeatSelection(piercing_metamagic);

            var spells = library.GetAllBlueprints().OfType<BlueprintAbility>().Where(b => b.IsSpell && b.SpellResistance == true).Cast<BlueprintAbility>().ToArray();
            foreach (var s in spells)
            {
                s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)MetamagicExtender.Piercing;
                if (s.Parent != null)
                {
                    s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)MetamagicExtender.Piercing;
                }
            }
        }


        static void createMetamagicRod(BlueprintFeature feature)
        {
            var zarcies = ResourcesLibrary.TryGetBlueprint<BlueprintSharedVendorTable>("5450d563aab78134196ee9a932e88671");
            
            var names = new string[] { "Lesser", "Regular", "Greater" };
            var display_prefix = new string[] { "Lesser ", "Standard", "Greater " };
            var max_level = new string[] { "3rd", "6th", "9th " };

            var metamagic = feature.GetComponent<AddMetamagicFeat>().Metamagic;
            var metamagic_cost = calculateNewMetamagicCost(metamagic);

            string[] prototype_guids = new string[] {"1e7a5a4d257cf434a87e687c9ee7a872", "a02f06b63af839a448147dadff3724f2", "a02f06b63af839a448147dadff3724f2" };

            var display_name = feature.Name.Replace("(", "").Replace(")", "").Replace("Metamagic", "").Replace("Spell", "").Trim();
            for (int i = 0; i < 3; i++)
            {
                var rod_name = names[i] + " " + display_name + " Metamagic Rod";
                var description = $"The wielder can cast up to three spells per day that are affected as though the spells were augmented with the {feature.Name} feat.\n" +
                    $"{names[i]} rods can be used with spells of {max_level[i]} level or lower.\n" +
                    $"{feature.Name}: {feature.Description}";


                var rod = library.CopyAndAdd<BlueprintItemEquipmentUsable>(prototype_guids[i], feature.name + $"{i+1}Rod", "");
                Helpers.SetField(rod, "m_Cost", metamagic_rod_costs[metamagic_cost - 1][i]);
                Helpers.SetField(rod, "m_DisplayNameText", Helpers.CreateString(rod.name + ".Name", rod_name));
                Helpers.SetField(rod, "m_DescriptionText", Helpers.CreateString(rod.name + ".Description", description));

                rod.ActivatableAbility = library.CopyAndAdd<BlueprintActivatableAbility>(rod.ActivatableAbility.AssetGuid, feature.name + $"{i+1}RodActivatableAbility", "");
                rod.ActivatableAbility.SetNameDescription(rod.Name, rod.Description);
                rod.ActivatableAbility.Buff = library.CopyAndAdd<BlueprintBuff>(rod.ActivatableAbility.Buff.AssetGuid, feature.name + $"{i+1}RodBuff", "");
                rod.ActivatableAbility.Buff.SetNameDescription(rod.Name, rod.Description);
                rod.ActivatableAbility.Buff.ReplaceComponent<MetamagicRodMechanics>(m => { m.Metamagic = metamagic; m.MaxSpellLevel = (i + 1) * 3; m.RodAbility = rod.ActivatableAbility; });

                //add to zarcie
                Helpers.AddItemToSpecifiedVendorTable(zarcies, rod, 5);
            }
        }


        static void AddMetamagicToFeatSelection(BlueprintFeature feat)
        {
            library.AddFeats(feat);
            var selections = new BlueprintFeatureSelection[]{library.Get<BlueprintFeatureSelection>("d6dd06f454b34014ab0903cb1ed2ade3"),
                                                            library.Get<BlueprintFeatureSelection>("8c3102c2ff3b69444b139a98521a4899"),
                                                           };

            foreach (var s in selections)
            {
                s.AllFeatures = s.AllFeatures.AddToArray(feat);
            }

            createMetamagicRod(feat);
        }


        static void createElementalMetamagic()
        {
            var acid_icon = library.Get<BlueprintFeature>("52135eada006e9045a848cd659749608").Icon;
            var fire_icon = library.Get<BlueprintFeature>("13bdf8d542811ac4ca228a53aa108145").Icon;
            var elec_icon = library.Get<BlueprintFeature>("d439691f37d17804890bd9c263ae1e80").Icon;
            var cold_icon = library.Get<BlueprintFeature>("2ed9d8bf76412ba4a8afe38fa9925fca").Icon;

            createElementalSpell(MetamagicExtender.ElementalAcid, "Acid", SpellDescriptor.Acid, DamageEnergyType.Acid, acid_icon);
            createElementalSpell(MetamagicExtender.ElementalCold, "Cold", SpellDescriptor.Cold, DamageEnergyType.Cold, cold_icon);
            createElementalSpell(MetamagicExtender.ElementalFire, "Fire", SpellDescriptor.Fire, DamageEnergyType.Fire, fire_icon);
            createElementalSpell(MetamagicExtender.ElementalElectricity, "Electricity", SpellDescriptor.Electricity, DamageEnergyType.Electricity, elec_icon);
        }


        static void createElementalSpell(MetamagicExtender metamagic, string Name, SpellDescriptor descriptor, DamageEnergyType energy, UnityEngine.Sprite icon)
        {
            var feat = library.CopyAndAdd<BlueprintFeature>("a1de1e4f92195b442adb946f0e2b9d4e", Name + "ElementalSpellFeature", "");
            feat.SetNameDescriptionIcon($"Metamagic (Elemental {Name})",
                                                         "Choose one energy type: acid, cold, electricity, or fire. You may replace a spell’s normal damage with that energy type or split the spell’s damage, so that half is of that energy type and half is of its normal type.\n"
                                                         + "Level Increase: +1 (an elemental spell uses up a spell slot one level higher than the spell’s actual level.)\n"
                                                         + "You can gain this feat multiple times. Each time you must choose a different energy type.",
                                                         icon
                                                        );

            feat.ReplaceComponent<AddMetamagicFeat>(a => a.Metamagic = (Metamagic)metamagic);
            AddMetamagicToFeatSelection(feat);

            var spells = library.GetAllBlueprints().OfType<BlueprintAbility>().Where(b => b.IsSpell
                                                                                     && b.EffectOnEnemy == AbilityEffectOnUnit.Harmful
                                                                                     && ((b.AvailableMetamagic & Metamagic.Maximize) != 0)
                                                                                     && (b.SpellDescriptor & (SpellDescriptor.Fire | SpellDescriptor.Cold | SpellDescriptor.Electricity | SpellDescriptor.Acid)) != 0).Cast<BlueprintAbility>().ToArray();
            foreach (var s in spells)
            {
                s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)metamagic;
                if (s.Parent != null)
                {
                    s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)metamagic;
                }
            }

            elemental_metamagic.Add((Metamagic)metamagic, new ValueTuple<SpellDescriptor, DamageEnergyType, BlueprintFeature>(descriptor, energy, feat));
        }

        static void createPersistentSpell()
        {
            persistent_metamagic = library.CopyAndAdd<BlueprintFeature>("a1de1e4f92195b442adb946f0e2b9d4e", "PersistentSpellFeature", "");
            persistent_metamagic.SetNameDescriptionIcon("Metamagic (Persistent Spell)",
                                                         "Whenever a creature targeted by a persistent spell or within its area succeeds on its saving throw against the spell, it must make another saving throw against the effect. If a creature fails this second saving throw, it suffers the full effects of the spell, as if it had failed its first saving throw.\n"
                                                         + "Level Increase: +2 (a persistent spell uses up a spell slot two levels higher than the spell’s actual level.)\n"
                                                         + "Spells that do not require a saving throw to resist or lessen the spell’s effect do not benefit from this feat.",
                                                         LoadIcons.Image2Sprite.Create(@"FeatIcons/PersistentSpell.png")
                                                        );

            persistent_metamagic.ReplaceComponent<AddMetamagicFeat>(a => a.Metamagic = (Metamagic)MetamagicExtender.Persistent);
            AddMetamagicToFeatSelection(persistent_metamagic);

            var spells = library.GetAllBlueprints().OfType<BlueprintAbility>().Where(b => b.IsSpell && b.LocalizedSavingThrow.ToString() != Helpers.savingThrowNone.ToString() && !b.LocalizedSavingThrow.ToString().Empty()).Cast<BlueprintAbility>().ToArray();
            foreach (var s in spells)
            {
                s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)MetamagicExtender.Persistent;
                if (s.Parent != null)
                {
                    s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)MetamagicExtender.Persistent;
                }
            }
        }


        static void createDazingSpell()
        {
            dazing_metamagic = library.CopyAndAdd<BlueprintFeature>("a1de1e4f92195b442adb946f0e2b9d4e", "DazingSpellFeature", "");
            dazing_metamagic.SetNameDescriptionIcon("Metamagic (Dazing Spell)",
                                                         "You can modify a spell to daze a creature damaged by the spell. When a creature takes damage from this spell, they become dazed for a number of rounds equal to the original level of the spell. If the spell allows a saving throw, a successful save negates the daze effect. If the spell does not allow a save, the target can make a Will save to negate the daze effect. \n"
                                                         + "Level Increase: +3 (a dazing spell uses up a spell slot three levels higher than the spell’s actual level.)\n"
                                                         + "Spells that do not inflict damage do not benefit from this feat.",
                                                         LoadIcons.Image2Sprite.Create(@"FeatIcons/DazingSpell.png")
                                                        );

            dazing_metamagic.ReplaceComponent<AddMetamagicFeat>(a => a.Metamagic = (Metamagic)MetamagicExtender.Dazing);
            AddMetamagicToFeatSelection(dazing_metamagic);

            var spells = library.GetAllBlueprints().OfType<BlueprintAbility>().Where(b => b.IsSpell && b.EffectOnEnemy == AbilityEffectOnUnit.Harmful && ((b.AvailableMetamagic & Metamagic.Maximize) != 0)).Cast<BlueprintAbility>().ToArray();
            foreach (var s in spells)
            {
                s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)MetamagicExtender.Dazing;
                if (s.Parent != null)
                {
                    s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)MetamagicExtender.Dazing;
                }
            }
        }


        static void createRimeSpell()
        {
            rime_metamagic = library.CopyAndAdd<BlueprintFeature>("a1de1e4f92195b442adb946f0e2b9d4e", "RimeSpellFeature", "");
            rime_metamagic.SetNameDescriptionIcon("Metamagic (Rime Spell)",
                                                         "The frost of your cold spell clings to the target, impeding it for a short time. A rime spell causes creatures that takes cold damage from the spell to become entangled for a number of rounds equal to the original level of the spell.\n"
                                                         + "Level Increase: +1 (a rime spell uses up a spell slot one level higher than the spell’s actual level.)\n"
                                                         + "This feat only affects spells with the cold descriptor.",
                                                         LoadIcons.Image2Sprite.Create(@"FeatIcons/RimeSpell.png")
                                                        );

            rime_metamagic.ReplaceComponent<AddMetamagicFeat>(a => a.Metamagic = (Metamagic)MetamagicExtender.Rime);           
            AddMetamagicToFeatSelection(rime_metamagic);

            var spells = library.GetAllBlueprints().OfType<BlueprintAbility>().Where(b => b.IsSpell 
                                                                                     && b.EffectOnEnemy == AbilityEffectOnUnit.Harmful 
                                                                                     && ((b.AvailableMetamagic & Metamagic.Maximize) != 0) 
                                                                                     && (b.SpellDescriptor & (SpellDescriptor.Fire | SpellDescriptor.Cold | SpellDescriptor.Electricity | SpellDescriptor.Acid)) != 0 ).Cast<BlueprintAbility>().ToArray();
            foreach (var s in spells)
            {
                s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)MetamagicExtender.Rime;
                if (s.Parent != null)
                {
                    s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)MetamagicExtender.Rime;
                }
            }
        }


        static void createTopplingSpell()
        {
            toppling_metamagic = library.CopyAndAdd<BlueprintFeature>("a1de1e4f92195b442adb946f0e2b9d4e", "TopplingSpellFeature", "");
            toppling_metamagic.SetNameDescriptionIcon("Metamagic (Toppling Spell)",
                                                         "The impact of your force spell is strong enough to knock the target prone. If the target takes damage, fails its saving throw, or is moved by your force spell, make a trip check against the target, using your caster level plus your casting ability score bonus (Wisdom for clerics, Intelligence for wizards, and so on). \n"
                                                         + "Level Increase: +1 (a toppling spell uses up a spell slot one level higher than the spell’s actual level.)\n"
                                                         + "A toppling spell only affects spells with the force descriptor.",
                                                         LoadIcons.Image2Sprite.Create(@"FeatIcons/TopplingSpell.png")
                                                        );

            toppling_metamagic.ReplaceComponent<AddMetamagicFeat>(a => a.Metamagic = (Metamagic)MetamagicExtender.Toppling);
            AddMetamagicToFeatSelection(toppling_metamagic);

            var spells = new BlueprintAbility[]
                                                {
                                                    library.Get<BlueprintAbility>("4ac47ddb9fa1eaf43a1b6809980cfbd2"), //magic missile
                                                    library.Get<BlueprintAbility>("0a2f7c6aa81bc6548ac7780d8b70bcbc"), //battering blast
                                                };
            foreach (var s in spells)
            {
                s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)MetamagicExtender.Toppling;
                if (s.Parent != null)
                {
                    s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)MetamagicExtender.Toppling;
                }
            }
        }


        static void createIntensifiedSpell()
        {

            intensified_metamagic = library.CopyAndAdd<BlueprintFeature>("a1de1e4f92195b442adb946f0e2b9d4e", "IntensifiedSpellFeature", "");
            intensified_metamagic.SetNameDescriptionIcon("Metamagic (Intensified Spell)",
                                                         "An intensified spell increases the maximum number of damage dice by 5 levels. You must actually have sufficient caster levels to surpass the maximum in order to benefit from this feat. No other variables of the spell are affected, and spells that inflict damage that is not modified by caster level are not affected by this feat.\n"
                                                         + "Level Increase: +1 (an intensified spell uses up a spell slot one level higher than the spell’s actual level.)",
                                                         LoadIcons.Image2Sprite.Create(@"FeatIcons/IntensifiedSpell.png")
                                                         );
            intensified_metamagic.ReplaceComponent<AddMetamagicFeat>(a => a.Metamagic = (Metamagic)MetamagicExtender.Intensified);
            AddMetamagicToFeatSelection(intensified_metamagic);


           var spells = library.GetAllBlueprints().Where(b => (b is BlueprintAbility) && (b as BlueprintAbility).IsSpell).Cast<BlueprintAbility>().ToArray();

            foreach (var s in spells)
            {
                var spell = s;
                if (s.StickyTouch != null)
                {
                    spell = s.StickyTouch.TouchDeliveryAbility;
                }
                var run_action = spell.GetComponent<AbilityEffectRunAction>()?.Actions;
                if (run_action == null)
                {
                    continue;
                }
                var damage = Common.extractActions<ContextActionDealDamage>(run_action.Actions).Where(d => d.Value.DiceCountValue.ValueType == ContextValueType.Rank).ToArray();
                var context_rank_configs = spell.GetComponents<ContextRankConfig>().Where(c =>                                                                                     
                                                                                          Helpers.GetField<ContextRankBaseValueType>(c, "m_BaseValueType") == ContextRankBaseValueType.CasterLevel
                                                                                          && Helpers.GetField<bool>(c, "m_UseMax") == true
                                                                                          && Helpers.GetField<int>(c, "m_Max") != Helpers.GetField<int>(c, "m_Min")                                                                                 
                                                                                      ).ToArray();
                if (damage.Empty() || context_rank_configs.Empty())
                {
                    continue;
                }

                foreach (var d in damage)
                {
                    var config = context_rank_configs.FirstOrDefault(c => Helpers.GetField<AbilityRankType>(c, "m_Type") == d.Value.DiceCountValue.ValueRank);
                    if (config != null)
                    {
                        Helpers.SetField(config, "m_Feature", intensified_metamagic);
                        s.AvailableMetamagic = s.AvailableMetamagic | (Metamagic)MetamagicExtender.IntensifiedGeneral;
                        if (s.Parent != null)
                        {
                            s.Parent.AvailableMetamagic = s.Parent.AvailableMetamagic | (Metamagic)MetamagicExtender.IntensifiedGeneral;
                        }
                    }
                }
            }

            var acid_spray_buff = library.Get<BlueprintBuff>("ab9cfc0c9411e6441b738dcf5a3567ee");
            Helpers.SetField(acid_spray_buff.GetComponent<ContextRankConfig>(), "m_Feature", intensified_metamagic);
        }


        static int intensify_watcher = 0;


        [Harmony12.HarmonyPatch(typeof(ContextRankConfig))]
        [Harmony12.HarmonyPatch("GetValue", Harmony12.MethodType.Normal)]
        static class ContextRankConfig_GetValue_Patch
        {
            internal static bool Prefix(ContextRankConfig __instance, MechanicsContext context, ref int __result)
            {
                intensify_watcher = 0;
                if (context.HasMetamagic((Metamagic)MetamagicExtender.Intensified))
                {
                    intensify_watcher = 5;
                }

                if (context.HasMetamagic((Metamagic)MetamagicExtender.BloodIntensity))
                {
                    var value = Math.Max(context.MaybeCaster.Descriptor.Stats.Charisma.Bonus, context.MaybeCaster.Descriptor.Stats.Strength);
                    if (intensify_watcher < value)
                    {
                        intensify_watcher = value;
                    }
                }
                return true;
            }

            internal static void Postfix(ContextRankConfig __instance, MechanicsContext context, ref int __result)
            {
                 intensify_watcher = 0;
            }
        }


        [Harmony12.HarmonyPatch(typeof(ContextRankConfig))]
        [Harmony12.HarmonyPatch("ApplyMinMax", Harmony12.MethodType.Normal)]
        static class ContextRankConfig_ApplyMinMax_Patch
        {
            internal static bool Prefix(ContextRankConfig __instance, int value, ref int __result)
            {
                __result = value;
                bool intensified_allowed = Helpers.GetField<BlueprintFeature>(__instance, "m_Feature") == intensified_metamagic && intensify_watcher != 0;
                if (Helpers.GetField<bool>(__instance, "m_UseMin"))
                    __result = Math.Max(__result, Helpers.GetField<int>(__instance, "m_Min"));
                if (Helpers.GetField<bool>(__instance, "m_UseMax"))
                    __result = Math.Min(__result, Helpers.GetField<int>(__instance, "m_Max") + (intensified_allowed ? intensify_watcher : 0));

                return false;
            }
        }


        static int calculateNewMetamagicCost(Metamagic metamagic)
        {
            switch ((MetamagicExtender)metamagic)
            {
                case MetamagicExtender.Dazing:
                    return 3;
                case MetamagicExtender.Persistent:
                    return 2;
                case MetamagicExtender.Rime:
                case MetamagicExtender.Toppling:
                case MetamagicExtender.Intensified:
                case MetamagicExtender.ElementalFire:
                case MetamagicExtender.ElementalCold:
                case MetamagicExtender.ElementalElectricity:
                case MetamagicExtender.ElementalAcid:
                case MetamagicExtender.Selective:
                case MetamagicExtender.Piercing:
                    return 1;
            }
            return 0;
        }


        [Harmony12.HarmonyPatch(typeof(MetamagicHelper))]
        [Harmony12.HarmonyPatch("DefaultCost", Harmony12.MethodType.Normal)]
        static class MetamagicHelper_DefaultCost_Patch
        {
            internal static bool Prefix(Metamagic metamagic, ref int __result)
            {
                switch ((MetamagicExtender)metamagic)
                {
                    case MetamagicExtender.Dazing:
                        __result = 3;
                        return false;
                    case MetamagicExtender.Persistent:
                        __result = 2;
                        return false;
                    case MetamagicExtender.Rime:
                    case MetamagicExtender.Toppling:
                    case MetamagicExtender.Intensified:
                    case MetamagicExtender.ElementalFire:
                    case MetamagicExtender.ElementalCold:
                    case MetamagicExtender.ElementalElectricity:
                    case MetamagicExtender.ElementalAcid:
                    case MetamagicExtender.Selective:
                    case MetamagicExtender.Piercing:
                        __result = 1;
                        return false;
                }
                return true;
            }
        }

        [Harmony12.HarmonyPatch(typeof(MetamagicHelper))]
        [Harmony12.HarmonyPatch("SpellIcon", Harmony12.MethodType.Normal)]
        static class MetamagicHelper_SpellIcon_Patch
        {
            internal static bool Prefix(Metamagic metamagic, ref Sprite __result)
            {
                switch ((MetamagicExtender)metamagic)
                {
                    case MetamagicExtender.Intensified:
                        __result = UIRoot.Instance.SpellBookColors.MetamagicEmpower;
                        return false;
                    case MetamagicExtender.Dazing:
                    case MetamagicExtender.Toppling:
                    case MetamagicExtender.Selective:
                        __result = UIRoot.Instance.SpellBookColors.MetamagicReach;
                        return false;
                    case MetamagicExtender.Rime:
                    case MetamagicExtender.ElementalAcid:
                    case MetamagicExtender.ElementalCold:
                    case MetamagicExtender.ElementalFire:
                    case MetamagicExtender.ElementalElectricity:
                        __result = UIRoot.Instance.SpellBookColors.MetamagicHeighten;
                        return false;
                    case MetamagicExtender.Persistent:
                    case MetamagicExtender.Piercing:
                        __result = UIRoot.Instance.SpellBookColors.MetamagicMaximize;
                        return false;
                }
                
                return true;
            }
        }


        public interface IRuleSavingThrowTriggered : IGlobalSubscriber
        {
            void ruleSavingThrowTriggered(RuleSavingThrow evt);
        }


        [Harmony12.HarmonyPatch(typeof(RuleSavingThrow))]
        [Harmony12.HarmonyPatch("OnTrigger", Harmony12.MethodType.Normal)]
        static class RuleSavingThrow_OnTrigger_Patch
        {
            internal static void Postfix(RuleSavingThrow __instance, RulebookEventContext context)
            {
                EventBus.RaiseEvent<IRuleSavingThrowTriggered>((Action<IRuleSavingThrowTriggered>)(h => h.ruleSavingThrowTriggered(__instance)));

                if (__instance.Initiator.Descriptor.State.IsDead)
                    return;

     
                var context2 = __instance.Reason?.Context;
                //var ability = __instance.Reason?.;

                if (context2 == null)
                {
                    return;
                }

                if (!context2.HasMetamagic((Metamagic)MetamagicExtender.Persistent))
                {
                    return;
                }

                if (__instance.IsPassed)
                {
                    int old_value = __instance.D20;
                    Harmony12.Traverse.Create(__instance).Property("D20").SetValue(RulebookEvent.Dice.D20);
                    int new_value = __instance.D20;
                    Common.AddBattleLogMessage(__instance.Initiator.CharacterName + " rerolls saving throw due to persistent spell: " + $"{old_value}  >>  {new_value}");
                }
            }
        }


        [Harmony12.HarmonyPatch(typeof(RuleDealDamage))]
        [Harmony12.HarmonyPatch("OnTrigger", Harmony12.MethodType.Normal)]
        static class RuleDealDamage_OnTrigger_Patch
        {
            static BlueprintBuff entangled = library.Get<BlueprintBuff>("f7f6330726121cf4b90a6086b05d2e38");
            static BlueprintBuff dazed = Common.dazed_non_mind_affecting;
            internal static void Postfix(RuleDealDamage __instance, RulebookEventContext context)
            {
                var spellContext = Helpers.GetMechanicsContext()?.SourceAbilityContext;
                if (spellContext == null)
                {                   
                    var source_buff = (__instance.Reason?.Item as ItemEntityWeapon)?.Blueprint.GetComponent<NewMechanics.EnchantmentMechanics.WeaponSourceBuff>()?.buff;
                    if (source_buff != null)
                    {
                        spellContext = __instance.Initiator.Buffs?.GetBuff(source_buff)?.MaybeContext?.SourceAbilityContext;
                    }
                }
                var target = __instance.Target;
                if (spellContext == null || target == null)
                {
                    return;
                }

                var spell_level = spellContext.Params.SpellLevel;
                if (spellContext.HasMetamagic((Metamagic)MetamagicExtender.Toppling))
                {
                    GameAction toppling_action = Helpers.Create<ContextActionCombatManeuver>(a => { a.Type = Kingmaker.RuleSystem.Rules.CombatManeuver.Trip; a.ReplaceStat = true;  a.UseCastingStat = true; a.UseCasterLevelAsBaseAttack = true; a.OnSuccess = Helpers.CreateActionList(); a.IgnoreConcealment = true; });
                    Common.runActionOnDamageDealt(__instance, Helpers.CreateActionList(toppling_action));
                }

                if (spellContext.HasMetamagic((Metamagic)MetamagicExtender.Rime))
                {
                    GameAction rime_action = Helpers.CreateApplyBuff(entangled, Helpers.CreateContextDuration(spell_level), fromSpell: true);
                    Common.runActionOnDamageDealt(__instance, Helpers.CreateActionList(rime_action), descriptor: SpellDescriptor.Cold);
                }

                if (spellContext.HasMetamagic((Metamagic)MetamagicExtender.Dazing))
                {
                    GameAction dazed_action = Helpers.CreateApplyBuff(dazed, Helpers.CreateContextDuration(spell_level), fromSpell: true);
                    Common.runActionOnDamageDealt(__instance, Helpers.CreateActionList(dazed_action), save_type: SavingThrowType.Will, use_existing_save: true);
                }
            }
        }


        [Harmony12.HarmonyPatch(typeof(RuleCastSpell))]
        [Harmony12.HarmonyPatch("OnTrigger", Harmony12.MethodType.Normal)]
        static class RuleCastSpell_OnTrigger_Patch
        {
            internal static void Postfix(RuleCastSpell __instance, RulebookEventContext context)
            {
                var context2 = __instance.Context;

                if (context2?.AbilityBlueprint == null || context2?.Params == null)
                {
                    return;
                }

                if (context2.AbilityBlueprint.IsSpell &&
                    (context2.Params.Metamagic & (Metamagic)MetamagicExtender.Elemental) != 0)
                {
                    foreach (var key_value in elemental_metamagic)
                    {
                        if (context2.Params.HasMetamagic((Metamagic)key_value.Key) )
                        {
                            context2.AddSpellDescriptor(key_value.Value.Item1);
                            return;
                        }
                    }                   
                }
            }
        }


        [Harmony12.HarmonyPatch(typeof(RulePrepareDamage))]
        [Harmony12.HarmonyPatch("OnTrigger", Harmony12.MethodType.Normal)]
        static class RulePrepareDamage_OnTrigger_Patch
        {
            internal static bool Prefix(RulePrepareDamage __instance, RulebookEventContext context)
            {
                var context2  = Helpers.GetMechanicsContext()?.SourceAbilityContext;
                if (context2 == null)
                {
                    var source_buff = (__instance.Reason?.Item as ItemEntityWeapon)?.Blueprint.GetComponent<NewMechanics.EnchantmentMechanics.WeaponSourceBuff>()?.buff;

                    if (source_buff != null)
                    {
                        context2 = __instance.Initiator.Buffs?.GetBuff(source_buff)?.MaybeContext?.SourceAbilityContext;
                    }
                }
                if (context2 == null)
                {
                    return true;
                }
                if (context2.AbilityBlueprint.IsSpell &&
                    (context2.Params.Metamagic & (Metamagic)MetamagicExtender.Elemental) != 0)
                {
                    foreach (var key_value in elemental_metamagic)
                    {
                        if (context2.Params.HasMetamagic((Metamagic)key_value.Key))
                        {
                            foreach (BaseDamage item in __instance.DamageBundle)
                            {
                                (item as EnergyDamage)?.ReplaceEnergy(key_value.Value.Item2);
                            }
                            return true;
                        }
                    }
                }
                return true;
            }
        }

        [Harmony12.HarmonyPatch(typeof(UnitPartSpellResistance))]
        [Harmony12.HarmonyPatch("IsImmune", Harmony12.MethodType.Normal)]
        static class UnitPartSpellResistance_IsImmune_Patch
        {
            internal static bool Prefix(UnitPartSpellResistance __instance, MechanicsContext context, ref bool __result)
            {
                if (context?.Params == null || context.MaybeCaster == null)
                {
                    return true;
                }

                if (!context.MaybeCaster.IsEnemy(__instance.Owner.Unit) && context.HasMetamagic((Metamagic)MetamagicExtender.Selective))
                {
                    __result = true;
                    return false;
                }

                return true;
            }
        }


        [Harmony12.HarmonyPatch(typeof(AbilityEffectRunAction), "Apply", typeof(AbilityExecutionContext), typeof(TargetWrapper))]
        static class AbilityEffectRunAction_Apply_Patch
        {
            internal static bool Prefix(AbilityExecutionContext context, TargetWrapper target)
            {
                if (!target.IsUnit)
                {
                    return true;
                }
                if (context?.Params == null || context.MaybeCaster == null)
                {
                    return true;
                }

                if (!context.MaybeCaster.IsEnemy(target.Unit) && context.HasMetamagic((Metamagic)MetamagicExtender.Selective))
                {
                    return false;
                }

                var time_stopped_part = target.Unit.Get<TImeStopMechanics.TimeStoppedUnitPart>();
                if (time_stopped_part != null && time_stopped_part.active())
                {
                    return false;
                }

                return true;
            }


            internal static void Postfix(AbilityEffectRunAction __instance, AbilityExecutionContext context, TargetWrapper target)
            {
                var caster = context.MaybeCaster;
                if (caster == null)
                {
                    return;
                }

                var pet = caster.Descriptor.Pet;
                if (pet == null || pet.Descriptor.State.IsDead)
                {
                    return;
                }

                if (!context.HasMetamagic((Metamagic)MetamagicExtender.ImprovedSpellSharing))
                {
                    return;
                }

                using (context.GetDataScope(pet))
                {
                    __instance.Actions.Run();
                }
            }

        }


        static class UIUtilityTexts_GetMetamagicList_Patch
        {
            static void Postfix(Metamagic mask, ref string __result)
            {          
                string extra_metamagic = "";
            
                if ((mask & (Metamagic)MetamagicExtender.Intensified) != 0)
                {
                    extra_metamagic += "Intesnsified, ";
                }
                if ((mask & (Metamagic)MetamagicExtender.Piercing) != 0)
                {
                    extra_metamagic += "Piercing, ";
                }
                if ((mask & (Metamagic)MetamagicExtender.Toppling) != 0)
                {
                    extra_metamagic += "Toppling, ";
                }
                if ((mask & (Metamagic)MetamagicExtender.Selective) != 0)
                {
                    extra_metamagic += "Selective, ";
                }
                if ((mask & (Metamagic)MetamagicExtender.Dazing) != 0)
                {
                    extra_metamagic += "Dazing, ";
                }
                if ((mask & (Metamagic)MetamagicExtender.Rime) != 0)
                {
                    extra_metamagic += "Rime, ";
                }
                if ((mask & (Metamagic)MetamagicExtender.Persistent) != 0)
                {
                    extra_metamagic += "Persistent, ";
                }
                if ((mask & (Metamagic)MetamagicExtender.ElementalAcid) != 0)
                {
                    extra_metamagic += "Elemental Acid, ";
                }
                if ((mask & (Metamagic)MetamagicExtender.ElementalCold) != 0)
                {
                    extra_metamagic += "Elemental Cold, ";
                }
                if ((mask & (Metamagic)MetamagicExtender.ElementalElectricity) != 0)
                {
                    extra_metamagic += "Elemental Electricity, ";
                }
                if ((mask & (Metamagic)MetamagicExtender.ElementalFire) != 0)
                {
                    extra_metamagic += "Elemental Fire, ";
                }

                if (extra_metamagic.Length > 2)
                {
                    extra_metamagic = extra_metamagic.Substring(0, extra_metamagic.Length - 2);
                }
                if (!__result.Empty())
                {
                    __result += ", ";
                }

                __result += extra_metamagic;
            }
        }


        [Harmony12.HarmonyPatch(typeof(RuleSpellResistanceCheck))]
        [Harmony12.HarmonyPatch("OnTrigger", Harmony12.MethodType.Normal)]
        static class RuleSpellResistanceCheck_OnTrigger_Patch
        {
            internal static void Postfix(RuleSpellResistanceCheck __instance, RulebookEventContext context)
            {
                
                var context2 = __instance.Context;
                if (context2?.SourceAbility == null || context2?.Params == null)
                {
                    return;
                }

                if (context2.SourceAbility.IsSpell &&
                    (context2.Params.Metamagic & (Metamagic)MetamagicExtender.Piercing) != 0)
                {
                    var tr = Harmony12.Traverse.Create(__instance);
                    tr.Property("SpellResistance").SetValue(Math.Max(0, __instance.SpellResistance - 5));
                }
            }
        }


        [Harmony12.HarmonyPatch(typeof(ContextDurationValue))]
        [Harmony12.HarmonyPatch("Calculate", Harmony12.MethodType.Normal)]
        static class ContextDurationValue_Calculate_Patch
        {
            internal static void Postfix(ContextDurationValue __instance, MechanicsContext context, ref Rounds __result)
            {
                if (__instance.IsExtendable && context.HasMetamagic((Metamagic)MetamagicExtender.ImprovedSpellSharing))
                {
                    __result = __result / 2;
                }
            }
        }



        [Harmony12.HarmonyPatch(typeof(BuffDescriptorImmunity))]
        [Harmony12.HarmonyPatch("IsImmune", Harmony12.MethodType.Normal)]
        static class BuffDescriptorImmunity_IsImmune_Patch
        {
            static BlueprintFeature undead_arcana = library.Get<BlueprintFeature>("1a5e7191279e7cd479b17a6ca438498c");
            internal static void Postfix(BuffDescriptorImmunity __instance, MechanicsContext context, ref bool __result)
            {
                if (__instance.IgnoreFeature == undead_arcana && context.HasMetamagic((Metamagic)MetamagicExtender.BypassUndeadMindAffectingImmunity))
                {
                    __result = false;
                }
            }
        }


        [Harmony12.HarmonyPatch(typeof(UnitPartSpellResistance.SpellImmunity))]
        [Harmony12.HarmonyPatch("CanApply", Harmony12.MethodType.Normal)]
        static class SpellImmunity_CanApply_Patch
        {
            static BlueprintFeature undead_arcana = library.Get<BlueprintFeature>("1a5e7191279e7cd479b17a6ca438498c");
            internal static void Postfix(UnitPartSpellResistance.SpellImmunity __instance, MechanicsContext context, ref bool __result)
            {
                if (__instance.CasterIgnoreImmunityFact == undead_arcana && context.HasMetamagic((Metamagic)MetamagicExtender.BypassUndeadMindAffectingImmunity)) 
                {
                    __result = false;
                }
            }
        }
    }
}
