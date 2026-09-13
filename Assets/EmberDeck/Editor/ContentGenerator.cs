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

        static readonly Color AttackTint = new(0.86f, 0.42f, 0.28f);
        static readonly Color SkillTint  = new(0.38f, 0.60f, 0.82f);
        static readonly Color PowerTint  = new(0.72f, 0.52f, 0.85f);

        [MenuItem("EmberDeck/Generate Content and Scene")]
        public static void Generate()
        {
            EnsureFolders();
            PurgeStaleContent();

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
            Card(cards, "cremate", "Cremate", CardType.Skill, 0, TargetMode.Self, CardRarity.Common, exhaustTop, energy1);
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
            Card(cards, "overclock", "Overclock", CardType.Skill, 0, TargetMode.Self, CardRarity.Uncommon, heat5, draw2);
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

            // ── Enemies ─────────────────────────────────────────────────────────────
            var bite    = Move("Move_Bite", "Bite", IntentKind.Attack, 1, Damage("Dmg_Bite", 10));
            var skitter = Move("Move_Skitter", "Skitter", IntentKind.Block, 1, Block("Blk_Skitter", 11));
            var maul    = Move("Move_Maul", "Maul", IntentKind.Attack, 3, Damage("Dmg_Maul", 14));
            var howl    = Move("Move_Howl", "Howl", IntentKind.Buff, 1,
                               Status("Str_Howl_Self", StatusType.Strength, 3, toSelf: true));
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

            var emberCore = Asset<EmberCoreRelic>("Relics/Relic_EmberCore", relic =>
            {
                relic.Id = "ember_core";
                relic.DisplayName = "Ember Core";
                relic.Description = "The first attack you play each turn deals 3 additional damage.";
                relic.BonusDamage = 3;
            });

            // ── Run config ──────────────────────────────────────────────────────────
            var config = CreateAsset<RunConfig>(ConfigPath, cfg =>
            {
                cfg.PlayerName = "Ember";
                cfg.MaxHp = 63;
                cfg.EnergyPerTurn = 3;
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

                cfg.Relics = new List<RelicData> { emberCore };
                cfg.Encounter = new List<EnemyData> { emberling, cinderRat, ashHound };
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
            });

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
                         ContentRoot + "/Enemies", ContentRoot + "/Relics",
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
