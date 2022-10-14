﻿using BlueprintCore.Actions.Builder;
using BlueprintCore.Actions.Builder.ContextEx;
using BlueprintCore.Blueprints.Configurators.Classes.Selection;
using BlueprintCore.Blueprints.Configurators.UnitLogic.ActivatableAbilities;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Conditions.Builder;
using BlueprintCore.Utils;
using CharacterOptionsPlus.Components;
using CharacterOptionsPlus.Conditions;
using CharacterOptionsPlus.UnitParts;
using CharacterOptionsPlus.Util;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers;
using Kingmaker.Controllers.Rest.State;
using Kingmaker.Designers;
using Kingmaker.Designers.EventConditionActionSystem.ContextData;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.Recommendations;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Inspect;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TabletopTweaks.Core.NewEvents;
using static Kingmaker.Blueprints.Classes.Prerequisites.Prerequisite;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;
using static Kingmaker.Visual.Animation.Kingmaker.Actions.UnitAnimationActionCastSpell;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace CharacterOptionsPlus.Feats
{
  // TODO: Escape Artist, Stealth
  internal class SignatureSkill
  {
    internal const string FeatName = "SignatureSkill";

    internal const string FeatDisplayName = "SignatureSkill.Name";
    private const string FeatDescription = "SignatureSkill.Description";

    private const string IconPrefix = "assets/icons/";
    private const string IconName = IconPrefix + "furiousfocus.png";

    private static readonly ModLogger Logger = Logging.GetLogger(FeatName);

    internal static void Configure()
    {
      try
      {
        if (Settings.IsEnabled(Guids.SignatureSkillFeat))
          ConfigureEnabled();
        else
          ConfigureDisabled();
      }
      catch (Exception e)
      {
        Logger.LogException("SignatureSkill.Configure", e);
      }
    }

    private static void ConfigureDisabled()
    {
      Logger.Log($"Configuring {FeatName} (disabled)");

      FeatureSelectionConfigurator.New(FeatName, Guids.SignatureSkillFeat).Configure();

      BuffConfigurator.New(MobilityAbilityBuff, Guids.SignatureSkillMobilityAbilityBuff).Configure();
      ActivatableAbilityConfigurator.New(MobilityAbility, Guids.SignatureSkillMobilityAbility).Configure();
      FeatureConfigurator.New(MobilityName, Guids.SignatureSkillMobility).Configure();

      FeatureConfigurator.New(PersuasionName, Guids.SignatureSkillPersuasion).Configure();

      BuffConfigurator.New(KnowledgeBuff, Guids.SignatureSkillKnowledgeBuff).Configure();
      AbilityConfigurator.New(KnowledgeAbility, Guids.SignatureSkillKnowledgeAbility).Configure();
      FeatureConfigurator.New(KnowledgeArcanaName, Guids.SignatureSkillKnowledgeArcana).Configure();
      FeatureConfigurator.New(KnowledgeWorldName, Guids.SignatureSkillKnowledgeArcana).Configure();
      FeatureConfigurator.New(KnowledgeNatureName, Guids.SignatureSkillKnowledgeArcana).Configure();
      FeatureConfigurator.New(KnowledgeReligionName, Guids.SignatureSkillKnowledgeArcana).Configure();

      FeatureConfigurator.New(PerceptionName, Guids.SignatureSkillPerception).Configure();
    }

    private static void ConfigureEnabled()
    {
      Logger.Log($"Configuring {FeatName}");

      ConfigureKnowledgeCommon();

      var feat = FeatureSelectionConfigurator.New(FeatName, Guids.SignatureSkillFeat)
        .SetDisplayName(FeatDisplayName)
        .SetDescription(FeatDescription)
        .SetIcon(IconName)
        .AddFeatureTagsComponent(featureTags: FeatureTag.Skills)
        .AddComponent<RecommendationSignatureSkill>()
        .AddToAllFeatures(
          ConfigureAthletics(),
          ConfigureKnowledgeArcana(),
          ConfigureKnowledgeWorld(),
          ConfigureKnowledgeNature(),
          ConfigureKnowledgeReligion(),
          ConfigureMobility(),
          ConfigurePerception(),
          ConfigurePersuasion())
        .Configure();

      // Add to feat selection
      FeatureConfigurator.For(FeatName).AddToGroups(FeatureGroup.Feat).Configure(delayed: true);

      // Grant as a bonus feature for rogues
      ProgressionConfigurator.For(ProgressionRefs.RogueProgression)
        .ModifyLevelEntries(
          entry =>
          {
            if (entry.Level == 5 || entry.Level == 10 || entry.Level == 15 || entry.Level == 20)
              entry.m_Features.Add(feat.ToReference<BlueprintFeatureBaseReference>());
          })
        .Configure();
    }

    [AllowMultipleComponents]
    [AllowedOn(typeof(BlueprintFeature))]
    [TypeId("d97a84e9-bde0-4bad-ab33-89a41841e26a")]
    private class RecommendationSignatureSkill : LevelUpRecommendationComponent
    {
      private readonly StatType? Skill;

      public RecommendationSignatureSkill() { Skill = null; }
      public RecommendationSignatureSkill(StatType skill) { Skill = skill; }

      // Recommend when the character has max ranks in a relevant skill
      public override RecommendationPriority GetPriority(LevelUpState levelUpState)
      {
        if (levelUpState is null)
          return RecommendationPriority.Same;

        var unit = levelUpState.Unit;
        if (Skill is not null)
        {
          if (unit.Stats.GetStat(Skill.Value).BaseValue >= unit.Progression.CharacterLevel)
            return RecommendationPriority.Good;
          return RecommendationPriority.Same;
        }

        var skillTypes =
          new[]
          {
            StatType.SkillKnowledgeArcana,
            StatType.SkillKnowledgeWorld,
            StatType.SkillLoreNature,
            StatType.SkillLoreReligion,
            StatType.SkillMobility,
            StatType.SkillPerception,
            StatType.SkillPersuasion,
            StatType.SkillStealth,
          };
        foreach (var stat in skillTypes)
        {
          if (levelUpState.Unit.Stats.GetStat(stat).BaseValue >= unit.Progression.CharacterLevel)
            return RecommendationPriority.Good;
        }
        return RecommendationPriority.Same;
      }
    }

    #region Athletics
    private const string AthleticsName = "SignatureSkill.Athletics";
    private const string AthleticsDisplayName = "SignatureSkill.Athletics.Name";
    private const string AthleticsDescription = "SignatureSkill.Athletics.Description";

    private const string AthleticsAbility = "SignatureSkill.Athletics.Ability";
    private const string AthleticsAbilityName = "SignatureSkill.Athletics.BreakFree.Name";
    private const string AthleticsAbilityDescription = "SignatureSkill.Athletics.BreakFree.Description";

    private const string AthleticsSuppressPassive = "SignatureSkill.Athletics.Suppress.Passive";
    private const string AthleticsSuppressPassiveName = "SignatureSkill.Athletics.Suppress.Passive.Name";
    private const string AthleticsSuppressPassiveDescription = "SignatureSkill.Athletics.Suppress.Passive.Description";
    private const string AthleticsSuppressPassiveBuff = "SignatureSkill.Athletics.Suppress.Passive.Buff";
    private const string AthleticsSuppressActive = "SignatureSkill.Athletics.Suppress.Active";
    private const string AthleticsSuppressActiveName = "SignatureSkill.Athletics.Suppress.Active.Name";
    private const string AthleticsSuppressActiveDescription = "SignatureSkill.Athletics.Suppress.Active.Description";
    private const string AthleticsSuppressParalyzeBuff = "SignatureSkill.Athletics.Suppress.Paralyze.All";
    private const string AthleticsSuppressSlowBuff = "SignatureSkill.Athletics.Suppress.Slow.All";

    private static BlueprintFeature ConfigureAthletics()
    {
      BuffConfigurator.New(AthleticsSuppressParalyzeBuff, Guids.SignatureSkillAthleticsSuppressParalyzeBuff)
        .SetFlags(BlueprintBuff.Flags.HiddenInUi)
        .AddComponent(new SuppressConditions(UnitCondition.Paralyzed))
        .SetStacking(StackingType.Stack)
        .Configure();

      BuffConfigurator.New(AthleticsSuppressSlowBuff, Guids.SignatureSkillAthleticsSuppressSlowBuff)
        .SetFlags(BlueprintBuff.Flags.HiddenInUi)
        .AddComponent(new SuppressConditions(UnitCondition.Slowed))
        .SetStacking(StackingType.Stack)
        .Configure();

      var buff = BuffConfigurator.New(AthleticsSuppressPassiveBuff, Guids.SignatureSkillAthleticsSuppressPassiveBuff)
        .SetFlags(BlueprintBuff.Flags.HiddenInUi)
        .Configure();

      var passiveSuppress = ActivatableAbilityConfigurator.New(AthleticsSuppressPassive, Guids.SignatureSkillAthleticsSuppressPassive)
        .SetDisplayName(AthleticsSuppressPassiveName)
        .SetDescription(AthleticsSuppressPassiveDescription)
        .SetDeactivateImmediately()
        .SetDoNotTurnOffOnRest()
        .SetIsOnByDefault()
        .SetBuff(buff)
        .Configure();

      var activeSuppress = AbilityConfigurator.New(AthleticsSuppressActive, Guids.SignatureskillAthleticsSuppressActive)
        .SetDisplayName(AthleticsSuppressActiveName)
        .SetDescription(AthleticsSuppressActiveDescription)
        .SetRange(AbilityRange.Personal)
        .SetType(AbilityType.Extraordinary)
        .AllowTargeting(self: true)
        .SetActionType(CommandType.Standard)
        .SetAnimation(CastAnimationStyle.Omni)
        .AddComponent<SuppressSlowAbilityRequirements>()
        .AddAbilityEffectRunAction(ActionsBuilder.New().Add<SuppressSlow>())
        .Configure();

      var ability = AbilityConfigurator.New(AthleticsAbility, Guids.SignatureSkillAthleticsAbility)
        .SetDisplayName(AthleticsAbilityName)
        .SetDescription(AthleticsAbilityDescription)
        .SetRange(AbilityRange.Personal)
        .SetType(AbilityType.Extraordinary)
        .AllowTargeting(self: true)
        .SetActionType(CommandType.Standard)
        .SetAnimation(CastAnimationStyle.Omni)
        .AddComponent<BreakFreeAbilityRequirements>()
        .AddAbilityEffectRunAction(ActionsBuilder.New().Add<BreakFree>())
        .Configure();

      return FeatureConfigurator.New(AthleticsName, Guids.SignatureSkillAthletics)
        .SetDisplayName(AthleticsDisplayName)
        .SetDescription(AthleticsDescription)
        .SetIsClassFeature()
        .AddPrerequisiteStatValue(StatType.SkillAthletics, value: 5, group: GroupType.Any)
        .AddPrerequisiteClassLevel(CharacterClassRefs.RogueClass.ToString(), level: 5, group: GroupType.Any)
        .AddComponent(new RecommendationSignatureSkill(StatType.SkillAthletics))
        .AddComponent<SignatureAthleticsComponent>()
        .AddFacts(new() { ability })
        .AddComponent(
          new AddFactsOnSkillRank(
            StatType.SkillAthletics,
            (passiveSuppress.ToReference<BlueprintUnitFactReference>(), rank: 15),
            (activeSuppress.ToReference<BlueprintUnitFactReference>(), rank: 15)))
        .Configure();
    }

    [TypeId("567d5b5e-65b7-423d-81ef-86a2229a4dab")]
    private class SuppressSlow : ContextAction
    {
      public override string GetCaption()
      {
        return "Suppress slow";
      }

      public override void RunAction()
      {
        try
        {
          var target = Context.MaybeCaster;
          if (target is null)
          {
            Logger.Warning("Suppress slow missing caster");
            return;
          }

          var escapeArtist = target.Get<UnitPartEscapeArtist>();
          if (escapeArtist?.SuppressTarget is null)
          {
            Logger.Warning("Nothing to suppress");
            return;
          }

          escapeArtist.TrySuppress(spendAction: false);
        }
        catch (Exception e)
        {
          Logger.LogException("SuppressSlow.RunAction", e);
        }
      }
    }

    [TypeId("ceaf8034-d3cb-46a5-b452-7eb6713d27a4")]
    private class SuppressSlowAbilityRequirements : BlueprintComponent, IAbilityCasterRestriction
    {
      private const string Restriction = "SignatureSkill.Athletics.Suppress.Restriction";

      public string GetAbilityCasterRestrictionUIText()
      {
        return LocalizationTool.GetString(Restriction);
      }

      public bool IsCasterRestrictionPassed(UnitEntityData caster)
      {
        try
        {
          var escapeArtist = caster.Get<UnitPartEscapeArtist>();
          return escapeArtist?.SuppressTarget is not null;
        }
        catch (Exception e)
        {
          Logger.LogException("SuppressSlowAbilityRequirements.IsCasterRestrictionPassed", e);
        }
        return false;
      }
    }

    [TypeId("ef1e6886-95d1-4e56-8081-0573378ef701")]
    private class BreakFree : ContextAction
    {
      public override string GetCaption()
      {
        return "Break Free";
      }

      public override void RunAction()
      {
        try
        {
          var target = Context.MaybeCaster;
          if (target is null)
          {
            Logger.Warning("Break free missing caster");
            return;
          }

          var buff = target?.Get<UnitPartEscapeArtist>()?.BreakFreeBuffs.FirstOrDefault();
          if (buff is not null)
          {
            using (ContextData<FactData>.Request().Setup(buff))
            {
              foreach (var action in
                  buff.Blueprint.ElementsArray.Where(e => e is ContextActionBreakFree).Cast<ContextActionBreakFree>())
              {
                Logger.NativeLog($"Breaking free from: {buff.Name} for {target.CharacterName}");
                buff.RunActionInContext(new() { Actions = new GameAction[] { action } });
                return;
              }
            }
          }

          var targetGrapple = target?.Get<UnitPartGrappleTarget>();
          if (targetGrapple is not null)
          {
            var initiator = targetGrapple.Initiator.Value;
            var initiatorGraple = targetGrapple.Initiator.Value;
            if (initiatorGraple is null)
            {
              Logger.Warning($"No grapple initiator for {target.CharacterName}");
              return;
            }

            Logger.NativeLog($"Breaking free from: {targetGrapple.m_Buff?.Name} for {target.CharacterName}");
            if (target.TryBreakFree(initiator, UnitHelper.BreakFreeFlags.Default, targetGrapple.Context))
              target.Remove<UnitPartGrappleTarget>();
            return;
          }

          Logger.Warning($"{target.CharacterName} has nothing to break free from");
        }
        catch (Exception e)
        {
          Logger.LogException("BreakFree.RunAction", e);
        }
      }
    }

    [TypeId("62f99d41-f2c2-4e6f-9aad-b84094db9cd2")]
    private class BreakFreeAbilityRequirements : BlueprintComponent, IAbilityCasterRestriction
    {
      private const string Restriction = "SignatureSkill.Athletics.BreakFree.Restriction";

      public string GetAbilityCasterRestrictionUIText()
      {
        return LocalizationTool.GetString(Restriction);
      }

      public bool IsCasterRestrictionPassed(UnitEntityData caster)
      {
        try
        {
          var grapple = caster.Get<UnitPartGrappleTarget>();
          return caster.Get<UnitPartEscapeArtist>()?.BreakFreeBuffs?.Any() == true
            || (grapple is not null && grapple.Initiator.Value is not null);
        }
        catch (Exception e)
        {
          Logger.LogException("BreakFreeabilityRequirements.IsCasterRestrictionPassed", e);
        }
        return false;
      }
    }

    [TypeId("885b1244-2816-47a7-a4ad-4bd48fd75695")]
    private class SignatureAthleticsComponent :
      UnitFactComponentDelegate,
      IUnitBuffHandler
    {
      public void HandleBuffDidAdded(Buff buff)
      {
        try
        {
          if (buff.Owner != Owner)
            return;

          if (IsBreakFreeBuff(buff))
          {
            Logger.NativeLog($"Adding {buff.Name} to BreakFreeBuffs for {Owner.CharacterName}");
            Owner.Ensure<UnitPartEscapeArtist>().BreakFreeBuffs.Add(buff);
            return;
          }

          if (buff.Context.Params is null || buff.Context.Params.DC <= 0)
            return;

          var appliesSlow = UnitPartEscapeArtist.AppliesCondition(buff, UnitCondition.Slowed);
          var appliesParalyze = UnitPartEscapeArtist.AppliesCondition(buff, UnitCondition.Paralyzed);
          if (appliesParalyze || appliesSlow)
          {
            Logger.NativeLog($"Adding {buff.Name} to SuppressBuffs for {Owner.CharacterName}");
            Owner.Ensure<UnitPartEscapeArtist>().AddSupressBuff(buff, appliesParalyze, appliesSlow);
          }
        }
        catch (Exception e)
        {
          Logger.LogException("SignatureAthleticsComponent.HandleBuffDidAdded", e);
        }
      }

      private static bool IsBreakFreeBuff(Buff buff)
      {
        if (buff.Blueprint.ElementsArray.Any(element => element is ContextActionBreakFree))
          return true;

        if (buff.SourceAbility?.ElementsArray?.Any(element => element is ContextActionGrapple) == true)
          return true;

        return false;
      }

      public void HandleBuffDidRemoved(Buff buff)
      {
        try
        {
          if (buff.Owner != Owner)
            return;

          var unitPart = Owner.Get<UnitPartEscapeArtist>();
          if (unitPart is null)
            return;

          if (unitPart.BreakFreeBuffs.Remove(buff))
            return;

          unitPart.RemoveSuppressBuff(buff);
        }
        catch (Exception e)
        {
          Logger.LogException("SignatureAthleticsComponent.HandleBuffDidRemoved", e);
        }
      }
    }
    #endregion

    #region Knowledge / Lore

    #region Blueprint Links
    private static BlueprintFeature _arcana;
    private static BlueprintFeature Arcana
    {
      get
      {
        _arcana ??= BlueprintTool.Get<BlueprintFeature>(Guids.SignatureSkillKnowledgeArcana);
        return _arcana;
      }
    }

    private static BlueprintFeature _World;
    private static BlueprintFeature World
    {
      get
      {
        _World ??= BlueprintTool.Get<BlueprintFeature>(Guids.SignatureSkillKnowledgeWorld);
        return _World;
      }
    }

    private static BlueprintFeature _Nature;
    private static BlueprintFeature Nature
    {
      get
      {
        _Nature ??= BlueprintTool.Get<BlueprintFeature>(Guids.SignatureSkillLoreNature);
        return _Nature;
      }
    }

    private static BlueprintFeature _Religion;
    private static BlueprintFeature Religion
    {
      get
      {
        _Religion ??= BlueprintTool.Get<BlueprintFeature>(Guids.SignatureSkillLoreReligion);
        return _Religion;
      }
    }

    private static BlueprintBuff _buff;
    private static BlueprintBuff Buff
    {
      get
      {
        _buff ??= BlueprintTool.Get<BlueprintBuff>(Guids.SignatureSkillKnowledgeBuff);
        return _buff;
      }
    }

    private static BlueprintAbility _knowledgeAbilityBlueprint;
    private static BlueprintAbility KnowledgeAbilityBlueprint
    {
      get
      {
        _knowledgeAbilityBlueprint ??= BlueprintTool.Get<BlueprintAbility>(Guids.SignatureSkillKnowledgeAbility);
        return _knowledgeAbilityBlueprint;
      }
    }
    #endregion

    private const string KnowledgeDescription = "SignatureSkill.Knowledge.Description";

    private const string KnowledgeAbility = "SignatureSkill.Knowledge.Ability.Name";
    private const string KnowledgeAbilityDescription = "SignatureSkill.Knowledge.Ability.Description";

    private const string KnowledgeBuff = "SignatureSkill.Knowledge.Arcana.Buff";

    private static void ConfigureKnowledgeCommon()
    {
      BuffConfigurator.New(KnowledgeBuff, Guids.SignatureSkillKnowledgeBuff)
        .SetDisplayName(KnowledgeAbility)
        .SetDescription(KnowledgeAbilityDescription)
        .Configure();

      AbilityConfigurator.New(KnowledgeAbility, Guids.SignatureSkillKnowledgeAbility)
        .SetDisplayName(KnowledgeArcanaDisplayName)
        .SetDescription(KnowledgeAbilityDescription)
        .SetRange(AbilityRange.Long)
        .SetType(AbilityType.Extraordinary)
        .AllowTargeting(enemies: true)
        .SetActionType(CommandType.Move)
        .SetAnimation(CastAnimationStyle.Omni)
        .AddComponent<SignatureKnowledgeAbilityRequirements>()
        .AddAbilityEffectRunAction(
          ActionsBuilder.New().MakeKnowledgeCheck(successActions: ActionsBuilder.New().Add<ApplySignatureSkillBuff>()))
        .Configure();
    }

    #region Arcana
    private const string KnowledgeArcanaName = "SignatureSkill.KnowledgeArcana";
    private const string KnowledgeArcanaDisplayName = "SignatureSkill.KnowledgeArcana.Name";

    private static BlueprintFeature ConfigureKnowledgeArcana()
    {
      return FeatureConfigurator.New(KnowledgeArcanaName, Guids.SignatureSkillKnowledgeArcana)
        .SetDisplayName(KnowledgeArcanaDisplayName)
        .SetDescription(KnowledgeDescription)
        .SetIsClassFeature()
        .AddPrerequisiteStatValue(StatType.SkillKnowledgeArcana, value: 5, group: GroupType.Any)
        .AddPrerequisiteClassLevel(CharacterClassRefs.RogueClass.ToString(), level: 5, group: GroupType.Any)
        .AddComponent(new RecommendationSignatureSkill(StatType.SkillKnowledgeArcana))
        .AddComponent(new SignatureKnowledgeComponent(StatType.SkillKnowledgeArcana))
        .AddComponent(
          new AddFactsOnSkillRank(
            StatType.SkillKnowledgeArcana,
            (KnowledgeAbilityBlueprint.ToReference<BlueprintUnitFactReference>(), 10)))
        .Configure();
    }
    #endregion

    #region World
    private const string KnowledgeWorldName = "SignatureSkill.KnowledgeWorld";
    private const string KnowledgeWorldDisplayName = "SignatureSkill.KnowledgeWorld.Name";

    private static BlueprintFeature ConfigureKnowledgeWorld()
    {
      return FeatureConfigurator.New(KnowledgeWorldName, Guids.SignatureSkillKnowledgeWorld)
        .SetDisplayName(KnowledgeWorldDisplayName)
        .SetDescription(KnowledgeDescription)
        .SetIsClassFeature()
        .AddPrerequisiteStatValue(StatType.SkillKnowledgeWorld, value: 5, group: GroupType.Any)
        .AddPrerequisiteClassLevel(CharacterClassRefs.RogueClass.ToString(), level: 5, group: GroupType.Any)
        .AddComponent(new RecommendationSignatureSkill(StatType.SkillKnowledgeWorld))
        .AddComponent(new SignatureKnowledgeComponent(StatType.SkillKnowledgeWorld))
        .AddComponent(
          new AddFactsOnSkillRank(
            StatType.SkillKnowledgeArcana,
            (KnowledgeAbilityBlueprint.ToReference<BlueprintUnitFactReference>(), 10)))
        .Configure();
    }
    #endregion

    #region Nature
    private const string KnowledgeNatureName = "SignatureSkill.LoreNature";
    private const string KnowledgeNatureDisplayName = "SignatureSkill.LoreNature.Name";

    private static BlueprintFeature ConfigureKnowledgeNature()
    {
      return FeatureConfigurator.New(KnowledgeNatureName, Guids.SignatureSkillLoreNature)
        .SetDisplayName(KnowledgeNatureDisplayName)
        .SetDescription(KnowledgeDescription)
        .SetIsClassFeature()
        .AddPrerequisiteStatValue(StatType.SkillLoreNature, value: 5, group: GroupType.Any)
        .AddPrerequisiteClassLevel(CharacterClassRefs.RogueClass.ToString(), level: 5, group: GroupType.Any)
        .AddComponent(new RecommendationSignatureSkill(StatType.SkillLoreNature))
        .AddComponent(new SignatureKnowledgeComponent(StatType.SkillLoreNature))
        .AddComponent(
          new AddFactsOnSkillRank(
            StatType.SkillKnowledgeArcana,
            (KnowledgeAbilityBlueprint.ToReference<BlueprintUnitFactReference>(), 10)))
        .Configure();
    }
    #endregion

    #region Religion
    private const string KnowledgeReligionName = "SignatureSkill.LoreReligion";
    private const string KnowledgeReligionDisplayName = "SignatureSkill.LoreReligion.Name";

    private static BlueprintFeature ConfigureKnowledgeReligion()
    {
      return FeatureConfigurator.New(KnowledgeReligionName, Guids.SignatureSkillLoreReligion)
        .SetDisplayName(KnowledgeReligionDisplayName)
        .SetDescription(KnowledgeDescription)
        .SetIsClassFeature()
        .AddPrerequisiteStatValue(StatType.SkillLoreReligion, value: 5, group: GroupType.Any)
        .AddPrerequisiteClassLevel(CharacterClassRefs.RogueClass.ToString(), level: 5, group: GroupType.Any)
        .AddComponent(new RecommendationSignatureSkill(StatType.SkillLoreReligion))
        .AddComponent(new SignatureKnowledgeComponent(StatType.SkillLoreReligion))
        .AddComponent(
          new AddFactsOnSkillRank(
            StatType.SkillKnowledgeArcana,
            (KnowledgeAbilityBlueprint.ToReference<BlueprintUnitFactReference>(), 10)))
        .Configure();
    }
    #endregion

    [TypeId("769750b3-8f3b-42c5-8cbd-f6aa9910ab62")]
    private class SignatureKnowledgeComponent :
      UnitFactComponentDelegate,
      IUnitIdentifiedHandler,
      IInitiatorRulebookHandler<RuleRollD20>,
      IInitiatorRulebookHandler<RuleSavingThrow>,
      IInitiatorRulebookHandler<RuleCalculateAttackBonus>,
      IInitiatorRulebookHandler<RuleSpellResistanceCheck>
    {
      private readonly StatType Skill;

      public SignatureKnowledgeComponent(StatType skill)
      {
        Skill = skill;
      }

      #region Reroll
      public void OnEventAboutToTrigger(RuleRollD20 evt)
      {
        try
        {
          if (Rulebook.CurrentContext.PreviousEvent is not RuleSkillCheck skillCheck)
            return;

          if (skillCheck.StatType != Skill)
            return;

          var skillRanks = Owner.Stats.GetStat(Skill).BaseValue;
          if (skillRanks < 20)
            return;

          Logger.Log($"Adding reroll for {Owner.CharacterName}.");
          evt.AddReroll(amount: 1, takeBest: true, Fact);
        }
        catch (Exception e)
        {
          Logger.LogException("SignatureInspectionComponent.OnEventAboutToTrigger(RuleRollD20)", e);
        }
      }

      #endregion

      #region Competence Bonus
      public void OnEventAboutToTrigger(RuleSavingThrow evt)
      {
        try
        {
          var buff = evt.Reason.Caster.GetFact(Buff);
          if (buff is null || buff.MaybeContext?.MaybeCaster != evt.Initiator)
            return;

          var bonus = GetBonus(evt.Initiator);
          Logger.NativeLog($"Adding +{bonus} to saving throw for {evt.Initiator.CharacterName}");
          evt.AddModifier(bonus, Fact, ModifierDescriptor.Competence);
        }
        catch (Exception e)
        {
          Logger.LogException("SignatureInspectionComponent.OnEventAboutToTrigger(RuleSavingThrow)", e);
        }
      }

      public void OnEventAboutToTrigger(RuleCalculateAttackBonus evt)
      {
        try
        {
          if (evt.Target is null)
          {
            Logger.Warning("No target available for attack.");
            return;
          }

          var buff = evt.Target.GetFact(Buff);
          if (buff is null || buff.MaybeContext?.MaybeCaster != evt.Initiator)
            return;

          var bonus = GetBonus(evt.Initiator);
          Logger.NativeLog($"Adding +{bonus} to attack against {evt.Target.CharacterName}");
          evt.AddModifier(bonus, Fact, ModifierDescriptor.Competence);
        }
        catch (Exception e)
        {
          Logger.LogException("SignatureKnowledgeBuffComponent.OnEventAboutToTrigger(RuleCalculateAttackBonus)", e);
        }
      }

      public void OnEventAboutToTrigger(RuleSpellResistanceCheck evt)
      {
        try
        {
          if (evt.Target is null)
          {
            Logger.Warning("No target available for attack.");
            return;
          }

          var buff = evt.Target.GetFact(Buff);
          if (buff is null || buff.MaybeContext?.MaybeCaster != evt.Initiator)
            return;

          var bonus = GetBonus(evt.Initiator);
          Logger.NativeLog($"Adding +{bonus} to spell resistance check against {evt.Target.CharacterName}");
          evt.AddSpellPenetration(bonus, ModifierDescriptor.Competence);
        }
        catch (Exception e)
        {
          Logger.LogException("SignatureKnowledgeBuffComponent.OnEventAboutToTrigger(RuleSpellResistanceCheck)", e);
        }
      }

      private int GetBonus(UnitEntityData unit)
      {
        return Math.Max(3, (unit.Stats.GetStat(Skill).BaseValue - 5) / 5);
      }
      #endregion

      public void OnUnitIdentified(RuleSkillCheck skillCheck, ref int checkBonus)
      {
        try
        {
          if (skillCheck.StatType != Skill)
            return;

          checkBonus += Owner.Stats.GetStat(Skill).BaseValue;
          Logger.NativeLog($"Adding +{checkBonus} to identify success for {Owner.CharacterName}");
        }
        catch (Exception e)
        {
          Logger.LogException("SignatureInspectionComponent.OnUnitIdentified", e);
        }
      }

      #region Unused
      public void OnEventDidTrigger(RuleCalculateAttackBonus evt) { }

      public void OnEventDidTrigger(RuleSpellResistanceCheck evt) { }

      public void OnEventDidTrigger(RuleSavingThrow evt) { }

      public void OnEventDidTrigger(RuleRollD20 evt) { }
      #endregion
    }

    [TypeId("3addba51-c42a-476c-9847-d547b29757cc")]
    private class ApplySignatureSkillBuff : ContextAction
    {
      public override string GetCaption()
      {
        return "Apply Signature Skill buff";
      }

      public override void RunAction()
      {
        try
        {
          List<UnitEntityData> targets = new() { Context.MainTarget.Unit };

          var targetType = Context.MainTarget.Unit.Blueprint.Type;
          targets.AddRange(
            GameHelper.GetTargetsAround(Context.MaybeCaster.Position, 120.Feet())
              .Where(unit => unit.IsEnemy(Context.MaybeCaster) && unit.Blueprint.Type == targetType));

          foreach (var target in targets)
          {
            var buff = target.AddBuff(Buff, Context, duration: 1.Minutes());
            buff.IsNotDispelable = true;
          }
        }
        catch (Exception e)
        {
          Logger.LogException("ApplySignatureSkillBuff.RunAction", e);
        }
      }
    }

    [TypeId("77039b8b-eb62-407d-b959-c301905d3882")]
    private class SignatureKnowledgeAbilityRequirements : BlueprintComponent, IAbilityTargetRestriction
    {
      private const string MissingFeat = "SignatureSkill.Knowledge.Ability.TargetRestriction.Feat";
      private const string MissingRanks = "SignatureSkill.Knowledge.Ability.TargetRestriction.Ranks";

      public string GetAbilityTargetRestrictionUIText(UnitEntityData caster, TargetWrapper target)
      {
        if (target.Unit is null)
          return string.Empty;

        var requiredFeat = GetRequiredFeat(target.Unit);
        if (caster.HasFact(requiredFeat))
        {
          return string.Format(
            LocalizationTool.GetString(MissingRanks), LocalizedTexts.Instance.Stats.GetText(GetStatType(target.Unit)));
        }
        return string.Format(LocalizationTool.GetString(MissingFeat).ToString(), requiredFeat.Name);
      }

      public bool IsTargetRestrictionPassed(UnitEntityData caster, TargetWrapper target)
      {
        try
        {
          if (target.Unit is null)
            return false;

          if (caster.Stats.GetStat(GetStatType(target.Unit)).BaseValue < 10)
            return False;

          return caster.HasFact(GetRequiredFeat(target.Unit));
        }
        catch (Exception e)
        {
          Logger.LogException("SignatureSkillAbilityRequirements.IsTargetRestrictionPassed", e);
        }
        return false;
      }

      private static StatType GetStatType(UnitEntityData target)
      {
        return target.Blueprint.Type ? target.Blueprint.Type.KnowledgeStat : StatType.SkillLoreNature;
      }

      private static BlueprintFeature GetRequiredFeat(UnitEntityData target)
      {
        return
          GetStatType(target) switch
          {
            StatType.SkillLoreNature => Nature,
            StatType.SkillLoreReligion => Religion,
            StatType.SkillKnowledgeWorld => World,
            StatType.SkillKnowledgeArcana => Arcana,
            _ => throw new NotImplementedException()
          };
      }
    }

    public interface IUnitIdentifiedHandler : IUnitSubscriber
    {
      void OnUnitIdentified(RuleSkillCheck skillCheck, ref int checkBonus);
    }

    [HarmonyPatch(typeof(InspectUnitsManager))]
    static class InspectUnitsManager_Patch
    {
      private static int OnUnitIdentified(RuleSkillCheck skillCheck, UnitEntityData identifier)
      {
        int bonus = 0;
        EventBus.RaiseEvent<IUnitIdentifiedHandler>(identifier, h => h.OnUnitIdentified(skillCheck, ref bonus));
        return skillCheck.RollResult + bonus;
      }

      static readonly MethodInfo UnitInfo_SetCheck =
        AccessTools.Method(typeof(InspectUnitsManager.UnitInfo), nameof(InspectUnitsManager.UnitInfo.SetCheck));
      static readonly MethodInfo UnitEntityData_Descriptor =
        AccessTools.PropertyGetter(typeof(UnitEntityData), nameof(UnitEntityData.Descriptor));

      [HarmonyPatch(nameof(InspectUnitsManager.TryMakeKnowledgeCheck), new Type[] { typeof(UnitEntityData) }), HarmonyTranspiler]
      static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
      {
        // Patch should be doing:
        //   ldloc.s ruleSkillCheck   [Existing]
        //   ldloc.s unitEntityData   [New]
        //   call OnUnitIdentified    [New]
        //   callvirt SetCheck(int32) [Existing]
        // Note that this removes the call to RollResult which normally follows load for ruleSkillCheck
        try
        {
          var code = new List<CodeInstruction>(instructions);

          // Search backwards for the SetCheck() instruction which is the insertion point.
          var index = code.Count - 1;
          var insertionIndex = 0;
          for (; index >= 0; index--)
          {
            if (code[index].Calls(UnitInfo_SetCheck))
            {
              insertionIndex = index;
              break;
            }
          }
          if (insertionIndex == 0)
          {
            throw new InvalidOperationException("Missing inspect units manager transpiler insertion index.");
          }

          // Keep searching backwards to find the load statement for unitEntityData
          CodeInstruction loadInitiator = null;
          index--;
          for (; index >= 0; index--)
          {
            if (code[index].Calls(UnitEntityData_Descriptor))
            {
              // Statement before Descriptor must load the skill check
              loadInitiator = code[index - 1].Clone();
              break;
            }
          }
          if (loadInitiator is null)
          {
            throw new InvalidOperationException("Missing unit entity data load instruction.");
          }

          var newCode =
            new List<CodeInstruction>()
            {
              loadInitiator,
              CodeInstruction.Call(typeof(InspectUnitsManager_Patch), nameof(InspectUnitsManager_Patch.OnUnitIdentified)),
            };
          code.InsertRange(insertionIndex, newCode);
          code.RemoveAt(insertionIndex - 1); // Remove the call to ruleSkillCheck.RollResult
          return code;
        }
        catch (Exception e)
        {
          Logger.LogException("InspectUnitsManager_Patch.Transpiler", e);
          return instructions;
        }
      }
    }
    #endregion

    // TODO: Merge the two mobility thingies using Ability Variants
    // Probably that requires replacing the original, including save fixing
    #region Mobility

    private static BlueprintFeature _getUp;
    private static BlueprintFeature GetUp
    {
      get
      {
        _getUp ??= FeatureRefs.AcrobatsFootwearFeature.Reference.Get();
        return _getUp;
      }
    }

    private const string MobilityName = "SignatureSkill.Mobility";
    private const string MobilityDisplayName = "SignatureSkill.Mobility.Name";
    private const string MobilityDescription = "SignatureSkill.Mobility.Description";

    private const string MobilityAbility = "SignatureSkill.Mobility.Ability";
    private const string MobilityAbilityBuff = "SignatureSkill.Mobility.Ability.Buff";
    private const string MobilityAbilityDescription = "SignatureSkill.Mobility.Ability.Description";

    private static BlueprintFeature ConfigureMobility()
    {
      var buff = BuffConfigurator.New(MobilityAbilityBuff, Guids.SignatureSkillMobilityAbilityBuff)
        .SetDisplayName(MobilityDisplayName)
        .SetDescription(MobilityAbilityDescription)
        .SetIcon(ActivatableAbilityRefs.MobilityUseAbility.Reference.Get().Icon) // TODO: Replace
        .AddComponent<AcrobaticMovement>()
        .Configure();

      var ability = ActivatableAbilityConfigurator.New(MobilityAbility, Guids.SignatureSkillMobilityAbility)
        .SetDisplayName(MobilityDisplayName)
        .SetDescription(MobilityAbilityDescription)
        .SetIcon(ActivatableAbilityRefs.MobilityUseAbility.Reference.Get().Icon) // TODO: Replace
        .SetDeactivateIfCombatEnded()
        .SetDeactivateImmediately()
        .SetActivationType(AbilityActivationType.WithUnitCommand)
        .SetBuff(buff)
        .Configure();

      return FeatureConfigurator.New(MobilityName, Guids.SignatureSkillMobility)
        .SetDisplayName(MobilityDisplayName)
        .SetDescription(MobilityDescription)
        .SetIsClassFeature()
        .AddPrerequisiteStatValue(StatType.SkillMobility, value: 5, group: GroupType.Any)
        .AddPrerequisiteClassLevel(CharacterClassRefs.RogueClass.ToString(), level: 5, group: GroupType.Any)
        .AddComponent<SignatureMobilityComponent>()
        .AddComponent(
          new AddFactsOnSkillRank(
            StatType.SkillMobility, (GetUp.ToReference<BlueprintUnitFactReference>(), 15)))
        .AddFacts(new() { ability })
        .Configure();
    }

    private class SignatureMobilityComponent :
      UnitFactComponentDelegate,
      IInitiatorRulebookHandler<RuleCalculateCMD>,
      ITargetRulebookHandler<RuleCalculateCMD>,
      IInitiatorRulebookHandler<RuleSavingThrow>
    {
      private static BlueprintBuff _mobilityBuff;
      private static BlueprintBuff MobilityBuff
      {
        get
        {
          _mobilityBuff ??= BlueprintTool.Get<BlueprintBuff>(Guids.SignatureSkillMobilityAbilityBuff);
          return _mobilityBuff;
        }
      }

      public void OnEventAboutToTrigger(RuleCalculateCMD evt)
      {
        try
        {
          if (evt.Initiator == Owner)
            CheckForPenalty(evt);
          else if (evt.Target == Owner)
            CheckForBonus(evt);
        }
        catch (Exception e)
        {
          Logger.LogException("SignatureMobilityComponent.OnEventAboutToTrigger(RuleCalculateCMD)", e);
        }
      }

      private void CheckForPenalty(RuleCalculateCMD evt)
      {
        // None is used only for Mobility / AOO avoidance
        if (evt.Type != CombatManeuver.None)
          return;

        if (!evt.Initiator.HasFact(MobilityBuff))
          return;

        Logger.NativeLog($"Adding +5 to {evt.Target.CharacterName} CMD");
        evt.AddModifier(5, Fact);
      }

      private void CheckForBonus(RuleCalculateCMD evt)
      {
        if (evt.Type != CombatManeuver.Trip)
          return;

        var ranks = Owner.Stats.GetStat(StatType.SkillMobility).BaseValue;
        if (ranks < 10)
          return;

        var bonus = ranks >= 20 ? 4 : 2;
        Logger.NativeLog($"Adding (+{bonus}) to {evt.Target.CharacterName} CMD");
        evt.AddModifier(bonus, Fact);
      }

      public void OnEventAboutToTrigger(RuleSavingThrow evt)
      {
        try
        {
          if (evt.Type != SavingThrowType.Reflex)
            return;

          var inPit = Owner.Get<UnitPartInPit>();
          if (inPit is null || inPit.State != UnitInPitState.ReadyToEvade)
            return;

          var ranks = Owner.Stats.GetStat(StatType.SkillMobility).BaseValue;
          if (ranks < 10)
            return;

          var bonus = ranks >= 20 ? 4 : 2;
          Logger.NativeLog($"Adding (+{bonus}) to {Owner.CharacterName} Reflex");
          evt.AddModifier(bonus, Fact);
        }
        catch (Exception e)
        {
          Logger.LogException("SignatureMobilityComponent.OnEventAboutToTrigger(RuleSavingThrow)", e);
        }
      }

      public void OnEventDidTrigger(RuleCalculateCMD evt) { }

      public void OnEventDidTrigger(RuleSavingThrow evt) { }
    }

    [HarmonyPatch(typeof(UnitEntityData))]
    static class UnitEntityData_Patch
    {
      private static BlueprintBuff _Mobility;
      private static BlueprintBuff Mobility
      {
        get
        {
          _Mobility ??= BlueprintTool.Get<BlueprintBuff>(Guids.SignatureSkillMobilityAbilityBuff);
          return _Mobility;
        }
      }

      [HarmonyPatch(nameof(UnitEntityData.CalculateSpeedModifier)), HarmonyPostfix]
      static void CalculateSpeedModifier(UnitEntityData __instance, ref float __result)
      {
        try
        {
          if (!__instance.Descriptor.State.HasCondition(UnitCondition.UseMobilityToNegateAttackOfOpportunity))
            return;

          if (!__instance.HasFact(Mobility) || __instance.Descriptor.State.Features.TricksterMobilityFastMovement)
            return;

          __result *= 2f;
        }
        catch (Exception e)
        {
          Logger.LogException("UnitEntityData_Patch.CalculateSpeedModifier", e);
        }
      }
    }
    #endregion

    #region Perception
    private const string PerceptionName = "SignatureSkill.Perception";
    private const string PerceptionDisplayName = "SignatureSkill.Perception.Name";
    private const string PerceptionDescription = "SignatureSkill.Perception.Description";

    private static BlueprintFeature ConfigurePerception()
    {
      return FeatureConfigurator.New(PerceptionName, Guids.SignatureSkillPerception)
        .SetDisplayName(PerceptionDisplayName)
        .SetDescription(PerceptionDescription)
        .SetIsClassFeature()
        .AddPrerequisiteStatValue(StatType.SkillPerception, value: 5, group: GroupType.Any)
        .AddPrerequisiteClassLevel(CharacterClassRefs.RogueClass.ToString(), level: 5, group: GroupType.Any)
        .AddComponent(new RecommendationSignatureSkill(StatType.SkillPerception))
        .AddComponent<SignaturePerceptionComponent>()
        .Configure();
    }

    [TypeId("e48b6792-f51c-465a-87d8-5903ac170bfb")]
    private class SignaturePerceptionComponent :
      UnitFactComponentDelegate,
      IInitiatorRulebookHandler<RuleSkillCheck>,
      IInitiatorRulebookHandler<RuleCachedPerceptionCheck>
    {
      public void OnEventAboutToTrigger(RuleSkillCheck evt)
      {
        try
        {
          if (evt.StatType != StatType.SkillPerception)
            return;

          if (evt.Reason.SourceEntity is null || evt.Reason.SourceEntity is not StaticEntityData)
            return;

          var perceptionRanks = Owner.Stats.GetStat(StatType.SkillPerception).BaseValue;
          if (perceptionRanks < 10)
            return;

          var bonus = perceptionRanks >= 20 ? 10 : 5;
          Logger.NativeLog($"Adding (+{bonus}) to {Owner.CharacterName} (hidden object)");
          evt.AddModifier(bonus, Fact);
        }
        catch (Exception e)
        {
          Logger.LogException("SignaturePerceptionComponent.OnEventAboutToTrigger(RuleSkillCheck)", e);
        }
      }

      // RuleCachedPerceptionCheck is only used for hidden units
      public void OnEventAboutToTrigger(RuleCachedPerceptionCheck evt)
      {
        try
        {
          var perceptionRanks = Owner.Stats.GetStat(StatType.SkillPerception).BaseValue;
          if (perceptionRanks < 10)
            return;

          var bonus = perceptionRanks >= 20 ? 10 : 5;
          Logger.NativeLog($"Adding (+{bonus}) to {Owner.CharacterName} (hidden unit)");
          evt.AddModifier(bonus, Fact);
        }
        catch (Exception e)
        {
          Logger.LogException("SignaturePerceptionComponent.OnEventAboutToTrigger(RuleCachedPerceptionCheck)", e);
        }
      }

      public void OnEventDidTrigger(RuleSkillCheck evt) { }

      public void OnEventDidTrigger(RuleCachedPerceptionCheck evt) { }
    }

    [HarmonyPatch(typeof(CampingRole))]
    static class CampingRole_Patch
    {
      private static BlueprintUnitFact _signaturePerception;
      private static BlueprintUnitFact SignaturePerception
      {
        get
        {
          _signaturePerception ??= BlueprintTool.Get<BlueprintUnitFact>(Guids.SignatureSkillPerception);
          return _signaturePerception;
        }
      }

      [HarmonyPatch(nameof(CampingRole.CreateRuleCheck)), HarmonyPostfix]
      static void CreateRuleCheck(CampingRole __instance, RuleSkillCheck __result)
      {
        try
        {
          if (__result == null)
            return;

          if (__instance.m_RoleType != CampingRoleType.GuardFirstWatch && __instance.m_RoleType != CampingRoleType.GuardSecondWatch)
            return;

          var signatureSkill = __result.Initiator.GetFact(SignaturePerception);
          if (signatureSkill is not null)
          {
            var bonus = __result.Initiator.Stats.SkillPerception.BaseValue >= 15 ? 4 : 2;
            Logger.NativeLog($"Adding (+{bonus}) to {__result.Initiator.CharacterName} (guard duty)");
            __result.AddModifier(bonus, signatureSkill);
          }
        }
        catch (Exception e)
        {
          Logger.LogException("CampingRole_Patch.CreateRuleCheck", e);
        }
      }
    }
    #endregion

    #region Persuasion
    private const string PersuasionName = "SignatureSkill.Persuasion";
    private const string PersuasionDisplayName = "SignatureSkill.Persuasion.Name";
    private const string PersuasionDescription = "SignatureSkill.Persuasion.Description";

    private static BlueprintFeature ConfigurePersuasion()
    {
      return FeatureConfigurator.New(PersuasionName, Guids.SignatureSkillPersuasion)
        .SetDisplayName(PersuasionDisplayName)
        .SetDescription(PersuasionDescription)
        .SetIsClassFeature()
        .AddPrerequisiteStatValue(StatType.SkillPersuasion, value: 5, group: GroupType.Any)
        .AddPrerequisiteClassLevel(CharacterClassRefs.RogueClass.ToString(), level: 5, group: GroupType.Any)
        .AddComponent(new RecommendationSignatureSkill(StatType.SkillPersuasion))
        .AddComponent<SignaturePersuasionComponent>()
        .Configure();
    }

    [TypeId("c01de10e-c307-450d-8c78-81bc2fdaacb3")]
    private class SignaturePersuasionComponent : UnitFactComponentDelegate, IInitiatorDemoralizeHandler
    {
      private static BlueprintBuff _frightened;
      private static BlueprintBuff Frightened
      {
        get
        {
          _frightened ??= BuffRefs.Frightened.Reference.Get();
          return _frightened;
        }
      }

      private static BlueprintBuff _panicked;
      private static BlueprintBuff Panicked
      {
        get
        {
          // TODO: Replace w/ equivalent to CowerBuff
          _panicked ??= BuffRefs.EyebitePanickedBuff.Reference.Get();
          return _panicked;
        }
      }

      private static BlueprintBuff _cowering;
      private static BlueprintBuff Cowering
      {
        get
        {
          _cowering ??= BuffRefs.CowerBuff.Reference.Get();
          return _cowering;
        }
      }

      public void AfterIntimidateSuccess(Demoralize action, RuleSkillCheck intimidateCheck, Buff appliedBuff)
      {
        try
        {
          var target = ContextData<MechanicsContext.Data>.Current?.CurrentTarget?.Unit;
          if (target is null)
          {
            Logger.Warning($"No target for Persuasion.");
            return;
          }

          if (appliedBuff is null)
          {
            Logger.NativeLog($"{target.CharacterName} is immune to Persuasion");
            return;
          }

          var caster = Context.MaybeCaster;
          if (caster is null)
          {
            Logger.Warning($"Caster is missing");
            return;
          }

          var succeedBy = intimidateCheck.RollResult - intimidateCheck.DC;
          if (succeedBy < 10)
          {
            Logger.NativeLog($"Failed to exceed DC by 10: {succeedBy}");
            return;
          }

          var intimidateRanks = Owner.Stats.SkillPersuasion.BaseValue;
          var ruleSavingThrow = new RuleSavingThrow(target, SavingThrowType.Will, 10 + intimidateRanks);
          ruleSavingThrow.Reason = Context;

          var result = Context.TriggerRule(ruleSavingThrow);
          if (result.IsPassed)
            return;

          if (succeedBy >= 20 && intimidateRanks >= 20)
          {
            var cowerDuration =
              ContextValueHelper.CalculateDiceValue(DiceType.D4, diceCountValue: 1, bonusValue: 0, Context);
            target.AddBuff(Cowering, Context, duration: cowerDuration.Rounds().Seconds);
            // Link duration of Panicked to the Persuasion buff
            appliedBuff.StoreFact(target.AddBuff(Panicked, Context));
          }
          else if (succeedBy >= 20 && intimidateRanks >= 15)
            target.AddBuff(Cowering, Context, duration: 1.Rounds().Seconds);
          else if (intimidateRanks >= 10)
            target.AddBuff(Panicked, Context, duration: 1.Rounds().Seconds);
          else
            target.AddBuff(Frightened, Context, duration: 1.Rounds().Seconds);
        }
        catch (Exception e)
        {
          Logger.LogException("SignaturePersuasionComponent.AfterIntimidateSuccess", e);
        }
      }
    }
    #endregion
  }
}
