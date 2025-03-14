using System;

namespace EmpyrionPlaytimeRewarsShop_Client
{
    public class PlayerData
    {
        public DateTime loginTimestamp { get; set; } = DateTime.UtcNow;
        public int Points { get; set; } = 123;
    }
}
