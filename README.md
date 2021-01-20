# auto-playlister

#### Overview
This program monitors frontpage submissions for specific subreddits on reddit.com and updates a spotify playlist accordingly. 

#### Building
Build using Visual Studio

#### Customizing
For the console application, the settings are found in https://github.com/andreasjhkarlsson/auto-playlister/blob/master/src/console/settings.xml

For the AWS lambda runner, check https://github.com/andreasjhkarlsson/auto-playlister/blob/master/src/lambda/Function.fs for the keys and configure them in the aws console.

#### How do I get an auth token for the spotify api?
To use this program you need an auth token to authenticate the requests to the spotify web api. This token needs to be added to settings.xml

The simplest way to create such a token is to run the Authentication example in the [FSpotify](https://github.com/rotmoset/fspotify) library and copy the output into settings.xml (note that this program already depends on FSpotify).

