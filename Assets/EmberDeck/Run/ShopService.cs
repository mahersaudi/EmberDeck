using System.Collections.Generic;
using EmberDeck.Content;
using EmberDeck.Content.Relics;
using EmberDeck.Core;

namespace EmberDeck.Run
{
    public sealed class ShopItem
    {
        public CardData Card;
        public int Price;
        public bool OnSale;
        public bool Sold;
    }

    public sealed class ShopPotion
    {
        public PotionData Potion;
        public int Price;
        public bool Sold;
    }

    /// <summary>What one shop has for sale, and what has already been bought from it.</summary>
    public sealed class ShopStock
    {
        public readonly List<ShopItem> Cards = new();
        public RelicData Relic;
        public int RelicPrice;
        public bool RelicSold;
        public int RemovalPrice;
        public bool RemovalUsed;
        public readonly List<ShopPotion> Potions = new();
    }

    /// <summary>
    /// The shop's rules: what is stocked, what it costs, and what buying does.
    ///
    /// Stock is derived from the shop's position on the map, so walking back into a shop after a
    /// restart shows the same shelves. The game's ShopView and RunSimulator both buy through here,
    /// so a price change is measured by the simulator the moment it is made.
    ///
    /// Prices follow what each thing is worth to a deck. A common is efficiency and cheap; a rare
    /// is identity and costs three commons. Removal is the most valuable service in the genre and the
    /// least obvious — every weak starter card removed makes every later draw better — so it starts
    /// cheap enough to try and climbs each time it is used.
    /// </summary>
    public static class ShopService
    {
        public const int CardCount = 5;

        /// <summary>A deck smaller than one hand would draw its whole deck every turn.</summary>
        public const int MinimumDeck = 5;

        public static ShopStock Roll(RunState run, RunConfig config)
        {
            var node = run.ActiveNode;
            var rng = new DeterministicRng(run.Seed ^ unchecked((node?.Row ?? 0) * 0x1B873593 + (node?.Column ?? 0) * 0x68E31DA4) ^ 0x3C6EF372);

            var stock = new ShopStock();
            var cards = RewardService.Roll(config.RewardPool, rng, CardCount);
            // One card at half price: a reason to look at every shelf rather than only at rares.
            int sale = cards.Count > 0 ? rng.Range(0, cards.Count) : -1;
            for (int i = 0; i < cards.Count; i++)
            {
                int price = CardPrice(cards[i].Rarity, rng);
                bool onSale = i == sale;
                stock.Cards.Add(new ShopItem { Card = cards[i], Price = onSale ? price / 2 : price, OnSale = onSale });
            }

            var unowned = new List<RelicData>();
            foreach (var relic in config.RelicPool)
                if (relic != null && !run.Relics.Contains(relic)) unowned.Add(relic);
            if (unowned.Count > 0)
            {
                stock.Relic = unowned[rng.Range(0, unowned.Count)];
                stock.RelicPrice = rng.Range(140, 171);
            }

            stock.RemovalPrice = RemovalPrice(run);

            foreach (var (potion, price) in PotionService.RollShelf(config, rng))
                stock.Potions.Add(new ShopPotion { Potion = potion, Price = price });
            return stock;
        }

        static int CardPrice(CardRarity rarity, DeterministicRng rng) => rarity switch
        {
            CardRarity.Rare     => rng.Range(135, 166),
            CardRarity.Uncommon => rng.Range(68, 83),
            _                   => rng.Range(45, 56),
        };

        public static int RemovalPrice(RunState run) => 75 + 25 * run.CardsRemoved;

        public static bool BuyCard(RunState run, ShopItem item)
        {
            if (item == null || item.Sold || !GoldService.Spend(run, item.Price)) return false;
            item.Sold = true;
            run.AddCard(item.Card);
            run.Stats.CardsAdded++;
            return true;
        }

        public static bool BuyRelic(RunState run, ShopStock stock)
        {
            if (stock.Relic == null || stock.RelicSold || !GoldService.Spend(run, stock.RelicPrice)) return false;
            stock.RelicSold = true;
            run.Relics.Add(stock.Relic);
            return true;
        }

        public static bool BuyPotion(RunState run, ShopPotion item)
        {
            if (item == null || item.Sold || !PotionService.HasRoom(run) || !GoldService.Spend(run, item.Price)) return false;
            item.Sold = true;
            PotionService.TryAdd(run, item.Potion);
            return true;
        }

        /// <summary>One removal per shop, never below the minimum deck size.</summary>
        public static bool RemoveCard(RunState run, ShopStock stock, CardData card)
        {
            if (stock.RemovalUsed || card == null || !run.Deck.Contains(card) || run.Deck.Count <= MinimumDeck) return false;
            if (!GoldService.Spend(run, stock.RemovalPrice)) return false;
            run.Deck.Remove(card);
            run.CardsRemoved++;
            run.Stats.CardsRemoved++;
            stock.RemovalUsed = true;
            return true;
        }
    }
}
