using EmpyrionPlaytimeRewarsShop_Client;
using Newtonsoft.Json;
using System.IO;
using System;
using Newtonsoft.Json.Converters;
using System.Diagnostics.Eventing.Reader;

namespace ConsoleApp1
{
    internal class PlaytimeRewardShop
    {

        /// <summary>
        /// string to the folder with the configurations
        /// </summary>
        private string saveGameModPath = string.Empty;

        /// <summary>
        /// shop offers, prices and points progress
        /// </summary>
        private PlaytimeRewardsShopConfiguration configuration = null;

        /// <summary>
        /// name of the config file
        /// </summary>
        private const string configFileName = "config.json";

        /// <summary>
        /// template name of the player data file
        /// </summary>
        private string playerDataFileName = "PlayerData{0}.json";

        public PlaytimeRewardShop()
        {
            saveGameModPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\Empyrion\Mods\";

            // check if the path exists
            if(!Directory.Exists(saveGameModPath))
            {
               DirectoryInfo dirInfo = Directory.CreateDirectory(saveGameModPath);
            }

            // load the mod configuration
            readConfiguration();
        }

        private void readConfiguration()
        {
            string configFilePath = Path.Combine(saveGameModPath, configFileName);
            if (!File.Exists(configFilePath))
            {
                // create a new configuration
                this.configuration = new PlaytimeRewardsShopConfiguration();

                // add a few standard items
                ShopItem newItem = new ShopItem()
                {
                    Name = "neo",
                    Description = "Neodynium Ore",
                    price = 100,
                    quantity = 100,
                    itemId = 4300
                };
                this.configuration.RewardItems.Add(newItem);

                ShopStat newStat = new ShopStat()
                {
                    Name = "life",
                    Description = "Health",
                    price = 100,
                    quantity = 100,
                    maxStat = 2000
                };
                this.configuration.RewardStats.Add(newStat);

                File.WriteAllText(configFilePath, JsonConvert.SerializeObject(this.configuration));
            }
            else
            {
                using (StreamReader configFile = File.OpenText(configFilePath))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    this.configuration = (PlaytimeRewardsShopConfiguration)serializer.Deserialize(configFile, typeof(PlaytimeRewardsShopConfiguration));
                }
            }
        }

        private PlayerData readPlayerData(int playerID)
        {
            PlayerData playerData = null;
            string playerDataFilePath = Path.Combine(saveGameModPath, string.Format(playerDataFileName, playerID));

            if (!File.Exists(playerDataFilePath))
            {
                playerData = new PlayerData();
            }
            else
            {
                using (StreamReader playerDataFile = File.OpenText(playerDataFilePath))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Converters.Add(new JavaScriptDateTimeConverter());
                    serializer.NullValueHandling = NullValueHandling.Ignore;
                    playerData = (PlayerData)serializer.Deserialize(playerDataFile, typeof(PlayerData));

                    if (null == playerData)
                        playerData = new PlayerData();
                }
            }

            return playerData;
        }

        private void savePlayerData(int playerID, PlayerData playerData)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.Converters.Add(new JavaScriptDateTimeConverter());
            serializer.NullValueHandling = NullValueHandling.Ignore;
            using (StreamWriter file = File.CreateText(Path.Combine(saveGameModPath, string.Format(playerDataFileName, playerID))))
            {
                serializer.Serialize(file, playerData);
            }
        }

        public void updateLoginTimestamp(int playerID)
        {
            PlayerData playerData = readPlayerData(playerID);

            playerData.loginTimestamp = DateTime.UtcNow;

            savePlayerData(playerID, playerData);
        }

        public void updatePoints(int playerID)
        {
            PlayerData playerData = readPlayerData(playerID);

            // calculate the points
            TimeSpan timeDiff = DateTime.UtcNow - playerData.loginTimestamp;

            if (timeDiff.TotalSeconds > 60.0)
            { 
                playerData.Points += Convert.ToInt32(timeDiff.TotalSeconds * this.configuration.RewardPointsPerPeriod / (this.configuration.RewardPeriodInMinutes * 60.0));

                // update the timestamp
                playerData.loginTimestamp = DateTime.UtcNow;

                savePlayerData(playerID, playerData);
            }
        }
    }
}
