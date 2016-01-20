module Settings

open FSpotify
open FSharp.Data


type Settings = XmlProvider<"settings.xml">

let settings = Settings.Load "settings.xml"
