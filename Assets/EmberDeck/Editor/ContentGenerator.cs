using System.Collections.Generic;
using System.IO;
using EmberDeck.Combat;
using EmberDeck.Content;
using EmberDeck.Content.Effects;
using EmberDeck.Content.Powers;
using EmberDeck.Content.Relics;
using EmberDeck.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EmberDeck.EditorTools
{
    /// <summary>
    /// Generates every content asset and the playable scene from code.
    ///
    /// Content is generated rather than committed as .asset files because ScriptableObject
    /// and scene YAML carry hand-assigned fileIDs and GUIDs: they are unreadable in review,
    /// merge badly, and a single wrong GUID produces a silent null at runtime. Regenerating
    /// from a script keeps the whole set in one diffable file — and makes a balance pass a
    /// code edit plus one menu click.
    ///
    /// The card set and its reasoning are in docs/card-design.md; this file is that document
    /// made executable, in the same order.
    /// </summary>
    public static class ContentGenerator
    {
        const string Root = "Assets/EmberDeck";
        const string ContentRoot = Root + "/Content";
        const string ScenePath = Root + "/Scenes/Combat.unity";
        const string ConfigPath = ContentRoot + "/RunConfig.asset";
        const string CardArtPath = Root + "/Art/Cards";
        const string EnemyArtPath = Root + "/Art/Enemies";

        static readonly Color AttackTint = new(0.86f, 0.42f, 0.28f);
        static readonly Color SkillTint  = new(0.38f, 0.60f, 0.82f);
        static readonly Color PowerTint  = new(0.72f, 0.52f, 0.85f);

        [MenuItem("EmberDeck/Generate Content and Scene")]
        public static void Generate()
        {
            EnsureFolders();
            PurgeStaleContent();
            UpgradeCache.Clear();

            var cards = new List<CardData>();

            // ── Shared effect assets ────────────────────────────────────────────────
            var dmg1  = Damage("Dmg_1", 1);
            var dmg3  = Damage("Dmg_3", 3);
            var dmg4  = Damage("Dmg_4", 4);
            var dmg5  = Damage("Dmg_5", 5);
            var dmg6  = Damage("Dmg_6", 6);
            var dmg10 = Damage("Dmg_10", 10);
            var dmg22 = Damage("Dmg_22", 22);
            var dmg3x2 = Damage("Dmg_3x2", 3, hits: 2);
            var dmg2x3 = Damage("Dmg_2x3", 2, hits: 3);
            var dmg2x2All = Damage("Dmg_2x2_All", 2, hits: 2);
            var sparks = Damage("Dmg_2x4_Burn1", 2, hits: 4, burnPerHit: 1);

            var blk1  = Block("Blk_1", 1);
            var blk3  = Block("Blk_3", 3);
            var blk4  = Block("Blk_4", 4);
            var blk5  = Block("Blk_5", 5);
            var blk6  = Block("Blk_6", 6);
            var blk14 = Block("Blk_16", 16);

            var burn2 = Status("Burn_2", StatusType.Burn, 2);
            var burn5a = Status("Burn_5_Kindle", StatusType.Burn, 5);
            var burn5 = Status("Burn_5", StatusType.Burn, 5);
            var burn7 = Status("Burn_7", StatusType.Burn, 7);
            var vuln2 = Status("Vuln_2", StatusType.Vulnerable, 2);
            var weak2 = Status("Weak_2", StatusType.Weak, 2);
            var str1Self = Status("Str_1_Self", StatusType.Strength, 1, toSelf: true);
            var str2Self = Status("Str_1_Whetstone", StatusType.Strength, 1, toSelf: true);
            var dex2Self = Status("Dex_3_Self", StatusType.Dexterity, 3, toSelf: true);

            var draw1 = Draw("Draw_1", 1);
            var draw2 = Draw("Draw_2", 2);
            var energy1 = Asset<GainEnergyEffect>("Energy_1", e => e.Amount = 1);
            var heal2 = Asset<HealEffect>("Heal_3", e => e.Amount = 3);
            var heal6 = Asset<HealEffect>("Heal_6", e => e.Amount = 6);

            var heat1 = Heat("Heat_1", 1);
            var heat2 = Heat("Heat_2", 2);
            var heat5 = Heat("Heat_5", 5);
            var heat3 = Heat("Heat_3", 3);
            var heat4 = Heat("Heat_4", 4);

            var exhaustTop = Asset<ExhaustCardEffect>("Exhaust_Top", e =>
            {
                e.Source = ExhaustSource.TopOfDrawPile;
                e.Count = 1;
            });
            var exhaustHand = Asset<ExhaustCardEffect>("Exhaust_Hand", e =>
            {
                e.Source = ExhaustSource.RandomFromHand;
                e.Count = 1;
            });

            // ── Starter ─────────────────────────────────────────────────────────────
            var strike    = Card(cards, "strike", "Strike", CardType.Attack, 1, TargetMode.SingleEnemy, CardRarity.Starter, dmg6);
            var guard     = Card(cards, "guard", "Guard", CardType.Skill, 1, TargetMode.Self, CardRarity.Starter, blk5);
            var emberLash = Card(cards, "ember_lash", "Ember Lash", CardType.Attack, 1, TargetMode.SingleEnemy, CardRarity.Starter, dmg5, burn2);
            var stoke     = Card(cards, "stoke", "Stoke", CardType.Skill, 1, TargetMode.Self, CardRarity.Starter, heat4, draw1);

            // ── Common — Forge ──────────────────────────────────────────────────────
            Card(cards, "bulwark", "Bulwark", CardType.Skill, 2, TargetMode.Self, CardRarity.Common, blk14);
            Card(cards, "anvil_strike", "Anvil Strike", CardType.Attack, 1, TargetMode.SingleEnemy, CardRarity.Common,
                 Asset<DamageFromBlockEffect>("BlockDamage_x15", e => { e.Multiplier = 1.5f; e.ConsumeBlock = false; }));
            Card(cards, "brace", "Brace", CardType.Skill, 0, TargetMode.Self, CardRarity.Common, blk4);
            Card(cards, "temper", "Temper", CardType.Power, 1, TargetMode.Self, CardRarity.Common, dex2Self);

            // ── Common — Swarm ──────────────────────────────────────────────────────
            Card(cards, "twin_fangs", "Twin Fangs", CardType.Attack, 1, TargetMode.SingleEnemy, CardRarity.Common, dmg3x2);
            Card(cards, "whetstone", "Whetstone", CardType.Power, 1, TargetMode.Self, CardRarity.Common, str2Self);
            Card(cards, "flurry", "Flurry", CardType.Attack, 1, TargetMode.SingleEnemy, CardRarity.Common, dmg2x3);
            Card(cards, "quick_jab", "Quick Jab", CardType.Attack, 0, TargetMode.SingleEnemy, CardRarity.Common, dmg3);

            // ── Common — Pyre ───────────────────────────────────────────────────────
            Card(cards, "kindle", "Kindle", CardType.Skill, 1, TargetMode.SingleEnemy, CardRarity.Common, burn5a);
            Card(cards, "scorch", "Scorch", CardType.Attack, 1, TargetMode.SingleEnemy, CardRarity.Common, dmg4, Status("Burn_3_Scorch", StatusType.Burn, 3));
            Card(cards, "fan_the_flames", "Fan the Flames", CardType.Skill, 0, TargetMode.AllEnemies, CardRarity.Common, burn2);
            Exhausting(Card(cards, "smoulder", "Smoulder", CardType.Skill, 1, TargetMode.SingleEnemy, CardRarity.Common, burn7));

            // ── Common — Overdrive ──────────────────────────────────────────────────
            Card(cards, "bellows", "Bellows", CardType.Skill, 0, TargetMode.Self, CardRarity.Common, heat3);
            Card(cards, "vent", "Vent", CardType.Skill, 1, TargetMode.Self, CardRarity.Common,
                 Asset<SpendHeatEffect>("SpendHeat_Block", e => { e.Payout = HeatPayout.Block; e.Ratio = 1f; }));
            Card(cards, "flare", "Flare", CardType.Attack, 1, TargetMode.SingleEnemy, CardRarity.Common,
                 Asset<ScaleWithHeatEffect>("ScaleHeat_Dmg4", e => { e.Payout = HeatPayout.Damage; e.Base = 4; }));
            Card(cards, "heat_sink", "Heat Sink", CardType.Skill, 1, TargetMode.Self, CardRarity.Common, heat3, blk5);

            // ── Common — Ashfall ────────────────────────────────────────────────────
            // Exhausting, since decks are built rather than drafted: at 0 cost for +1 Energy it is a net
            // gain every time it is played, and the best constructed deck simply held four of them and
            // never ran out of Energy again.
            Exhausting(Card(cards, "cremate", "Cremate", CardType.Skill, 0, TargetMode.Self, CardRarity.Common, exhaustTop, energy1));
            Exhausting(Card(cards, "ash_cloud", "Ash Cloud", CardType.Skill, 1, TargetMode.AllEnemies, CardRarity.Common, weak2));
            Exhausting(Card(cards, "salvage", "Salvage", CardType.Skill, 1, TargetMode.Self, CardRarity.Common, draw2));

            // ── Common — neutral ────────────────────────────────────────────────────
            var focus = Card(cards, "focus", "Focus", CardType.Skill, 1, TargetMode.Self, CardRarity.Common, draw2);
            Card(cards, "second_wind", "Second Wind", CardType.Skill, 1, TargetMode.Self, CardRarity.Common, blk6, draw1);
            Exhausting(Card(cards, "mend", "Mend", CardType.Skill, 1, TargetMode.Self, CardRarity.Common, heal6));

            // ── Uncommon — Forge ────────────────────────────────────────────────────
            Card(cards, "reinforce", "Reinforce", CardType.Skill, 2, TargetMode.Self, CardRarity.Uncommon,
                 Asset<MultiplyBlockEffect>("BlockMul_2", e => e.Multiplier = 2));
            Card(cards, "counterweight", "Counterweight", CardType.Attack, 2, TargetMode.SingleEnemy, CardRarity.Uncommon,
                 Asset<DamageFromBlockEffect>("BlockDamage_x2_Consume", e => { e.Multiplier = 2f; e.ConsumeBlock = true; }));
            Card(cards, "ironhide", "Ironhide", CardType.Power, 1, TargetMode.Self, CardRarity.Uncommon,
                 Power("Pow_Ironhide", PowerTrigger.TurnStart, "At the start of each turn, gain 5 Block.", false, blk5));
            Card(cards, "forge_rite", "Forge Rite", CardType.Power, 2, TargetMode.Self, CardRarity.Uncommon,
                 Power("Pow_ForgeRite", PowerTrigger.BlockGained, "Whenever you gain Block, gain 1 Heat.", false, heat1));

            // ── Uncommon — Swarm ────────────────────────────────────────────────────
            var cinderStorm = Card(cards, "cinder_storm", "Cinder Storm", CardType.Attack, 2, TargetMode.AllEnemies, CardRarity.Uncommon, dmg2x2All);
            Card(cards, "rising_heat", "Rising Heat", CardType.Power, 2, TargetMode.Self, CardRarity.Uncommon,
                 Power("Pow_RisingHeat", PowerTrigger.AttackPlayed, "Whenever you play an Attack, gain 1 Heat.", false, heat1));
            Card(cards, "rain_of_sparks", "Rain of Sparks", CardType.Attack, 1, TargetMode.SingleEnemy, CardRarity.Uncommon, sparks);
            Card(cards, "frenzy", "Frenzy", CardType.Attack, 1, TargetMode.SingleEnemy, CardRarity.Uncommon,
                 Asset<ScaleWithCounterEffect>("Counter_PlayedDmg3", e =>
                 {
                     e.Counter = CombatCounter.CardsPlayedThisTurn;
                     e.Payout = HeatPayout.Damage;
                     e.PerPoint = 3;
                 }));

            // ── Uncommon — Pyre ─────────────────────────────────────────────────────
            Card(cards, "wildfire", "Wildfire", CardType.Skill, 2, TargetMode.AllEnemies, CardRarity.Uncommon, Status("Burn_8_Wildfire", StatusType.Burn, 8));
            Card(cards, "bellows_blast", "Bellows Blast", CardType.Skill, 1, TargetMode.SingleEnemy, CardRarity.Uncommon,
                 Asset<MultiplyStatusEffect>("BurnMul_2", e => { e.Status = StatusType.Burn; e.Multiplier = 2; }));
            Card(cards, "slow_roast", "Slow Roast", CardType.Power, 1, TargetMode.Self, CardRarity.Uncommon,
                 Asset<RuleChangeEffect>("Rule_SlowRoast", e =>
                 {
                     e.StopBurnDecaying = true;
                     e.Text = "Burn no longer decreases at the end of turn.";
                 }));
            Card(cards, "immolate", "Immolate", CardType.Attack, 2, TargetMode.SingleEnemy, CardRarity.Uncommon,
                 Asset<DamageFromStatusEffect>("StatusDamage_Burn", e => { e.Status = StatusType.Burn; e.Multiplier = 1.5f; }));
            Card(cards, "backdraft", "Backdraft", CardType.Skill, 1, TargetMode.AllEnemies, CardRarity.Uncommon,
                 Asset<SpendHeatEffect>("SpendHeat_BurnAll", e =>
                 {
                     e.Payout = HeatPayout.Burn;
                     e.Ratio = 0.5f;
                     e.AllEnemies = true;
                 }));

            // ── Uncommon — Overdrive ────────────────────────────────────────────────
            Card(cards, "detonate", "Detonate", CardType.Attack, 1, TargetMode.SingleEnemy, CardRarity.Uncommon,
                 Asset<SpendHeatEffect>("SpendHeat_Dmg2x", e => { e.Payout = HeatPayout.Damage; e.Ratio = 2f; }));
            Card(cards, "heat_shield", "Heat Shield", CardType.Skill, 1, TargetMode.Self, CardRarity.Uncommon,
                 Asset<ScaleWithHeatEffect>("ScaleHeat_Block", e => { e.Payout = HeatPayout.Block; e.Base = 0; }));
            // Exhausting, for the same reason as Cremate: 0 cost and two cards drawn is an engine, and a
            // deck holding three of them drew its whole deck every fight and paid only in Heat.
            Exhausting(Card(cards, "overclock", "Overclock", CardType.Skill, 0, TargetMode.Self, CardRarity.Uncommon, heat5, draw2));
            Card(cards, "coolant", "Coolant", CardType.Skill, 1, TargetMode.Self, CardRarity.Uncommon,
                 Asset<SpendHeatEffect>("SpendHeat_Block6", e =>
                 {
                     e.Payout = HeatPayout.Block;
                     e.Ratio = 1.5f;
                     e.MaxSpent = 6;
                 }));
            Card(cards, "thermal_mass", "Thermal Mass", CardType.Power, 1, TargetMode.Self, CardRarity.Uncommon,
                 Asset<RuleChangeEffect>("Rule_ThermalMass", e =>
                 {
                     e.OverheatThresholdDelta = 5;
                     e.Text = "Your Overheat threshold increases by 5.";
                 }));

            // ── Uncommon — Ashfall ──────────────────────────────────────────────────
            Card(cards, "pyre_rite", "Pyre Rite", CardType.Power, 1, TargetMode.Self, CardRarity.Uncommon,
                 Power("Pow_PyreRite", PowerTrigger.CardExhausted, "Whenever you Exhaust a card, gain 2 Heat.", false, heat2));
            Card(cards, "burnt_offering", "Burnt Offering", CardType.Attack, 1, TargetMode.SingleEnemy, CardRarity.Uncommon,
                 exhaustHand, Damage("Dmg_16", 16));
            Exhausting(Card(cards, "ash_armor", "Ash Armor", CardType.Skill, 1, TargetMode.Self, CardRarity.Uncommon,
                 Asset<ScaleWithCounterEffect>("Counter_ExhaustBlock4", e =>
                 {
                     e.Counter = CombatCounter.CardsExhaustedThisCombat;
                     e.Payout = HeatPayout.Block;
                     e.PerPoint = 10;
                 })));

            // ── Rare ────────────────────────────────────────────────────────────────
            Card(cards, "molten_armor", "Molten Armor", CardType.Power, 2, TargetMode.Self, CardRarity.Rare,
                 Asset<RuleChangeEffect>("Rule_MoltenArmor", e =>
                 {
                     e.MakeBlockPersist = true;
                     e.Text = "Your Block is no longer removed at the start of your turn.";
                 }));
            Card(cards, "living_anvil", "Living Anvil", CardType.Power, 3, TargetMode.Self, CardRarity.Rare,
                 Power("Pow_LivingAnvil", PowerTrigger.TurnEnd,
                       "At the end of your turn, deal damage equal to half your Block to an enemy.", false,
                       Asset<DamageFromBlockEffect>("BlockDamage_Half", e => { e.Multiplier = 0.5f; e.ConsumeBlock = false; })));
            Card(cards, "thousand_cuts", "Thousand Cuts", CardType.Power, 2, TargetMode.Self, CardRarity.Rare,
                 Power("Pow_ThousandCuts", PowerTrigger.CardPlayed,
                       "Whenever you play a card, deal 1 damage to ALL enemies.", true, dmg1));
            Card(cards, "searing_blade", "Searing Blade", CardType.Power, 1, TargetMode.Self, CardRarity.Rare,
                 Power("Pow_SearingBlade", PowerTrigger.BurnApplied,
                       "Whenever you apply Burn, gain 1 Strength.", false, str1Self));
            Card(cards, "conflagration", "Conflagration", CardType.Skill, 2, TargetMode.AllEnemies, CardRarity.Rare,
                 Asset<MultiplyStatusEffect>("BurnMul_2_All", e =>
                 {
                     e.Status = StatusType.Burn;
                     e.Multiplier = 2;
                     e.AllEnemies = true;
                 }));
            Card(cards, "eternal_flame", "Eternal Flame", CardType.Power, 2, TargetMode.Self, CardRarity.Rare,
                 Power("Pow_EternalFlame", PowerTrigger.TurnEnd,
                       "At the end of your turn, apply 2 Burn to ALL enemies.", true, burn2));
            Card(cards, "meltdown", "Meltdown", CardType.Attack, 3, TargetMode.AllEnemies, CardRarity.Rare,
                 Asset<SpendHeatEffect>("SpendHeat_DmgAll", e =>
                 {
                     e.Payout = HeatPayout.Damage;
                     e.Ratio = 1f;
                     e.AllEnemies = true;
                 }));
            Card(cards, "perpetual_flame", "Perpetual Flame", CardType.Power, 2, TargetMode.Self, CardRarity.Rare,
                 Power("Pow_PerpetualFlame", PowerTrigger.TurnStart,
                       "At the start of your turn, gain 3 Heat.", false, heat3),
                 Asset<RuleChangeEffect>("Rule_PerpetualFlame", e =>
                 {
                     e.OverheatThresholdDelta = 3;
                     e.Text = "Your Overheat threshold increases by 3.";
                 }));
            Card(cards, "ember_engine", "Ember Engine", CardType.Power, 2, TargetMode.Self, CardRarity.Rare,
                 Power("Pow_EmberEngine", PowerTrigger.HeatGained, "Whenever you gain Heat, gain 1 Block.", false, blk1));
            Card(cards, "phoenix_ash", "Phoenix Ash", CardType.Power, 2, TargetMode.Self, CardRarity.Rare,
                 Power("Pow_PhoenixAsh", PowerTrigger.CardExhausted, "Whenever you Exhaust a card, heal 3 HP.", false, heal2));
            Card(cards, "cinder_trance", "Cinder Trance", CardType.Power, 2, TargetMode.Self, CardRarity.Rare,
                 Power("Pow_CinderTrance", PowerTrigger.TurnStart,
                       "At the start of your turn, exhaust the top card of your draw pile and gain 1 Energy.",
                       false, exhaustTop, energy1));
            Card(cards, "second_forge", "Second Forge", CardType.Power, 3, TargetMode.Self, CardRarity.Rare,
                 Power("Pow_SecondForge", PowerTrigger.TurnStart, "Gain 1 Energy at the start of each turn.", false, energy1));
            Exhausting(Card(cards, "last_ember", "Last Ember", CardType.Attack, 3, TargetMode.SingleEnemy, CardRarity.Rare, dmg22));

            // ── Found in the Emberheart ─────────────────────────────────────────────
            // Two cards that only Act 3's rewards offer (MinAct 3), both about the act's rule: being
            // overheated at the start of a turn gives 1 Energy. Heartfire gets you over the line for
            // nothing; Molten Core is what the Heat you are carrying is for.
            FromAct(3, Card(cards, "heartfire", "Heartfire", CardType.Skill, 0, TargetMode.Self, CardRarity.Uncommon,
                            Heat("Heat_6_Heartfire", 6), draw1));
            FromAct(3, Card(cards, "molten_core", "Molten Core", CardType.Attack, 2, TargetMode.SingleEnemy, CardRarity.Rare,
                            Asset<ScaleWithHeatEffect>("ScaleHeat_Dmg8_MoltenCore", e => { e.Payout = HeatPayout.Damage; e.Base = 8; })));

            // ── Unlockable ──────────────────────────────────────────────────────────
            // Cards earned between runs (docs/meta-progression.md). Kept out of `cards`, so they reach a
            // reward pool only through RunConfig.Unlocks; built from existing effects like everything else.
            var unlockCards = new List<CardData>();
            var unlockBlk2 = Block("Unlock_Blk_2", 2);
            var crucible = Card(unlockCards, "crucible", "Crucible", CardType.Skill, 1, TargetMode.Self, CardRarity.Common,
                                Block("Unlock_Blk_7", 7), heat3);
            var brand = Card(unlockCards, "brand", "Brand", CardType.Attack, 1, TargetMode.SingleEnemy, CardRarity.Common,
                             Damage("Unlock_Dmg_6b", 6), Status("Unlock_Vuln_1", StatusType.Vulnerable, 1));
            var kilnGuard = Card(unlockCards, "kiln_guard", "Kiln Guard", CardType.Power, 2, TargetMode.Self, CardRarity.Uncommon,
                                 Power("Pow_Unlock_KilnGuard", PowerTrigger.AttackPlayed, "Whenever you play an Attack, gain 2 Block.", false, unlockBlk2));

            var emberScatter = Card(unlockCards, "ember_scatter", "Ember Scatter", CardType.Attack, 1, TargetMode.AllEnemies, CardRarity.Common,
                                    Damage("Unlock_Dmg_3_All", 3));
            var bladeDance = Card(unlockCards, "blade_dance", "Blade Dance", CardType.Attack, 2, TargetMode.SingleEnemy, CardRarity.Uncommon,
                                  Damage("Unlock_Dmg_3x4", 3, hits: 4));
            var momentum = Exhausting(Card(unlockCards, "momentum", "Momentum", CardType.Skill, 0, TargetMode.Self, CardRarity.Uncommon,
                                           energy1));

            var magmaHeart = Card(unlockCards, "magma_heart", "Magma Heart", CardType.Power, 1, TargetMode.Self, CardRarity.Uncommon,
                                  Power("Pow_Unlock_MagmaHeart", PowerTrigger.BurnApplied, "Whenever you apply Burn, gain 1 Block.", false, blk1));
            var supernova = Exhausting(Card(unlockCards, "supernova", "Supernova", CardType.Attack, 2, TargetMode.AllEnemies, CardRarity.Rare,
                                            Asset<SpendHeatEffect>("Unlock_SpendHeat_DmgAll", e =>
                                            {
                                                e.Payout = HeatPayout.Damage;
                                                e.Ratio = 1f;
                                                e.AllEnemies = true;
                                            })));
            var phoenixPlume = Exhausting(Card(unlockCards, "phoenix_plume", "Phoenix Plume", CardType.Skill, 1, TargetMode.Self, CardRarity.Uncommon,
                                               Block("Unlock_Blk_9", 9), Asset<HealEffect>("Unlock_Heal_3", e => e.Amount = 3)));

            var upgradable = new List<CardData>(cards);
            upgradable.AddRange(unlockCards);
            var upgrades = BuildUpgrades(upgradable, draw1);

            // ── Enemies ─────────────────────────────────────────────────────────────
            var bite    = Move("Move_Bite", "Bite", IntentKind.Attack, 1, Damage("Dmg_Bite", 10));
            var skitter = Move("Move_Skitter", "Skitter", IntentKind.Block, 1, Block("Blk_Skitter", 11));
            // Ash Hound appears only in the elite, and it was the elite's killer. Hunter-style
            // play died inside elites 108 times in 300 runs. The danger was escalation, not the
            // opening hit: Brace (13 Block) stretches the fight and every Howl stacked +3 Strength,
            // so Maul grew 14 -> 17 -> 20. Howl now grants +1 and Maul starts at 12. Its health is
            // unchanged, so an elite is still a long, costly fight — just not one that turns
            // lethal because it lasted.
            var maul    = Move("Move_Maul", "Maul", IntentKind.Attack, 3, Damage("Dmg_Maul", 12));
            var howl    = Move("Move_Howl", "Howl", IntentKind.Buff, 1,
                               Status("Str_Howl_Self", StatusType.Strength, 1, toSelf: true));
            var brace   = Move("Move_Brace", "Brace", IntentKind.Block, 1, Block("Blk_Brace", 13));
            var spark   = Move("Move_Spark", "Spark", IntentKind.Attack, 1, Damage("Dmg_Spark", 7));
            var ignite  = Move("Move_Ignite", "Ignite", IntentKind.Debuff, 1,
                               Damage("Dmg_Ignite", 4), Status("Weak_1_Enemy", StatusType.Weak, 1));

            var cinderRat = MakeEnemy("cinder_rat", "Cinder Rat", 20, 24, MovePattern.Sequence,
                                      new Color(0.70f, 0.33f, 0.26f), bite, skitter, bite, bite);
            var ashHound = MakeEnemy("ash_hound", "Ash Hound", 30, 35, MovePattern.WeightedRandom,
                                     new Color(0.45f, 0.30f, 0.42f), maul, howl, brace);
            var emberling = MakeEnemy("emberling", "Emberling", 12, 15, MovePattern.Sequence,
                                      new Color(0.85f, 0.58f, 0.25f), spark, ignite);

            // The boss. Its moves are bigger versions of moves the player has already met,
            // so the fight is readable on sight — a boss whose vocabulary is entirely new
            // punishes the player for knowledge they had no way to acquire.
            var crush   = Move("Move_Crush", "Crush", IntentKind.Attack, 3, Damage("Dmg_Crush", 13));
            var forge   = Move("Move_Forge", "Forge", IntentKind.Buff, 1,
                               Status("Str_Tyrant_Self", StatusType.Strength, 2, toSelf: true),
                               Block("Blk_Tyrant", 10));
            var scourge = Move("Move_Scourge", "Scourge", IntentKind.Attack, 2, Damage("Dmg_Scourge", 5, hits: 3));
            // 110-120 (was 90-100; 100-110 left run wins at 33-37%). Shops, events and potions all added
            // player power, and the boss fight
            // is close enough that one potion turned a loss at 20% boss health into a win: potions alone
            // added about ten points of run wins. The challenge is retuned rather than the new systems
            // weakened until they stop being worth finding.
            var tyrant = MakeEnemy("forge_tyrant", "Forge Tyrant", 110, 120, MovePattern.Sequence,
                                   new Color(0.86f, 0.34f, 0.22f), crush, scourge, forge, crush);

            // ── Encounter variety ───────────────────────────────────────────────────
            // Every hallway used to be Emberling + Cinder Rat, so after two fights there was nothing
            // left to read. Each new enemy asks the deck one question, built only from effects that
            // already exist — no new engine code:
            //   Ash Mite, in threes  can the deck hit more than one target?
            //   Slag Beetle          can it get through heavy Block? (Burn ignores Block)
            //   Kiln Imp             can it live with Burn on the player?
            //   Ash Wraith           can it play through Vulnerable and Weak?
            //   Cinder Cultist       can it race an enemy that keeps getting stronger?
            //   Molten Golem, Salamanders — elites asking the same questions, harder.
            // Effect files are prefixed EnemyFx_: CreateAsset reuses a file by name, so a shared
            // name would silently give two moves the last configuration written.
            var nibble  = Move("Move_Nibble", "Nibble", IntentKind.Attack, 3, Damage("EnemyFx_Nibble", 6));
            var burrow  = Move("Move_Burrow", "Burrow", IntentKind.Block, 1, Block("EnemyFx_Burrow", 6));
            var ashMite = MakeEnemy("ash_mite", "Ash Mite", 9, 11, MovePattern.WeightedRandom,
                                    new Color(0.55f, 0.52f, 0.50f), nibble, burrow);

            var harden  = Move("Move_Harden", "Harden", IntentKind.Block, 1, Block("EnemyFx_Harden", 12));
            var ram     = Move("Move_Ram", "Ram", IntentKind.Attack, 1, Damage("EnemyFx_Ram", 12));
            var slagBeetle = MakeEnemy("slag_beetle", "Slag Beetle", 36, 40, MovePattern.Sequence,
                                       new Color(0.40f, 0.36f, 0.34f), harden, ram, ram);

            var scald   = Move("Move_Scald", "Scald", IntentKind.Attack, 1,
                               Damage("EnemyFx_Scald", 4), Status("EnemyFx_Scald_Burn", StatusType.Burn, 3));
            var flick   = Move("Move_Flick", "Flick", IntentKind.Attack, 1, Damage("EnemyFx_Flick", 8));
            var cower   = Move("Move_Cower", "Cower", IntentKind.Block, 1, Block("EnemyFx_Cower", 9));
            var kilnImp = MakeEnemy("kiln_imp", "Kiln Imp", 34, 38, MovePattern.Sequence,
                                    new Color(0.88f, 0.42f, 0.18f), scald, flick, cower);

            var wail    = Move("Move_Wail", "Wail", IntentKind.Debuff, 1,
                               Status("EnemyFx_Wail_Vulnerable", StatusType.Vulnerable, 2),
                               Status("EnemyFx_Wail_Weak", StatusType.Weak, 1));
            var chill   = Move("Move_ChillTouch", "Chill Touch", IntentKind.Attack, 1, Damage("EnemyFx_Chill", 8));
            var ashWraith = MakeEnemy("ash_wraith", "Ash Wraith", 24, 28, MovePattern.Sequence,
                                      new Color(0.50f, 0.55f, 0.66f), wail, chill, chill);

            var chant   = Move("Move_Chant", "Chant", IntentKind.Buff, 1,
                               Status("EnemyFx_Chant_Strength", StatusType.Strength, 2, toSelf: true));
            var stab    = Move("Move_Stab", "Stab", IntentKind.Attack, 1, Damage("EnemyFx_Stab", 5));
            var cultist = MakeEnemy("cinder_cultist", "Cinder Cultist", 20, 23, MovePattern.Sequence,
                                    new Color(0.62f, 0.22f, 0.24f), chant, stab, stab, stab);

            var slam    = Move("Move_Slam", "Slam", IntentKind.Attack, 1, Damage("EnemyFx_Slam", 11));
            var spray   = Move("Move_MagmaSpray", "Magma Spray", IntentKind.Attack, 1,
                               Asset<DealDamageEffect>("EnemyFx_MagmaSpray", e =>
                               {
                                   e.Amount = 3;
                                   e.Hits = 3;
                                   e.PerHitStatus = StatusType.Burn;
                                   e.PerHitStatusAmount = 1;
                               }));
            var cool    = Move("Move_Cool", "Cool", IntentKind.Buff, 1,
                               Status("EnemyFx_Cool_Strength", StatusType.Strength, 1, toSelf: true),
                               Block("EnemyFx_Cool", 10));
            var golem   = MakeEnemy("molten_golem", "Molten Golem", 46, 50, MovePattern.Sequence,
                                    new Color(0.80f, 0.30f, 0.15f), slam, spray, cool);

            var firebite = Move("Move_Firebite", "Firebite", IntentKind.Attack, 3,
                                Damage("EnemyFx_Firebite", 6), Status("EnemyFx_Firebite_Burn", StatusType.Burn, 1));
            var coil     = Move("Move_Coil", "Coil", IntentKind.Block, 1, Block("EnemyFx_Coil", 9));
            var tailWhip = Move("Move_TailWhip", "Tail Whip", IntentKind.Attack, 2, Damage("EnemyFx_TailWhip", 4, hits: 2));
            var salamander = MakeEnemy("salamander", "Salamander", 22, 25, MovePattern.WeightedRandom,
                                       new Color(0.90f, 0.50f, 0.20f), firebite, coil, tailWhip);

            // ── Act 2: the Obsidian Deep ────────────────────────────────────────────
            // The Act 1 questions again, asked of a deck that has had a whole act to grow:
            //   Ember Wisps, in threes   area damage, while Burn stacks on the player
            //   Magma Leech              a race: it heals itself as it bites
            //   Obsidian Sentinel        Block big enough that only burst or Burn gets through
            //   Ashen Knight             Vulnerable, then the hit that punishes it
            //   Cinder Shaman            Weak and Vulnerable, and stronger every cycle it is left alone
            // Scaling restarts with the act (RunState.ActStartFight), so these are authored at full strength
            // rather than arriving multiplied by every fight of Act 1.
            var flicker   = Move("Move_Flicker", "Flicker", IntentKind.Attack, 1,
                                 Damage("EnemyFx_Flicker", 3), Status("EnemyFx_Flicker_Burn", StatusType.Burn, 1));
            var wispFlare = Move("Move_WispFlare", "Flare", IntentKind.Attack, 1, Damage("EnemyFx_WispFlare", 6));
            var emberWisp = MakeEnemy("ember_wisp", "Ember Wisp", 9, 11, MovePattern.Sequence,
                                      new Color(0.62f, 0.55f, 0.95f), flicker, wispFlare);

            var siphon = Move("Move_Siphon", "Siphon", IntentKind.Attack, 1,
                              Damage("EnemyFx_Siphon", 6), Asset<HealEffect>("EnemyFx_Siphon_Heal", e => e.Amount = 3));
            var latch  = Move("Move_Latch", "Latch", IntentKind.Attack, 1,
                              Damage("EnemyFx_Latch", 4), Status("EnemyFx_Latch_Weak", StatusType.Weak, 1));
            var magmaLeech = MakeEnemy("magma_leech", "Magma Leech", 22, 24, MovePattern.Sequence,
                                       new Color(0.78f, 0.36f, 0.20f), siphon, latch);

            var fortify = Move("Move_Fortify", "Fortify", IntentKind.Block, 1, Block("EnemyFx_Fortify", 12));
            var halberd = Move("Move_Halberd", "Halberd", IntentKind.Attack, 1, Damage("EnemyFx_Halberd", 9));
            var sentinel = MakeEnemy("obsidian_sentinel", "Obsidian Sentinel", 40, 43, MovePattern.Sequence,
                                     new Color(0.30f, 0.26f, 0.40f), fortify, halberd, halberd);

            var sunder = Move("Move_Sunder", "Sunder", IntentKind.Debuff, 1,
                              Damage("EnemyFx_Sunder", 6), Status("EnemyFx_Sunder_Vulnerable", StatusType.Vulnerable, 1));
            var cleave = Move("Move_Cleave", "Cleave", IntentKind.Attack, 1, Damage("EnemyFx_Cleave", 9));
            var steel  = Move("Move_Steel", "Steel", IntentKind.Buff, 1,
                              Block("EnemyFx_Steel", 8), Status("EnemyFx_Steel_Strength", StatusType.Strength, 1, toSelf: true));
            var ashenKnight = MakeEnemy("ashen_knight", "Ashen Knight", 36, 39, MovePattern.Sequence,
                                        new Color(0.55f, 0.52f, 0.60f), sunder, cleave, steel);

            var hex      = Move("Move_Hex", "Hex", IntentKind.Debuff, 1,
                                Status("EnemyFx_Hex_Weak", StatusType.Weak, 1), Status("EnemyFx_Hex_Vulnerable", StatusType.Vulnerable, 1));
            var firebolt = Move("Move_Firebolt", "Firebolt", IntentKind.Attack, 1,
                                Damage("EnemyFx_Firebolt", 6), Status("EnemyFx_Firebolt_Burn", StatusType.Burn, 2));
            var ritual   = Move("Move_Ritual", "Ritual", IntentKind.Buff, 1,
                                Status("EnemyFx_Ritual_Strength", StatusType.Strength, 1, toSelf: true));
            var shaman = MakeEnemy("cinder_shaman", "Cinder Shaman", 22, 24, MovePattern.Sequence,
                                   new Color(0.70f, 0.40f, 0.62f), hex, firebolt, ritual);

            // The Act 2 elite that is not a pairing: the Sentinel's question at elite scale.
            var shatter     = Move("Move_Shatter", "Shatter", IntentKind.Attack, 1, Damage("EnemyFx_Shatter", 3, hits: 3));
            var quake       = Move("Move_Quake", "Quake", IntentKind.Attack, 1, Damage("EnemyFx_Quake", 11));
            var crystalWard = Move("Move_CrystalWard", "Ward", IntentKind.Buff, 1,
                                   Block("EnemyFx_CrystalWard", 14), Status("EnemyFx_CrystalWard_Strength", StatusType.Strength, 1, toSelf: true));
            var colossus = MakeEnemy("obsidian_colossus", "Obsidian Colossus", 60, 64, MovePattern.Sequence,
                                     new Color(0.36f, 0.28f, 0.50f), shatter, quake, crystalWard);

            // The Act 2 boss. Like the Tyrant, its moves are bigger versions of ones already met: Burn on
            // every hit (Magma Spray), Weak with Vulnerable (Wail, Hex), one huge blow, and a Block turn that
            // also makes it stronger.
            // Short move names: a debuff intent shows its name beside the status icons, and "Molten Roar"
            // ran into them.
            var roar   = Move("Move_MoltenRoar", "Roar", IntentKind.Debuff, 1,
                              Status("EnemyFx_Roar_Weak", StatusType.Weak, 1), Status("EnemyFx_Roar_Vulnerable", StatusType.Vulnerable, 1));
            var breath = Move("Move_InfernoBreath", "Inferno Breath", IntentKind.Attack, 1,
                              Asset<DealDamageEffect>("EnemyFx_InfernoBreath", e =>
                              {
                                  e.Amount = 3;
                                  e.Hits = 4;
                                  e.PerHitStatus = StatusType.Burn;
                                  e.PerHitStatusAmount = 1;
                              }));
            var sweep  = Move("Move_TailSweep", "Tail Sweep", IntentKind.Attack, 1, Damage("EnemyFx_TailSweep", 14));
            var coilUp = Move("Move_WyrmCoil", "Coil", IntentKind.Buff, 1,
                              Block("EnemyFx_WyrmCoil", 16), Status("EnemyFx_WyrmCoil_Strength", StatusType.Strength, 1, toSelf: true));
            var wyrm = MakeEnemy("cinder_wyrm", "Cinder Wyrm", 120, 130, MovePattern.Sequence,
                                 new Color(0.52f, 0.24f, 0.30f), roar, breath, sweep, coilUp, breath, sweep);

            // ── Act 3: the Emberheart ───────────────────────────────────────────────
            // Below the Obsidian Deep is what the forge was built over. The act's own question is
            // attrition: every enemy here either grows, heals, or hits through Block, so a deck that
            // won two acts by holding still runs out of time.
            //   Cinder Revenant     grows two Strength a cycle: kill it early or not at all
            //   Molten Maw          one enormous hit, announced a turn ahead
            //   Ash Priest          heals itself: burst it down or watch the fight reset
            //   Emberfly            small, many hits, always in numbers
            //   Slag Titan          Block and a crushing blow, the Sentinel grown up
            //   Living Flame (elite) burns while it eats: damage that ignores Block
            var rend    = Move("Move_Rend", "Rend", IntentKind.Attack, 1, Damage("EnemyFx_Rend", 8));
            var kindling = Move("Move_Kindling", "Kindle", IntentKind.Buff, 1,
                                Status("EnemyFx_Kindling_Strength", StatusType.Strength, 2, toSelf: true));
            var revenant = MakeEnemy("cinder_revenant", "Cinder Revenant", 25, 28, MovePattern.Sequence,
                                     new Color(0.85f, 0.42f, 0.28f), kindling, rend, rend);

            var gape     = Move("Move_Gape", "Gape", IntentKind.Buff, 1, Block("EnemyFx_Gape", 8));
            var swallow  = Move("Move_Swallow", "Swallow", IntentKind.Attack, 1, Damage("EnemyFx_Swallow", 14));
            var moltenMaw = MakeEnemy("molten_maw", "Molten Maw", 30, 33, MovePattern.Sequence,
                                      new Color(0.72f, 0.22f, 0.24f), gape, swallow);

            // The Emberheart heats you. Censer and Mote add Heat to the player rather than a debuff, so its
            // rule — overheated at the start of a turn, gain 1 Energy — comes to every deck here, not only
            // to the ones built to chase it. The Heat still burns at the end of the turn.
            var censer     = Move("Move_Censer", "Censer", IntentKind.Attack, 1,
                                  Damage("EnemyFx_Censer", 5), Heat("EnemyFx_Censer_Heat", 3));
            var absolution = Move("Move_Absolution", "Absolution", IntentKind.Buff, 1,
                                  Asset<HealEffect>("EnemyFx_Absolution", e => e.Amount = 8), Block("EnemyFx_Absolution_Block", 8));
            var doom       = Move("Move_Doom", "Doom", IntentKind.Attack, 1, Damage("EnemyFx_Doom", 10));
            var ashPriest = MakeEnemy("ash_priest", "Ash Priest", 27, 30, MovePattern.Sequence,
                                      new Color(0.80f, 0.74f, 0.58f), censer, absolution, doom);

            var sting = Move("Move_Sting", "Sting", IntentKind.Attack, 1, Damage("EnemyFx_Sting", 3, hits: 3));
            var mote  = Move("Move_Mote", "Mote", IntentKind.Attack, 1,
                             Damage("EnemyFx_Mote", 4), Heat("EnemyFx_Mote_Heat", 2));
            var emberfly = MakeEnemy("emberfly", "Emberfly", 12, 14, MovePattern.Sequence,
                                     new Color(0.95f, 0.72f, 0.30f), sting, mote);

            var stomp  = Move("Move_Stomp", "Stomp", IntentKind.Attack, 1, Damage("EnemyFx_Stomp", 12));
            var titanHarden = Move("Move_TitanHarden", "Harden", IntentKind.Buff, 1,
                                   Block("EnemyFx_TitanHarden", 15), Status("EnemyFx_TitanHarden_Strength", StatusType.Strength, 1, toSelf: true));
            var titanCrush  = Move("Move_TitanCrush", "Crush", IntentKind.Attack, 1, Damage("EnemyFx_TitanCrush", 15));
            var slagTitan = MakeEnemy("slag_titan", "Slag Titan", 50, 55, MovePattern.Sequence,
                                      new Color(0.44f, 0.34f, 0.30f), titanHarden, stomp, titanCrush);

            var flareUp = Move("Move_FlareUp", "Flare Up", IntentKind.Attack, 1, Damage("EnemyFx_FlareUp", 4, hits: 3, burnPerHit: 1));
            var consume = Move("Move_Consume", "Consume", IntentKind.Attack, 1,
                               Damage("EnemyFx_Consume", 13), Asset<HealEffect>("EnemyFx_Consume_Heal", e => e.Amount = 6));
            var gutter  = Move("Move_Gutter", "Gutter", IntentKind.Buff, 1,
                               Block("EnemyFx_Gutter", 12), Status("EnemyFx_Gutter_Strength", StatusType.Strength, 1, toSelf: true));
            var livingFlame = MakeEnemy("living_flame", "Living Flame", 66, 72, MovePattern.Sequence,
                                        new Color(1f, 0.55f, 0.22f), flareUp, consume, gutter);

            // The Emberheart. The run's last fight, and the only enemy that does all three of the act's
            // things: it grows, it hits through Block with Burn, and it heals itself once a cycle. Its
            // pattern is six turns long so the player can learn it and plan two turns ahead, which is the
            // only way a fight this size can be won rather than survived.
            var pulse     = Move("Move_Pulse", "Pulse", IntentKind.Debuff, 1,
                                 Status("EnemyFx_Pulse_Weak", StatusType.Weak, 1), Status("EnemyFx_Pulse_Vulnerable", StatusType.Vulnerable, 1),
                                 Heat("EnemyFx_Pulse_Heat", 4));
            var eruption  = Move("Move_Eruption", "Eruption", IntentKind.Attack, 1, Damage("EnemyFx_Eruption", 4, hits: 4, burnPerHit: 1));
            var cataclysm = Move("Move_Cataclysm", "Cataclysm", IntentKind.Attack, 1, Damage("EnemyFx_Cataclysm", 17));
            var reforge   = Move("Move_Reforge", "Reforge", IntentKind.Buff, 1,
                                 Block("EnemyFx_Reforge", 18), Asset<HealEffect>("EnemyFx_Reforge_Heal", e => e.Amount = 8),
                                 Status("EnemyFx_Reforge_Strength", StatusType.Strength, 1, toSelf: true));
            var emberheart = MakeEnemy("emberheart", "The Emberheart", 150, 160, MovePattern.Sequence,
                                       new Color(1f, 0.42f, 0.18f), pulse, eruption, cataclysm, reforge, eruption, cataclysm);

            var encounters = new List<EncounterData>
            {
                Encounter("embers_and_rat", EncounterTier.Early, emberling, cinderRat),
                Encounter("mite_swarm", EncounterTier.Early, ashMite, ashMite, ashMite),
                Encounter("kiln_imp", EncounterTier.Early, kilnImp),
                Encounter("slag_beetle", EncounterTier.Early, slagBeetle),

                Encounter("wraith_and_emberling", EncounterTier.Late, ashWraith, emberling),
                Encounter("cultists", EncounterTier.Late, cultist, cultist),
                Encounter("beetle_and_mite", EncounterTier.Late, slagBeetle, ashMite),
                Encounter("imp_and_emberling", EncounterTier.Late, kilnImp, emberling),

                Encounter("hound_pack", EncounterTier.Elite, emberling, ashHound),
                Encounter("molten_golem", EncounterTier.Elite, golem),
                Encounter("salamanders", EncounterTier.Elite, salamander, salamander),

                Encounter("forge_tyrant", EncounterTier.Boss, tyrant),

                // Act 2. The early pool gained an extra Wisp in two fights: a player arrives here at full
                // health with a deck that has won ten fights, and at one enemy fewer these rows killed 0-1% of
                // simulated runs and cost 2-6 HP — the act's first three floors were a formality.
                EncounterIn(2, "wisp_trio", EncounterTier.Early, emberWisp, emberWisp, emberWisp),
                EncounterIn(2, "leech_pair", EncounterTier.Early, magmaLeech, magmaLeech, emberWisp),
                EncounterIn(2, "obsidian_sentinel", EncounterTier.Early, sentinel, emberWisp),
                EncounterIn(2, "shaman_and_wisp", EncounterTier.Early, shaman, emberWisp, emberWisp),

                EncounterIn(2, "knight_and_wisp", EncounterTier.Late, ashenKnight, emberWisp),
                EncounterIn(2, "shaman_and_leech", EncounterTier.Late, shaman, magmaLeech),
                EncounterIn(2, "sentinel_and_shaman", EncounterTier.Late, sentinel, shaman),
                EncounterIn(2, "wisps_and_leech", EncounterTier.Late, emberWisp, emberWisp, magmaLeech),

                EncounterIn(2, "obsidian_colossus", EncounterTier.Elite, colossus),
                EncounterIn(2, "knight_and_wisps", EncounterTier.Elite, ashenKnight, emberWisp, emberWisp),
                EncounterIn(2, "shaman_leech_wisp", EncounterTier.Elite, shaman, magmaLeech, emberWisp),

                EncounterIn(2, "cinder_wyrm", EncounterTier.Boss, wyrm),

                // Act 3. Same correction as Act 2's early pool, for the same reason: an extra Emberfly in three
                // early fights and in Priest and Revenant, which had been costing a median of 1 HP to win.
                EncounterIn(3, "emberfly_swarm", EncounterTier.Early, emberfly, emberfly, emberfly),
                EncounterIn(3, "revenant_pair", EncounterTier.Early, revenant, revenant, emberfly),
                EncounterIn(3, "maw_and_fly", EncounterTier.Early, moltenMaw, emberfly, emberfly),
                EncounterIn(3, "priest_and_fly", EncounterTier.Early, ashPriest, emberfly, emberfly),

                EncounterIn(3, "titan", EncounterTier.Late, slagTitan),
                EncounterIn(3, "priest_and_revenant", EncounterTier.Late, ashPriest, revenant, emberfly),
                EncounterIn(3, "maw_and_priest", EncounterTier.Late, moltenMaw, ashPriest),
                EncounterIn(3, "revenant_and_flies", EncounterTier.Late, revenant, emberfly, emberfly),

                EncounterIn(3, "living_flame", EncounterTier.Elite, livingFlame),
                EncounterIn(3, "titan_and_priest", EncounterTier.Elite, slagTitan, ashPriest),
                EncounterIn(3, "maws", EncounterTier.Elite, moltenMaw, moltenMaw, emberfly),

                EncounterIn(3, "emberheart", EncounterTier.Boss, emberheart),
            };

            var emberCore = Asset<EmberCoreRelic>("Relics/Relic_EmberCore", relic =>
            {
                relic.Id = "ember_core";
                relic.DisplayName = "Ember Core";
                relic.Description = "The first attack you play each turn deals 3 additional damage.";
                relic.BonusDamage = 3;
            });

            // Elite rewards. Every one is an existing effect or Power: a relic is something the
            // player already owns when the fight starts, so none needs engine code of its own.
            var relicPool = new List<RelicData>
            {
                RelicAsset("whetstone_charm", "Whetstone Charm", "Start each combat with 1 Strength.", str1Self),
                RelicAsset("tempered_buckler", "Tempered Buckler", "Start each combat with 1 Dexterity.",
                           Status("Dex_1_Relic", StatusType.Dexterity, 1, toSelf: true)),
                RelicAsset("bellows_heart", "Bellows Heart", "At the start of your turn, gain 1 Heat.",
                           Power("Pow_Relic_BellowsHeart", PowerTrigger.TurnStart,
                                 "At the start of your turn, gain 1 Heat.", false, heat1)),
                RelicAsset("iron_ward", "Iron Ward", "At the start of your turn, gain 3 Block.",
                           Power("Pow_Relic_IronWard", PowerTrigger.TurnStart,
                                 "At the start of your turn, gain 3 Block.", false, blk3)),
                RelicAsset("thermal_core", "Thermal Core", "Your Overheat threshold is 4 higher.",
                           Asset<RuleChangeEffect>("Rule_Relic_ThermalCore", e =>
                           {
                               e.OverheatThresholdDelta = 4;
                               e.Text = "Your Overheat threshold increases by 4.";
                           })),
                RelicAsset("cinder_ring", "Cinder Ring", "Whenever you Exhaust a card, gain 3 Block.",
                           Power("Pow_Relic_CinderRing", PowerTrigger.CardExhausted,
                                 "Whenever you Exhaust a card, gain 3 Block.", false, blk3)),
                RelicAsset("kindling_pouch", "Kindling Pouch", "At the start of each combat, apply 3 Burn to ALL enemies.",
                           Status("Burn_3_Relic", StatusType.Burn, 3)),
                // The Emberheart's relic: only its elites and boss grant it.
                Asset<HeartstoneRelic>("Relics/Relic_Heartstone", relic =>
                {
                    relic.Id = "heartstone";
                    relic.DisplayName = "Heartstone";
                    relic.Description = "While you are overheated, your attacks deal 3 more damage.";
                    relic.BonusDamage = 3;
                    relic.MinAct = 3;
                }),
            };

            // Relics earned between runs: never in the pool above until unlocked.
            var obsidianShard = RelicAsset("obsidian_shard", "Obsidian Shard", "At the start of each combat, apply 1 Weak to ALL enemies.",
                                           Status("Unlock_Relic_Weak_1", StatusType.Weak, 1));
            var forgeApron = RelicAsset("forge_apron", "Forge Apron", "At the end of your turn, gain 2 Block.",
                                        Power("Pow_Relic_ForgeApron", PowerTrigger.TurnEnd, "At the end of your turn, gain 2 Block.", false, unlockBlk2));
            var anvilCrown = RelicAsset("anvil_crown", "Anvil Crown", "Whenever you gain Heat, gain 1 Block.",
                                        Power("Pow_Relic_AnvilCrown", PowerTrigger.HeatGained, "Whenever you gain Heat, gain 1 Block.", false, blk1));

            // ── Potions ─────────────────────────────────────────────────────────────
            // Every potion is an existing effect used without a card. Colour carries the kind: red
            // heals, blue guards, orange burns, gold empowers, green draws, purple weakens.
            var potions = new List<PotionData>
            {
                PotionAsset("healing_draught", "Healing Draught", TargetMode.Self, "potion_red",
                            Asset<HealEffect>("Potion_Heal_15", e => e.Amount = 15)),
                PotionAsset("iron_tonic", "Iron Tonic", TargetMode.Self, "potion_blue", Block("Potion_Block_14", 14)),
                PotionAsset("fire_flask", "Fire Flask", TargetMode.AllEnemies, "potion_orange", Damage("Potion_Fire_8", 8)),
                PotionAsset("burning_oil", "Burning Oil", TargetMode.SingleEnemy, "potion_orange",
                            Status("Potion_Burn_7", StatusType.Burn, 7)),
                PotionAsset("strength_brew", "Strength Brew", TargetMode.Self, "potion_gold",
                            Status("Potion_Strength_2", StatusType.Strength, 2, toSelf: true)),
                PotionAsset("energy_draught", "Energy Draught", TargetMode.Self, "potion_gold",
                            Asset<GainEnergyEffect>("Potion_Energy_2", e => e.Amount = 2)),
                PotionAsset("swift_elixir", "Swift Elixir", TargetMode.Self, "potion_green", Draw("Potion_Draw_3", 3)),
                PotionAsset("weakening_ash", "Weakening Ash", TargetMode.SingleEnemy, "potion_purple",
                            Status("Potion_Weak_1", StatusType.Weak, 1), Status("Potion_Vulnerable_2", StatusType.Vulnerable, 2)),
            };

            // ── Run config ──────────────────────────────────────────────────────────
            var config = CreateAsset<RunConfig>(ConfigPath, cfg =>
            {
                cfg.PlayerName = "Ember";
                // 50, down from 63. The enemies were authored against a drafted deck that was two fifths
                // Strike and Guard; a deck of thirty cards the player chose is about six times as strong,
                // and at 63 HP the suggested deck won half of all simulated runs. Player health rather
                // than enemy damage, because an enemy's announced number has to stay exactly what lands.
                cfg.MaxHp = 50;
                cfg.EnergyPerTurn = 3;
                // 5, not 6. Tested once the deck grew to thirty built cards, on the theory that a bigger
                // deck wants a bigger hand: one extra card per turn took the simulated win rate from
                // 3.3-5.0% to 17.7-23.3%. A sixth card is not a sixth more power — it is another Block
                // every turn, and the whole game is built on there not being one.
                cfg.CardsPerTurn = 5;

                // The designed 10-card opener. The two singletons are seeds, not filler:
                // they are the player's first taste of Pyre and Overdrive, so the first
                // reward screen has something to point at.
                cfg.StarterDeck = new List<RunConfig.DeckEntry>
                {
                    Entry(strike, 4),
                    Entry(guard, 4),
                    Entry(emberLash, 1),
                    Entry(stoke, 1)
                };

                cfg.RewardPool = cards.FindAll(c => c.Rarity != CardRarity.Starter);
                cfg.AllCards = new List<CardData>(cards);
                cfg.AllCards.AddRange(unlockCards);
                // Upgraded cards are in the lookup, so a save can name them, but not in the
                // reward pool: rewards offer base cards, and upgrading is the rest site's job.
                cfg.AllCards.AddRange(upgrades);
                // 6%, not 18%: the scaling compounds per fight, and 18% put row-eight enemies at 2.4x
                // their base health. It was 8% while a run started from the ten-card starter deck; a run
                // that starts from thirty built cards is harder, not easier — the deck the player chose is
                // diluted by every reward on top of it — and at 8% the simulated win rate fell from
                // 5.0-7.3% to 3.3-4.3%. 6% puts it back in the band the rest of the game was tuned at.
                cfg.EnemyScalingPerFight = 0.06f;
                // 0.92: the bestiary was authored against the ten-card starter deck, which a fight cycles
                // through two or three times. A thirty-card deck shows two thirds of itself across a whole
                // fight, so the same enemy takes far longer to kill with the same cards. Swept at 5 cards
                // per turn and no hallway card rewards: 1.0 gave 0.7-2.3% run wins, 0.8 gave 12.7-24.0%,
                // and 0.92 gives 3.3-8.3% — the band the game was tuned at before decks were built.
                // 1.15, with the 50 HP above. Swept together against the suggested deck: at 63 HP and 0.92
                // it won 47-53% of runs, at 50 HP and 1.15 it wins 3.7-7.7% — the band this game has been
                // tuned at since the first act existed. See docs/deck-building.md for the whole table.
                cfg.EnemyHpFactor = 1.15f;
                // Act 2 grows more slowly. At 8% its first rows killed almost nobody while its last rows
                // killed a fifth of runs: the difficulty sat in the multiplier, not in the enemies. 4%
                // keeps the climb and leaves the enemies themselves to carry the act.
                // Act 3 grows slowest of the three. Its enemies are authored at the strength of a deck
                // that has already won two acts, and a multiplier on top of that only decides the run in
                // the last three rows.
                cfg.ActScalingPerFight = new List<float> { 0.06f, 0.04f, 0.03f };
                cfg.Relics = new List<RelicData> { emberCore };
                cfg.RelicPool = relicPool;
                cfg.PotionPool = potions;
                // The hallway fight is two enemies. The first full-run simulation showed the
                // old three-enemy group was elite difficulty wearing a hallway's name: the
                // starter deck won it 83% of the time but finished at 16 of 63 HP, and with
                // health carrying between fights that made the second fight lethal. Zero of
                // 900 simulated runs reached the boss.
                cfg.Encounter = new List<EnemyData> { emberling, cinderRat };
                // The elite is two of the hardest normal enemy rather than a new creature:
                // the threat is legible before the player commits to the node.
                // Two strong enemies, not the old three. Greedy map play — taking every elite
                // it could — reached the boss less often (19%) than cautious play (30%): an
                // elite that costs more than it pays is not a risk, it is a mistake the map
                // offers.
                // Emberling rather than Cinder Rat beside the hound. A simulated elite paying two
                // cards instead of one changed nothing, which says the lever is not the size of
                // the reward: a won elite cost 46% of max HP, and no card offsets that. So the
                // elite gets cheaper in health instead.
                cfg.EliteEncounter = new List<EnemyData> { emberling, ashHound };
                cfg.BossEncounter = new List<EnemyData> { tyrant };
                cfg.Encounters = encounters;
                cfg.Acts = 3;
                // Thresholds in lifetime Embers. A first run that dies on floor 6 earns about 8, a loss at the
                // Tyrant about 15, a win about 60: the first unlock inside two runs, the last after roughly a
                // dozen. See docs/meta-progression.md.
                cfg.Unlocks = new List<UnlockData>
                {
                    Unlock("kiln_cards", "Kiln cards", 10, cards: new[] { crucible, brand, kilnGuard }),
                    Unlock("obsidian_shard", "Obsidian Shard", 25, relic: obsidianShard),
                    Unlock("tempest_cards", "Tempest cards", 45, cards: new[] { emberScatter, bladeDance, momentum }),
                    Unlock("forge_apron", "Forge Apron", 70, relic: forgeApron),
                    Unlock("inferno_cards", "Inferno cards", 100, cards: new[] { magmaHeart, supernova, phoenixPlume }),
                    Unlock("anvil_crown", "Anvil Crown", 140, relic: anvilCrown),
                };
                cfg.ActNames = new List<string> { "The Forge", "The Obsidian Deep", "The Emberheart" };
                cfg.EarlyRows = 3;
                cfg.RestHealFraction = 0.3f;
                // Enough for one removal or one common at the first shop, not both.
                cfg.StartingGold = 75;
            });

            _ = new[] { focus, cinderStorm };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EmberDeck] Generated {cards.Count} cards.");
            CreateScene(config);
        }

        // ── Scene ────────────────────────────────────────────────────────────────────

        static void CreateScene(RunConfig config)
        {
            config = AssetDatabase.LoadAssetAtPath<RunConfig>(ConfigPath) ?? config;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Background;

            var combatGo = new GameObject("Combat");
            var view = combatGo.AddComponent<CombatView>();

            // Assign the field directly rather than through SerializedObject: in batch mode
            // ApplyModifiedPropertiesWithoutUndo silently fails to write an object reference
            // — no exception, no false return, just a null in the saved scene.
            view.EditorBindConfig(config);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Verify against the SAVED file, not the in-memory object: what matters is what
            // a build loads, and the in-memory check proved to be a false negative here.
            bool bound = File.ReadAllText(ScenePath).Contains(AssetDatabase.AssetPathToGUID(ConfigPath));
            if (!bound)
            {
                Debug.LogError("[EmberDeck] RunConfig is NOT bound in the saved scene — the build would start empty.");
                EditorApplication.Exit(1);
                return;
            }

            var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!buildScenes.Exists(entry => entry.path == ScenePath))
            {
                buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }

            Debug.Log("[EmberDeck] Content and scene generated. Open Assets/EmberDeck/Scenes/Combat.unity and press Play.");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        static RunConfig.DeckEntry Entry(CardData card, int count) => new() { Card = card, Count = count };

        static UnlockData Unlock(string id, string name, int threshold, CardData[] cards = null, RelicData relic = null)
        {
            var unlock = new UnlockData { Id = id, DisplayName = name, Threshold = threshold };
            if (cards != null) unlock.Cards.AddRange(cards);
            if (relic != null) unlock.Relics.Add(relic);
            return unlock;
        }

        // ── Upgrades ─────────────────────────────────────────────────────────────────
        //
        // Upgrades are generated by rule rather than written sixty times by hand. The rule is
        // small enough to hold in your head, every card gets an upgrade the moment it exists,
        // and all upgrades stay consistent with one another — hand-authored ones drift.
        //
        //   numbers improve  : damage and block +50% (between +2 and +8), statuses +1 (Burn a third, min 2),
        //                      draw +1, heal +3, Heat +1, multipliers and ratios +0.5, per-card a third
        //   Powers           : cost 1 less
        //   nothing numeric  : cost 1 less, or draw a card if it already costs 0

        static readonly Dictionary<CardEffect, CardEffect> UpgradeCache = new();

        static int Bump(int amount) => Mathf.Clamp(Mathf.RoundToInt(amount * 0.5f), 2, 8);

        /// <summary>An improved copy of the effect, or null when it has no number to improve.</summary>
        static CardEffect UpgradeEffect(CardEffect effect)
        {
            if (effect == null) return null;
            // Effects are shared between cards; cache so a shared effect gets one upgraded asset.
            if (UpgradeCache.TryGetValue(effect, out var cached)) return cached;

            string name = effect.name + "_Up";
            CardEffect result = effect switch
            {
                DealDamageEffect d => Asset<DealDamageEffect>(name, e =>
                {
                    // Multi-hit cards gain per hit, since Strength and Vulnerable already
                    // multiply every hit — a +50% bump on each would scale far too fast.
                    e.Amount = d.Hits > 1 ? d.Amount + 1 : d.Amount + Bump(d.Amount);
                    e.Hits = d.Hits;
                    e.PerHitStatus = d.PerHitStatus;
                    e.PerHitStatusAmount = d.PerHitStatusAmount;
                }),
                GainBlockEffect b => Asset<GainBlockEffect>(name, e => e.Amount = b.Amount + Bump(b.Amount)),
                ApplyStatusEffect s => Asset<ApplyStatusEffect>(name, e =>
                {
                    e.Status = s.Status;
                    // Burn is applied in large stacks, so a flat +2 meant Kindle 5 -> 7 but
                    // Wildfire only 8 -> 10. A third of the stack, at least 2, keeps both worth it.
                    e.Amount = s.Amount + (s.Status == StatusType.Burn
                        ? Mathf.Max(2, Mathf.RoundToInt(s.Amount / 3f))
                        : 1);
                    e.ApplyToSelf = s.ApplyToSelf;
                }),
                DrawCardsEffect dr => Asset<DrawCardsEffect>(name, e => e.Amount = dr.Amount + 1),
                HealEffect h => Asset<HealEffect>(name, e => e.Amount = h.Amount + 3),
                GainHeatEffect gh => Asset<GainHeatEffect>(name, e => e.Amount = gh.Amount + 1),
                SpendHeatEffect sh => Asset<SpendHeatEffect>(name, e =>
                {
                    e.Payout = sh.Payout;
                    e.Ratio = sh.Ratio + 0.5f;
                    e.MaxSpent = sh.MaxSpent;
                    e.AllEnemies = sh.AllEnemies;
                }),
                ScaleWithHeatEffect sw => Asset<ScaleWithHeatEffect>(name, e =>
                {
                    e.Payout = sw.Payout;
                    e.Base = sw.Base + 3;
                }),
                DamageFromBlockEffect db => Asset<DamageFromBlockEffect>(name, e =>
                {
                    e.Multiplier = db.Multiplier + 0.5f;
                    e.ConsumeBlock = db.ConsumeBlock;
                }),
                DamageFromStatusEffect ds => Asset<DamageFromStatusEffect>(name, e =>
                {
                    e.Status = ds.Status;
                    e.Multiplier = ds.Multiplier + 0.5f;
                }),
                ScaleWithCounterEffect sc => Asset<ScaleWithCounterEffect>(name, e =>
                {
                    e.Counter = sc.Counter;
                    e.Payout = sc.Payout;
                    // A flat +1 took Ash Armor from 10 to 11 per card — not worth a rest site.
                    e.PerPoint = sc.PerPoint + Mathf.Max(1, Mathf.RoundToInt(sc.PerPoint / 3f));
                }),
                _ => null,
            };

            UpgradeCache[effect] = result;
            return result;
        }

        static List<CardData> BuildUpgrades(List<CardData> baseCards, CardEffect drawOne)
        {
            var upgraded = new List<CardData>();
            int fallbacks = 0, identical = 0;

            foreach (var card in baseCards)
            {
                var effects = new List<CardEffect>();
                int cost = card.Cost;
                bool improved = false;

                if (card.Type == CardType.Power)
                {
                    effects.AddRange(card.Effects);
                    cost = Mathf.Max(0, card.Cost - 1);
                    improved = cost != card.Cost;
                }
                else
                {
                    foreach (var effect in card.Effects)
                    {
                        var better = UpgradeEffect(effect);
                        if (better != null) { effects.Add(better); improved = true; }
                        else effects.Add(effect);
                    }
                }

                if (!improved)
                {
                    fallbacks++;
                    if (card.Cost > 0) cost = card.Cost - 1;
                    else effects.Add(drawOne);
                }

                var plus = CreateAsset<CardData>($"{ContentRoot}/Cards/Card_{card.Id}_plus.asset", data =>
                {
                    data.Id = card.Id + "_plus";
                    data.DisplayName = card.DisplayName + "+";
                    data.Type = card.Type;
                    data.Rarity = card.Rarity;
                    data.Cost = cost;
                    data.Target = card.Target;
                    data.Exhaust = card.Exhaust;
                    data.TintColor = card.TintColor;
                    data.Art = card.Art;
                    data.Effects = effects;
                    data.IsUpgraded = true;
                    data.Upgrade = null;
                });

                card.Upgrade = plus;
                EditorUtility.SetDirty(card);

                // An upgrade that changes neither cost nor text is a wasted rest site.
                if (plus.Cost == card.Cost && plus.BuildDescription() == card.BuildDescription())
                {
                    identical++;
                    Debug.LogError($"[EmberDeck] Upgrade of {card.Id} is identical to the base card.");
                }
                upgraded.Add(plus);
            }

            Debug.Log($"[EmberDeck] Upgrades: {upgraded.Count} generated, {fallbacks} by fallback, "
                      + $"{identical} identical to base (must be 0)");
            return upgraded;
        }

        static PotionData PotionAsset(string id, string name, TargetMode target, string icon, params CardEffect[] effects) =>
            Asset<PotionData>($"Potions/Potion_{id}", potion =>
            {
                potion.Id = id;
                potion.DisplayName = name;
                potion.Target = target;
                potion.Icon = icon;
                potion.Effects = new List<CardEffect>(effects);
            });

        static EffectRelic RelicAsset(string id, string name, string description, params CardEffect[] effects) =>
            Asset<EffectRelic>($"Relics/Relic_{id}", relic =>
            {
                relic.Id = id;
                relic.DisplayName = name;
                relic.Description = description;
                relic.Effects = new List<CardEffect>(effects);
            });

        static CardData FromAct(int act, CardData card)
        {
            card.MinAct = act;
            EditorUtility.SetDirty(card);
            return card;
        }

        static CardData Exhausting(CardData card)
        {
            card.Exhaust = true;
            EditorUtility.SetDirty(card);
            return card;
        }

        static DealDamageEffect Damage(string file, int amount, int hits = 1, int burnPerHit = 0) =>
            Asset<DealDamageEffect>(file, effect =>
            {
                effect.Amount = amount;
                effect.Hits = hits;
                effect.PerHitStatus = StatusType.Burn;
                effect.PerHitStatusAmount = burnPerHit;
            });

        static GainBlockEffect Block(string file, int amount) =>
            Asset<GainBlockEffect>(file, effect => effect.Amount = amount);

        static GainHeatEffect Heat(string file, int amount) =>
            Asset<GainHeatEffect>(file, effect => effect.Amount = amount);

        static ApplyStatusEffect Status(string file, StatusType status, int amount, bool toSelf = false) =>
            Asset<ApplyStatusEffect>(file, effect =>
            {
                effect.Status = status;
                effect.Amount = amount;
                effect.ApplyToSelf = toSelf;
            });

        static DrawCardsEffect Draw(string file, int amount) =>
            Asset<DrawCardsEffect>(file, effect => effect.Amount = amount);

        static TriggeredPowerEffect Power(string file, PowerTrigger trigger, string text, bool perEnemy,
                                          params CardEffect[] effects) =>
            Asset<TriggeredPowerEffect>(file, power =>
            {
                power.Trigger = trigger;
                power.Text = text;
                power.PerEnemy = perEnemy;
                power.Effects = new List<CardEffect>(effects);
            });

        static CardData Card(List<CardData> into, string id, string name, CardType type, int cost,
                             TargetMode target, CardRarity rarity, params CardEffect[] effects)
        {
            var tint = type switch
            {
                CardType.Attack => AttackTint,
                CardType.Power  => PowerTint,
                _               => SkillTint
            };

            var card = CreateAsset<CardData>($"{ContentRoot}/Cards/Card_{id}.asset", data =>
            {
                data.Id = id;
                data.DisplayName = name;
                data.Type = type;
                data.Cost = cost;
                data.Target = target;
                data.TintColor = tint;
                data.Rarity = rarity;
                data.Exhaust = false;
                data.Effects = new List<CardEffect>(effects);
                data.Art = LoadArt($"{CardArtPath}/{id}.png");
            });
            into.Add(card);
            return card;
        }

        static EnemyMove Move(string file, string label, IntentKind kind, int weight, params CardEffect[] effects) =>
            CreateAsset<EnemyMove>($"{ContentRoot}/Enemies/{file}.asset", move =>
            {
                move.Label = label;
                move.Kind = kind;
                move.Weight = weight;
                move.Effects = new List<CardEffect>(effects);
            });

        static EncounterData Encounter(string id, EncounterTier tier, params EnemyData[] enemies) =>
            EncounterIn(1, id, tier, enemies);

        static EncounterData EncounterIn(int act, string id, EncounterTier tier, params EnemyData[] enemies) =>
            CreateAsset<EncounterData>($"{ContentRoot}/Enemies/Encounter_{id}.asset", encounter =>
            {
                encounter.Id = id;
                encounter.Tier = tier;
                encounter.Act = act;
                encounter.Enemies = new List<EnemyData>(enemies);
            });

        static EnemyData MakeEnemy(string id, string name, int minHp, int maxHp, MovePattern pattern, Color tint,
                                   params EnemyMove[] moves) =>
            CreateAsset<EnemyData>($"{ContentRoot}/Enemies/Enemy_{id}.asset", enemy =>
            {
                enemy.Id = id;
                enemy.DisplayName = name;
                enemy.MinHp = minHp;
                enemy.MaxHp = maxHp;
                enemy.Pattern = pattern;
                enemy.TintColor = tint;
                enemy.Moves = new List<EnemyMove>(moves);
                enemy.Art = LoadArt($"{EnemyArtPath}/{id}.png");
            });

        /// <summary>
        /// Art is optional: the game has to run before the icons exist, and a missing file
        /// should degrade to the coloured placeholder rather than fail the whole generation.
        /// </summary>
        static Sprite LoadArt(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[EmberDeck] No art at {path}");
            return sprite;
        }

        /// <summary>Effect assets, keyed by a short name under Content/Effects.</summary>
        static T Asset<T>(string name, System.Action<T> configure) where T : ScriptableObject
        {
            string path = name.Contains("/")
                ? $"{ContentRoot}/{name}.asset"
                : $"{ContentRoot}/Effects/{name}.asset";
            return CreateAsset(path, configure);
        }

        /// <summary>
        /// Reuses the existing asset when there is one, so regenerating never breaks a
        /// reference held by something the generator does not own.
        /// </summary>
        static T CreateAsset<T>(string path, System.Action<T> configure) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            configure(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// Deletes generated assets before regenerating. Without this, a renamed or dropped
        /// card stays on disk forever and quietly keeps appearing in reward pools — the kind
        /// of bug that looks like a design mistake rather than a tooling one.
        /// </summary>
        static void PurgeStaleContent()
        {
            foreach (var folder in new[] { ContentRoot + "/Cards", ContentRoot + "/Effects", ContentRoot + "/Enemies" })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { folder }))
                    AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            }
            AssetDatabase.Refresh();
        }

        static void EnsureFolders()
        {
            foreach (var folder in new[]
                     {
                         Root, ContentRoot,
                         ContentRoot + "/Effects", ContentRoot + "/Cards",
                         ContentRoot + "/Enemies", ContentRoot + "/Relics", ContentRoot + "/Potions",
                         Root + "/Scenes"
                     })
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                var parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
            AssetDatabase.Refresh();
        }
    }
}
