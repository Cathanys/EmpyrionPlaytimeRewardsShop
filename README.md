# EmpyrionPlaytimeRewardsShop

## What is it?
With this mod, players can buy items from their playtime.
This is my first Empyrion mod combining the [Backpack Extender](https://github.com/GitHub-TC/EmpyrionBackpackExtender) and [Playtime Rewards](https://github.com/GitHub-TC/EmpyrionPlaytimeRewards) using the DemoMod as template.
Thanks for all the support from the Empyrion Discord 💜

## Installation

This mod only works on servers. Copy the content of the EmpyrionPlayerRewardsShop_vx_x_X.zip into the folder Content\Mods\
You should have this file structure on your server:
- Content\Mods\
	- EmpyrionPlayerRewardsShop\
 		- EmpyrionPlayerRewardsShop.dll
   		- EmpyrionPlayerRewardsShop_Info.yaml

## Configuration
After starting the server or game with the mod in the correct folder, the configuration will be created here:
\[SaveGamePath\]\\Mods\\EmpyrionPlaytimeRewardsShop\\Configuration.json

For each item you want to add to the shop, add an entry in the item rewards sections.
The item ids are the "Game ID" from the database: https://empyrionbuddy.com (with the correct Scenario activated in their settings)

Currently these stats are implemented:
- "life" - increases the maximum health
- "exp" - increases the players experience points

```json
{
	"ChatCommandPrefix":"/\\prs",
	"RewardPeriodInMinutes":5,
	"RewardPointsPerPeriod":10,
	"RewardItems":
	[
		{"Name":"neo","Description":"Neodynium Ore","quantity":100,"price":100,"itemId":4300},
		{"Name":"sath","Description":"Sathium Ore","quantity":100,"price":100,"itemId":4332}
	],
	"RewardStats":
	[
		{"Name":"life","Description":"Health","quantity":100,"price":100,"maxStat":2000},
		{"Name":"exp","Description":"Experience","quantity":1000,"price":100,"maxStat":500000}
	]
}
```



## Usage
Enter the command into the private or faction chat in your game.

```
\prs help    : shows all available commands in a Window
\prs points  : updates the points for the player
\prs buy neo : buys the item neo with the conditions of the configuration file from the player points
\prs buy life: increases the maximum health of the player with the conditions from the configuration file
```
Source: https://github.com/Cathanys/EmpyrionPlaytimeRewardsShop
