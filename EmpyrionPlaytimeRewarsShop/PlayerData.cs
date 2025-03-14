using System;

namespace EmpyrionPlaytimeRewardsShop
{
    public class PlayerData
    {
        public DateTime loginTimestamp { get; set; } = DateTime.UtcNow;
        public int Points { get; set; } = 0;
    }
}
