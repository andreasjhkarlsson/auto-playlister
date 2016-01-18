# auto-playlister

#### Overview
This program monitors frontpage submissions for specific subreddits on reddit.com and updates a spotify playlist accordingly. 

#### Building
Build using Visual Studio (2015) or xbuild (mono)

#### Customizing
All settings that can be configured are found in [Settings.fs](https://github.com/rotmoset/auto-playlister/blob/master/src/Settings.fs)

#### How do I get an auth token for the spotify api?
To use this program you need an auth token to authenticate the requests to the spotify web api. This token needs to be hardcoded into Settings.fs

The simplest way to create such a token is to run the Authentication example in the [FSpotify](https://github.com/rotmoset/fspotify) library and paste the output into Settings.fs (note that this program already depends on FSpotify).

