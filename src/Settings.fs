module Settings

open FSpotify
open FSharp.Data

[<Literal>]
let sample = """<?xml version="1.0" encoding="utf-8" ?>
<setting>
  <authorization>
    <client id="" secret="" />
    <token
        value=""
        refresh=""
        type="Bearer"
        expires="3600" />
  </authorization>
  <jobs>
    <job id="listentothis-hot" refresh="600">
      <playlist user="" id="" limit="100"/>
      <subreddit name="listentothis" pattern="(?&lt;artist&gt;.*?)-+(?&lt;title&gt;.*?)\[" limit="25"/>
    </job>
  </jobs>
</setting>
"""

type Settings = XmlProvider<sample>

let settings = Settings.Load "settings.xml"
