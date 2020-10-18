

open RedditSharp
open Job
open FSharp.Data
open FSpotify

type Settings = XmlProvider<"settings.xml">

[<EntryPoint>]
let main argv = 
    printfn "Bot started"

    let reddit = new Reddit()

    let settings = Settings.Load "settings.xml"

    Authenticator.configure { 
        ClientId = settings.Authorization.Client.Id
        ClientSecret = settings.Authorization.Client.Secret
        RefreshToken = {
            access_token = settings.Authorization.Token.Value
            token_type = settings.Authorization.Token.Type
            expires_in = settings.Authorization.Token.Expires
            refresh_token = Some settings.Authorization.Token.Refresh
        }
    }

    let job = async {

        do! Job.run reddit {
            Playlist = {|User = (SpotifyId settings.Job.Playlist.User); Id = (SpotifyId settings.Job.Playlist.Id); Limit = settings.Job.Playlist.Limit|}
            Subreddit = {| Name = settings.Job.Subreddit.Name; Limit = settings.Job.Subreddit.Limit; Pattern = settings.Job.Subreddit.Pattern|}
        }
        
        printfn "Going to sleep (%A)" System.DateTime.Now
        do! Async.Sleep (settings.Job.Refresh * 1000)
    }

    while true do
        try
            job |> Async.RunSynchronously
        with e ->
            printfn "There was an error in the bot, restarting..."
    0
