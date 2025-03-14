using System.Collections.Generic;

namespace EmpyrionPlaytimeRewardsShop
{
    public class PlaytimeRewardsShopConfiguration
    {
        public string ChatCommandPrefix { get; set; } = "/\\prs";
        public int RewardPeriodInMinutes { get; set; } = 5;
        public int RewardPointsPerPeriod { get; set; } = 10;
        public List<ShopItem> RewardItems { get; set; } = new List<ShopItem>();
        public List<ShopStat> RewardStats { get; set; } = new List<ShopStat>();
    }

    public class ShopItem
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public int quantity { get; set; }
        public int price { get; set; }

        public int itemId { get; set; }
    }

    public class ShopStat
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public int quantity { get; set; }
        public int price { get; set; }

        public int maxStat { get; set; } // maximum allowed value of this stat
    }
}
