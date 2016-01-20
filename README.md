# auto-playlister

#### Overview
This program monitors frontpage submissions for specific subreddits on reddit.com and updates a spotify playlist accordingly. 

#### Building
Build using Visual Studio (2015) or xbuild (mono)

#### Customizing
All settings that can be configured are found in [settings.xml](https://github.com/rotmoset/auto-playlister/blob/master/src/settings.xml)

#### How do I get an auth token for the spotify api?
To use this program you need an auth token to authenticate the requests to the spotify web api. This token needs to be added to settings.xml

The simplest way to create such a token is to run the Authentication example in the [FSpotify](https://github.com/rotmoset/fspotify) library and copy the output into settings.xml (note that this program already depends on FSpotify).

