## TerrariaIRC

Ver 2.0.1.0
* Single settings file with optional second channel
* Support for GEO info on Join
- Support for IRC colors (doesn't work)

Ver 2.0.0.0
* Added support for second channel (Chat, Action)
o o Still need to merge two settings files
* Added support to see actions with chests (put, get)
* Changed /(command) and chest to route to ActionChannel
* Added filter for cunlock

Ver 1.2.3.0
* Added display of player commands to irc chat with filters on:
* * register
* * login
* * password

Ver 1.2.2.0
* Added Logged In As (Account) to iinfo
* Added Group to iinfo

Ver 1.2.1.3
* Added player IP to Life command
* Added catch for IRC disconnect due to IRC split

Ver 1.2.1.0
* Added player count to join and leave:
. . Joined [xx]: name (Life/Mana) - item[0](stack)
. . Left [xx]: name

Ver 1.2.0.9
* Added Buff command for Info:
. . IInfo <name> [ Life | Buffs ]

Ver 1.2.0.8
* Added commands for getting player info:
. . IInv  <name> [ Inv <row> | Acc | Arm | Amm ]
. . IInfo <name> [ Life ]

Ver 1.2.0.7
* Added display of Life & Mana after join

Ver 1.2.0.6
* Add retry delay and max retry to connect
* Replaced console messages with log messages

Ver 1.2.0.5
* Block commands with 'superadmin' in them
   (crude filter)

Ver 1.2.0.4
* Send response when irc user doesn't have op level

Ver 1.2.0.3
* Added limit to only op's in irc can run commands

Ver 1.2.0.2
* Fixed exit on server shutdown
* Enabled Remote Commands (! -> /)

Ver 1.2.0.1
* Added Commands.init() call to Initialize()


Ver 1.2.0.0
* Created by DeathMax
