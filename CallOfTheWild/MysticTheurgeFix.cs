﻿using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace CallOfTheWild
{
    class MysticTheurgeFix
    {
        static LibraryScriptableObject library => Main.library;
        static BlueprintCharacterClass mystic_theurge = library.Get<BlueprintCharacterClass>("0920ea7e4fd7a404282e3d8b0ac41838");
        static BlueprintProgression mystic_theurge_progression = library.Get<BlueprintProgression>("08c1075ef2786ef4fae11e82698a16e0");
        static BlueprintFeature spell_synthesis;
        static BlueprintFeature extra_spell_synthesis;
        static BlueprintFeature lesser_spell_synthesis;
        static BlueprintFeature extra_lesser_spell_synthesis;

        public static void load()
        {
            createSpellSynthesis();
            createLesserSpellSynthesis();
        }


        static void createLesserSpellSynthesis()
        {
            var icon = LoadIcons.Image2Sprite.Create(@"AbilityIcons/FontOfSpiritMagic.png");
            var combined_spells = library.Get<BlueprintFeature>("80ea00ac94323cd43b6e743f2fa168c8");
            lesser_spell_synthesis = Helpers.CreateFeature("LesserSpellSynthesisFeature",
                                            "Lesser Spell Synthesis",
                                            "Once per day as a full-round action, you can cast two spells, each from a different spellcasting class. Both spells must have a casting time of 1 standard action and must be a spell level equal to or lower than the level of spells you can prepare with the combined spells ability. You can make any decisions concerning the spells, such as the spells’ targets, independently.",
                                            "",
                                            icon,
                                            FeatureGroup.Feat,
                                            Helpers.PrerequisiteFeature(combined_spells));

            var resource = Helpers.CreateAbilityResource("LesserSpellSynthesisResource", "", "", "", null);
            resource.SetFixedResource(1);

            var arcane_buff = Helpers.CreateBuff($"LesserSpellSynthesisArcaneBuff",
                                      "",
                                      "",
                                      "",
                                      icon,
                                      null,
                                      Helpers.Create<FreeActionAbilityUseMechanics.SpellSynthesis>(s =>
                                                                                                  {
                                                                                                      s.action_type = CommandType.Standard;
                                                                                                      s.is_full_round = false;
                                                                                                      s.max_spell_level = Helpers.CreateContextValue(Kingmaker.Enums.AbilityRankType.Default);
                                                                                                      s.is_arcane = true;
                                                                                                  }
                                                                                                  ),
                                      Helpers.CreateContextRankConfig(baseValueType: Kingmaker.UnitLogic.Mechanics.Components.ContextRankBaseValueType.ClassLevel,
                                                                  classes: new BlueprintCharacterClass[] { mystic_theurge },
                                                                  progression: Kingmaker.UnitLogic.Mechanics.Components.ContextRankProgression.OnePlusDiv2
                                                                  )
                                      );
            arcane_buff.SetBuffFlags(BuffFlags.HiddenInUi);

            var divine_buff = Helpers.CreateBuff($"LesserSpellSynthesisDivineBuff",
                                  "",
                                  "",
                                  "",
                                  icon,
                                  null,
                                  Helpers.Create<FreeActionAbilityUseMechanics.SpellSynthesis>(s =>
                                                                                              {
                                                                                                  s.action_type = CommandType.Standard;
                                                                                                  s.is_full_round = false;
                                                                                                  s.max_spell_level = Helpers.CreateContextValue(Kingmaker.Enums.AbilityRankType.Default);
                                                                                                  s.is_arcane = false;
                                                                                              }
                                                                                              ),
                                  Helpers.CreateContextRankConfig(baseValueType: Kingmaker.UnitLogic.Mechanics.Components.ContextRankBaseValueType.ClassLevel,
                                                                  classes: new BlueprintCharacterClass[] {mystic_theurge},
                                                                  progression: Kingmaker.UnitLogic.Mechanics.Components.ContextRankProgression.OnePlusDiv2
                                                                  )
                                  );
            divine_buff.SetBuffFlags(BuffFlags.HiddenInUi);


            var buff = Helpers.CreateBuff("LesserSpellSynthesisBuff",
                                          lesser_spell_synthesis.Name,
                                          lesser_spell_synthesis.Description,
                                          "",
                                          icon,
                                          null
                                          );

            var ability = Helpers.CreateAbility($"LesserSpellSynthesisAbility",
                                    "Lesser Spell Synthesis",
                                    lesser_spell_synthesis.Description,
                                    "",
                                    icon,
                                    Kingmaker.UnitLogic.Abilities.Blueprints.AbilityType.Supernatural,
                                    CommandType.Standard,
                                    Kingmaker.UnitLogic.Abilities.Blueprints.AbilityRange.Personal,
                                    Helpers.oneRoundDuration,
                                    "",
                                    Helpers.CreateRunActions(Common.createContextActionApplyBuff(arcane_buff, Helpers.CreateContextDuration(1), dispellable: false),
                                                             Common.createContextActionApplyBuff(divine_buff, Helpers.CreateContextDuration(1), dispellable: false),
                                                             Common.createContextActionApplyBuff(buff, Helpers.CreateContextDuration(1), dispellable: false)
                                                             ),
                                    Helpers.CreateResourceLogic(resource)
                                    );
            ability.setMiscAbilityParametersSelfOnly();
            Common.setAsFullRoundAction(ability);

            lesser_spell_synthesis.AddComponents(Helpers.CreateAddFact(ability),
                                                Helpers.CreateAddAbilityResource(resource));


            extra_lesser_spell_synthesis = Helpers.CreateFeature("ExtraLesserSpellSynthesisFeature",
                                              "Extra Lesser Spell Synthesis",
                                              "You can perform one additional lesser spell synthesis per day.\n"
                                              + "Special: You can select this feat multiple times. Each time you take the feat, you can use this ability one additional time per day.",
                                              "",
                                              icon,
                                              FeatureGroup.Feat,
                                              Helpers.Create<ResourceMechanics.ContextIncreaseResourceAmount>(c => { c.Resource = resource; c.Value = Helpers.CreateContextValue(Kingmaker.Enums.AbilityRankType.Default); }),
                                              Helpers.PrerequisiteFeature(combined_spells),
                                              Helpers.PrerequisiteFeature(lesser_spell_synthesis)
                                              );
            extra_lesser_spell_synthesis.AddComponent(Helpers.CreateContextRankConfig(baseValueType: Kingmaker.UnitLogic.Mechanics.Components.ContextRankBaseValueType.FeatureRank,
                                                                                          feature: extra_lesser_spell_synthesis)
                                              );
            extra_lesser_spell_synthesis.ReapplyOnLevelUp = true;
            extra_lesser_spell_synthesis.Ranks = 10;

            library.AddFeats(lesser_spell_synthesis, extra_lesser_spell_synthesis);
        }


        static void createSpellSynthesis()
        {
            var icon = LoadIcons.Image2Sprite.Create(@"AbilityIcons/StormOfSouls.png");

            spell_synthesis = Helpers.CreateFeature("SpellSynthesisFeature",
                                                        "Spell Synthesis",
                                                        "At 10th level, a mystic theurge can cast two spells, one from each of his spellcasting classes, using one action. Both of the spells must have the same casting time. The mystic theurge can make any decisions concerning the spells independently.Any target affected by both of the spells takes a –2 penalty on saves made against each spell.The mystic theurge receives a + 2 bonus on caster level checks made to overcome spell resistance with these two spells. A mystic theurge may use this ability once per day.",
                                                        "",
                                                        icon,
                                                        FeatureGroup.None);

            var buff = Helpers.CreateBuff("SpellSynthesisBuff",
                                          spell_synthesis.Name,
                                          spell_synthesis.Description,
                                          "",
                                          icon,
                                          null,
                                          Helpers.Create<NewMechanics.IncreaseAllSpellsDC>(i => i.Value = 2),
                                          Helpers.Create<SpellPenetrationBonus>(s => s.Value = 2)
                                          );



            CommandType[] action_types = new CommandType[] { CommandType.Standard, CommandType.Standard, CommandType.Swift };
            bool[] full_round = new bool[] { true, false, false };
            string[] names = new string[] { "Full Round Action", "Standard Action", "Swift Action" };
            List<BlueprintAbility> abilities = new List<BlueprintAbility>();

            var resource = Helpers.CreateAbilityResource("SpellSynthesisResource", "", "", "", null);
            resource.SetFixedResource(1);

            for (int i = 0; i < action_types.Length; i++)
            {
                var arcane_buff = Helpers.CreateBuff($"SpellSynthesis{i}ArcaneBuff",
                                                      "",
                                                      "",
                                                      "",
                                                      icon,
                                                      null,
                                                      Helpers.Create<FreeActionAbilityUseMechanics.SpellSynthesis>(s => 
                                                                                                                  { s.action_type = action_types[i];
                                                                                                                    s.is_full_round = full_round[i];
                                                                                                                    s.max_spell_level = 100;
                                                                                                                    s.is_arcane = true;
                                                                                                                  }
                                                                                                                  )
                                                      );
                arcane_buff.SetBuffFlags(BuffFlags.HiddenInUi);

                var divine_buff = Helpers.CreateBuff($"SpellSynthesis{i}DivineBuff",
                                      "",
                                      "",
                                      "",
                                      icon,
                                      null,
                                      Helpers.Create<FreeActionAbilityUseMechanics.SpellSynthesis>(s =>
                                                                                                  {
                                                                                                      s.action_type = action_types[i];
                                                                                                      s.is_full_round = full_round[i];
                                                                                                      s.max_spell_level = 100;
                                                                                                      s.is_arcane = false;
                                                                                                  }
                                                                                                  )
                                      );
                divine_buff.SetBuffFlags(BuffFlags.HiddenInUi);

                var ability = Helpers.CreateAbility($"SpellSynthesis{i}Ability",
                                                    "Spell Synthesis: " + names[i],
                                                    spell_synthesis.Description,
                                                    "",
                                                    icon,
                                                    Kingmaker.UnitLogic.Abilities.Blueprints.AbilityType.Supernatural,
                                                    action_types[i],
                                                    Kingmaker.UnitLogic.Abilities.Blueprints.AbilityRange.Personal,
                                                    Helpers.oneRoundDuration,
                                                    "",
                                                    Helpers.CreateRunActions(Common.createContextActionApplyBuff(arcane_buff, Helpers.CreateContextDuration(1), dispellable: false),
                                                                             Common.createContextActionApplyBuff(divine_buff, Helpers.CreateContextDuration(1), dispellable: false),
                                                                             Common.createContextActionApplyBuff(buff, Helpers.CreateContextDuration(1), dispellable: false)
                                                                             ),
                                                    Helpers.CreateResourceLogic(resource)
                                                    );
                ability.setMiscAbilityParametersSelfOnly();
                if (full_round[i])
                {
                    Common.setAsFullRoundAction(ability);
                }

                abilities.Add(ability);
            }

            var base_ability = Common.createVariantWrapper("SpellSynthesisAbility", "", abilities.ToArray());
            base_ability.SetName(spell_synthesis.Name);

            spell_synthesis.AddComponents(Helpers.CreateAddFact(base_ability),
                                          Helpers.CreateAddAbilityResource(resource));

            mystic_theurge_progression.LevelEntries[9].Features.Add(spell_synthesis);

            extra_spell_synthesis = Helpers.CreateFeature("ExtraSpellSynthesisFeature",
                                                          "Extra Spell Synthesis",
                                                          "You can perform one additional spell synthesis per day.\n"
                                                          + "Special: You can select this feat multiple times. Each time you take the feat, you can use this ability one additional time per day.",
                                                          "",
                                                          icon,
                                                          FeatureGroup.Feat,
                                                          Helpers.Create<ResourceMechanics.ContextIncreaseResourceAmount>(c => { c.Resource = resource; c.Value = Helpers.CreateContextValue(Kingmaker.Enums.AbilityRankType.Default); }),
                                                          Helpers.PrerequisiteFeature(spell_synthesis)
                                                          );
            extra_spell_synthesis.AddComponent(Helpers.CreateContextRankConfig(baseValueType: Kingmaker.UnitLogic.Mechanics.Components.ContextRankBaseValueType.FeatureRank,
                                                                                          feature: extra_spell_synthesis)
                                              );
            extra_spell_synthesis.ReapplyOnLevelUp = true;
            extra_spell_synthesis.Ranks = 10;

            library.AddFeats(extra_spell_synthesis);
        }
    }
}
