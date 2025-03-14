using System;

namespace EmpyrionPlaytimeRewardsShop
{
    public class PlayerData
    {
        public DateTime loginTimestamp { get; set; } = DateTime.Now;
        public int Points { get; set; } = 0;
    }
}
