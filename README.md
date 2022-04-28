# CompetitiveServer
 A plugin for <a href=https://github.com/Corecii>Corecii's</a> <a href=https://github.com/Corecii/Distance-Server>Distance-Server</a> tool.
 
 This implements the Glicko2 ranking system to give rankings to players who race each other on the server. Players get ranked at the end of a match and it will remember a player's score even if they leave.

# Setup
If you're already familiar with the setup of the Distance-Server then that's great! If you don't then <a href=https://github.com/Corecii/Distance-Server>you really should go here before you do anything with this plugin.</a> 

Download the latest zip file in the releases page. <br />
The `Glicko2Rankings.dll` should be moved into the `Plugins` folder within your `DistanceServer` directory. <br />
Add the `Glicko2Rankings.dll` directory to the `Server.json` located in the `config` folder. <br />
The `0WorkshopSearch.dll` should be moved in the same place, replace the one that is there with this one. <br />
`BasicAutoServer.json` should be moved into the `config` folder within your `DistanceServer` directory. Replace the json that is in there already if needed. You will need to edit this file to your own preferences, but what is already written is preferred. There should not be anything in the `Workshop` section of the json. The Glicko2Rankings plugin will find the competitive levels on its own. If you don't understand what you're doing, see Corecii's <a href=https://github.com/Corecii/Distance-Server/blob/master/PLUGINS.md>PLUGINS.md</a> for configuration explanations.

In your `Server.json` in the `config` folder, you should remove the `VoteCommands.dll` directory.<br />
Why? <br />
Well ideally, the competitive server should be able to pick from a 'competitively viable' set of levels on its own. Allowing players to vote would also allow players to pick levels that are not 'competitively viable' or pick levels that they are specifically good at. There should not be a bias in the level select. <br />

That's it! Your competitive server should be all set.

# Known Issues
* The XML file is only saved locally. The data is not saved on a server anywhere so if multiple competitive servers are running they will have different rankings. This creates the risk of losing the XML somehow as well.
* The server always starts with Diversion no matter what
