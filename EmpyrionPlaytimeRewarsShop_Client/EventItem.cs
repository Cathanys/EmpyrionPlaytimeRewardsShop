using Eleon.Modding;

namespace EmpyrionPlaytimeRewardsShop_Client
{
    class EventItem
    {
        /// <summary>
        /// number of the event to identify the response
        /// </summary>
        public ushort Nr { get; set; }

        /// <summary>
        /// id of player who request the event
        /// </summary>
        public int PlayerId { get; set; }

        /// <summary>
        /// expected command in the event response
        /// </summary>
        public CmdId CommandID { get; set; }

        /// <summary>
        /// stat to increase from the requested event
        /// </summary>
        public ShopStat Stat { get; set; }
    }
}
