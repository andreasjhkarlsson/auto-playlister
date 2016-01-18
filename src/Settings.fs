module Settings

open FSpotify

let clientId = "<client-id>"
let clientSecret = "<client-secret>"
let token = {access_token = "<access-token>"
             token_type = "Bearer"
             expires_in = -1
             refresh_token = Some "<refresh-token>"}
let userId = SpotifyId "<username>"
let playlistId = SpotifyId "<playlist-id>"
let playlistLimit = 100
let refreshRate = 10*60*1000
let subreddit = "listentothis"
let regex = @"(?<artist>.*)--(?<title>.*)\[" 
let frontpageSize = 25