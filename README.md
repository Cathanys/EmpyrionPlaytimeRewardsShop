# EmpyrionPlaytimeRewardsShop

## What is it?
With this mod, players can buy items from their playtime.
This is my first Empyrion mod combining the [Backpack Extender](https://github.com/GitHub-TC/EmpyrionBackpackExtender) and [Playtime Rewards](https://github.com/GitHub-TC/EmpyrionPlaytimeRewards) Mod for the server application and used the DemoMod for the Client application.
Thanks for all the support from the Empyrion Discord 💜

## Installation
For Server copy the content of the EmpyrionPlayerRewardsShop ZIP into the folder Content\Mods\
For Single Player mode copy the content of the EmpyrionPlayerRewardsShop_Client ZIP into the folder Content\Mods\

## Configuration
After starting the server or game with the mod in the correct folder, the configuration will be created here:
\[SaveGamePath\]\\Mods\\EmpyrionPlaytimeRewardsShop\\Configuration.json

For each item you want to add to the shop, add an entry in the RewardItems section.

```json
{
	"ChatCommandPrefix":"/\\prs",
	"RewardPeriodInMinutes":5,
	"RewardPointsPerPeriod":10,
	"RewardItems":
	[
		{"Name":"neo","Description":"Neodynium Ore","itemId":4300,"quantity":100,"price":100},
		{"Name":"sath","Description":"Sathium Ore","itemId":4332,"quantity":100,"price":100}
	]
}
```

For the item ids enter the "Game ID" from the database: https://empyrionbuddy.com (with the correct Scenario activated in their settings)

## Usage
Enter the command into the server or faction chat in your game.

```
\prs help    : shows all available commands in a Window
\prs points  : updates the points for the player
\prs buy neo : buys the item neo with the conditions of the configuration file from the player points
```
Source: https://github.com/Cathanys/EmpyrionPlaytimeRewardsShop
