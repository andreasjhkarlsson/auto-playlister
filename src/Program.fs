
open System.Text.RegularExpressions
open FSpotify
open RedditSharp
open Settings

type Song = {
    Artist: string
    Title: string
}

let submissionFeed (reddit: Reddit) subreddit regex  =
    let subreddit = reddit.GetSubreddit(subreddit)
    let regex = new Regex(regex)
    let trim (str: string) = str.Trim ()
    fun length ->
        async {
            return
                subreddit.Hot.GetListing(length)
                |> Seq.choose (fun submission ->
                    
                    let ``match`` = regex.Match submission.Title

                    if ``match``.Success then  
                        { Artist = trim ``match``.Groups.["artist"].Value
                          Title = trim ``match``.Groups.["title"].Value } |> Some
                    else
                        printfn "Did not match submission (possible discussion or malformed): %s" submission.Title
                        None
                )
        }

let playlistBuilder userId playlistId maxSongs =

    let findSong (song: Song) = async {
        let! token = Authenticator.get ()
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
        let! token = Authenticator.get ()
        return
            Playlist.tracks userId playlistId
            |> Request.withAuthorization token
            |> Paging.page
            |> Paging.asSeq    
    }

    let removeTrack (track: Track) = async {
        let! token = Authenticator.get ()
        return!
            Playlist.removeTracks userId playlistId [track.id]
            |> Request.withAuthorization token
            |> Request.mapResponse ignore
            |> Request.asyncSend
    }
        

    let addTrack (track: Track) = async {

        let! tracks = currentTracks ()

        let tracks = tracks |> Array.ofSeq



        if tracks |> Array.exists (fun existing -> existing.track.id = track.id) then
            printfn "Track (%s -- %s) is already present in playlist, skipping" track.artists.Head.name track.name
        else
            if tracks.Length >= maxSongs then
                do!
                    tracks
                    |> Array.minBy (fun track -> track.added_at)
                    |> (fun pt ->
                        printfn "Playlist length exceeded, removing oldest track (%s -- %s)" pt.track.artists.Head.name pt.track.name
                        removeTrack pt.track
                    )
            let! token = Authenticator.get ()
            printfn "Adding track %s -- %s to playlist" track.artists.Head.name track.name
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
                        let! result = Authenticator.refresh ()
                        if not result then
                            let msg = "Could not refresh token, agent is quitting."
                            printfn "%s" msg
                            failwith msg
                        else printfn "Token refreshed"
                        mailbox.Post song // Process song again
                        do! processed |> waitForSong
                    else
                        do! processed |> Set.add song |> waitForSong
                        printfn "Spotify error: %s: %s" code msg
                    
                |error ->
                    printfn "Error: %A" error
                    do! processed |> Set.add song |> waitForSong
            else
                printfn "Song '%s -- %s' in cache, skipping" song.Artist song.Title
                do! processed |> waitForSong
        }

        waitForSong Set.empty
    )

let runJob reddit (job: Settings.Job) =
    let stream = submissionFeed reddit job.Subreddit.Name job.Subreddit.Pattern

    let playlist = playlistBuilder (SpotifyId job.Playlist.User) (SpotifyId job.Playlist.Id) job.Playlist.Limit 

    let rec loop () = async {
        try
            printfn "Fetching frontpage for %s" job.Subreddit.Name
            let! songs = stream job.Subreddit.Limit

            printfn "Updating playlist"
            songs |> Seq.iter playlist.Post
            printfn "Going to sleep (%A)" System.DateTime.Now
        with
        | error ->
            printfn "An error occured. Will try again later. %A" error

        do! Async.Sleep (job.Refresh * 1000)
        do! loop ()
    }

    loop ()

[<EntryPoint>]
let main argv = 
    printfn "Bot started"

    let reddit = new Reddit()

    runJob reddit settings.Job |> Async.RunSynchronously

    0
