namespace Lambda

open Amazon.Lambda.Core

open RedditSharp
open System.IO
open Job
open FSpotify
open System

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>)>]
()

type Function() =

    member __.FunctionHandler (input: Stream) (_: ILambdaContext) =
        let reddit = new Reddit()

        let job = {
            Subreddit = {|
                        Name = Environment.GetEnvironmentVariable("subreddit_name")
                        Pattern = Environment.GetEnvironmentVariable("subreddit_pattern")
                        Limit = int <| Environment.GetEnvironmentVariable("subreddit_limit")
            |};
            Playlist = {|
                        User = SpotifyId (Environment.GetEnvironmentVariable("spotify_user"))
                        Id = SpotifyId (Environment.GetEnvironmentVariable("spotify_playlist_id"))
                        Limit = int <| Environment.GetEnvironmentVariable("spotify_playlist_limit")
            |}
        }

        Authenticator.configure { 
            ClientId = Environment.GetEnvironmentVariable("spotify_client_id")
            ClientSecret = Environment.GetEnvironmentVariable("spotify_client_secret")
            RefreshToken = {
                access_token = Environment.GetEnvironmentVariable("spotify_token_value")
                token_type = Environment.GetEnvironmentVariable("spotify_token_type")
                expires_in = int <| Environment.GetEnvironmentVariable("spotify_token_expires_in")
                refresh_token = Some <| Environment.GetEnvironmentVariable("spotify_token_refresh")
            }
        }

        try
            Job.run reddit job |> Async.RunSynchronously
            "Bot did its job"
        with e ->
            sprintf "There was an error in the bot: %s" e.Message


        
