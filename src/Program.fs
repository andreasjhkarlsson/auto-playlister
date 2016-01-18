
open System.Text.RegularExpressions
open FSpotify
open RedditSharp

type Song = {
    Artist: string
    Title: string
}

let submissionFeed (reddit: Reddit) subreddit regex  =
    let subreddit = reddit.GetSubreddit(subreddit)

    let trim (str: string) = str.Trim ()
    fun () ->
        async {
            return
                subreddit.Hot.GetListing(25)
                |> Seq.choose (fun submission ->

                    let regex = new Regex(regex)
                    let ``match`` = regex.Match submission.Title

                    if ``match``.Success then  
                        { Artist = trim ``match``.Groups.["artist"].Value
                          Title = trim ``match``.Groups.["title"].Value } |> Some
                    else None
                )
        }

let playlistBuilder auth userId playlistId maxSongs =

    let findSong (song: Song) = async {
        let! token = Authenticator.get auth
        return
            sprintf "track:%s artist:%s" song.Title song.Artist
            |> Search.Query
            |> FSpotify.Search.track
            |> Request.withOptionals (fun o -> {o with limit = Some 1})
            |> Request.withAuthorization token
            |> Paging.page
            |> Paging.asSeq
            |> Seq.tryPick Some    
    }

    let currentTracks () = async {
        let! token = Authenticator.get auth
        return
            Playlist.tracks userId playlistId
            |> Request.withAuthorization token
            |> Paging.page
            |> Paging.asSeq    
    }

    let addTrack (track: Track) = async {

        let! tracks = currentTracks ()

        if tracks |> Seq.exists (fun existing -> existing.track.id = track.id) then
            printfn "Track is already present in playlist, skipping"
        else
            let! token = Authenticator.get auth
            printfn "Adding track %s -- %s (%A) to playlist" (List.head track.artists).name track.name track.id
            return!
                FSpotify.Playlist.add userId playlistId [track.id]
                |> Request.withAuthorization token
                |> Request.mapResponse ignore
                |> Request.asyncSend
    }

    let tryAddSong (song: Song) = async {
        let! track = findSong song
        match track with
        | Some track ->
            do! addTrack track
        | None ->
            printfn "Track: '%s -- %s' was not found" song.Artist song.Title    
    }    

    MailboxProcessor.Start (fun mailbox ->

        let rec waitForSong (processed: Set<Song>) = async {
            let! (song: Song) = mailbox.Receive ()
            
            if not (processed |> Set.contains song) then
                try
                    do! tryAddSong song
                    do! processed |> Set.add song |> waitForSong
                with
                | SpotifyError (code, msg) ->
                    if code = "401" then
                        printfn "Token expired, refreshing"
                        let! result = Authenticator.refresh auth
                        if not result then
                            failwith "Could not refresh token, agent is quitting."
                        else printfn "Token refreshed"
                        mailbox.Post song // Process song again
                        do! processed |> waitForSong
                    else
                        do! processed |> Set.add song |> waitForSong
                        printfn "Spotify error: %s: %s" code msg
                    
                |error ->
                    printfn "Error: %A" error
                    do! processed |> Set.add song |> waitForSong
            else do! processed |> waitForSong
        }

        waitForSong Set.empty
    )

[<EntryPoint>]
let main argv = 
    printfn "%A" argv

    let reddit = new Reddit()

    let listentothis = submissionFeed reddit Settings.subreddit Settings.regex

    let auth = Authenticator.control Settings.token

    let updatePlaylist = playlistBuilder auth Settings.userId Settings.playlistId Settings.playlistLimit

    let rec loop () = async {
        
        printfn "Fetching frontpage"
        let! songs = listentothis ()

        printfn "Updating playlist"
        songs |> Seq.iter updatePlaylist.Post

        do! Async.Sleep Settings.refreshRate
        do! loop ()
    }

    loop () |> Async.RunSynchronously

    0
