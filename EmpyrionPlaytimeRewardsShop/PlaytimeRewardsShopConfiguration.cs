using EmpyrionNetAPIDefinitions;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace EmpyrionPlaytimeRewardsShop
{
    public class PlaytimeRewardsShopConfiguration
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public LogLevel LogLevel { get; set; } = LogLevel.Message;
        public string ChatCommandPrefix { get; set; } = "/\\prs";
        public int RewardPeriodInMinutes { get; set; } = 5;
        public int RewardPointsPerPeriod { get; set; } = 10;
        public List<ShopItem> RewardItems { get; set; } = new List<ShopItem>();
    }

    public class ShopItem
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int itemId { get; set; }
        public int quantity { get; set; }
        public int price { get; set; }
    }
}
