using System;

namespace EmpyrionPlaytimeRewarsShop_Client
{
    public class PlayerData
    {
        public DateTime loginTimestamp { get; set; } = DateTime.Now;
        public int Points { get; set; } = 0;
    }
}
