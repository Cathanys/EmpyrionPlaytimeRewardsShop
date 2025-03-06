namespace EmpyrionPlaytimeRewarsShop_Client
{
    public class PlaytimeRewardsShopConfiguration
    {
        public string ChatCommandPrefix { get; set; } = "/\\prs";
        public int RewardPeriodInMinutes { get; set; } = 5;
        public int RewardPointsPerPeriod { get; set; } = 10;
    }
}
