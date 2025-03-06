using EmpyrionNetAPIDefinitions;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;

namespace EmpyrionPlaytimeRewardsShop
{
    public class PlaytimeRewardsShopConfiguration
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public LogLevel LogLevel { get; set; } = LogLevel.Message;
        public string ChatCommandPrefix { get; set; } = "/\\";
        public int RewardPeriodInMinutes { get; set; } = 5;
        public int RewardPointsPerPeriod { get; set; } = 10;
    }
}
