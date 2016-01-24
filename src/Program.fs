
open System.Text.RegularExpressions
open FSpotify
open RedditSharp
open Settings

type Submission = {
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
                        printfn "Did not match submission pattern: %s" submission.Title
                        None
                )
        }

let search =
    let agent = MailboxProcessor.Start (fun mailbox ->
        let rec search (cache: Map<Submission,Track option>) = async {
            let! ((song,reply): Submission*AsyncReplyChannel<Track option>) = mailbox.Receive ()
            match cache |> Map.tryFind song with
            | Some trackIdResult ->
                trackIdResult |> reply.Reply
                return! search cache
            | None ->
                let! trackResult =
                    Authenticator.withAuthentication (fun token -> async {
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
                    )

                trackResult |> reply.Reply
                return! cache |> Map.add song trackResult |> search    
        }

        Misc.supervise <| search Map.empty
    )

    fun song -> agent.PostAndAsyncReply (fun reply -> song,reply)


module Playlist =
    
    type Action =
        | Exists of Track*AsyncReplyChannel<bool>
        | Add of Track
        | Remove of Track
        | Count of AsyncReplyChannel<int>
        | Tracks of AsyncReplyChannel<PlaylistTrack seq>

    type Agent = MailboxProcessor<Action>

    let exists track (agent: Agent) = agent.PostAndAsyncReply (fun reply -> Exists(track, reply))

    let add track (agent: Agent) = agent.Post (Add track)

    let count (agent: Agent) = agent.PostAndAsyncReply Count

    let remove track (agent: Agent) = agent.Post (Remove track)

    let tracks (agent: Agent) = agent.PostAndAsyncReply Tracks

    let load userId playlistId: Agent =

        let loadTracks = 
            Authenticator.withAuthentication (fun token -> async {
                return
                    Playlist.tracks userId playlistId
                    |> Request.withAuthorization token
                    |> Paging.page
                    |> Paging.asSeq                
            })

        let removeTrack (track: Track) =
            Authenticator.withAuthentication (fun token ->
                Playlist.removeTracks userId playlistId [track.id]
                |> Request.withAuthorization token
                |> Request.mapResponse ignore
                |> Request.asyncSend
            )

        let addTrack (track: Track) =
            Authenticator.withAuthentication (fun token ->
                FSpotify.Playlist.add userId playlistId [track.id]
                |> Request.withAuthorization token
                |> Request.mapResponse ignore
                |> Request.asyncSend                
            )

        let asCache (tracks: PlaylistTrack seq) =
            tracks 
            |> Seq.map (fun track -> track.track.id)
            |> Set.ofSeq

        MailboxProcessor.Start(fun mailbox ->
            let rec listen (cache: Set<SpotifyId>) = async {
                let! message = mailbox.Receive ()
                match message with
                | Exists (track, reply) ->
                    cache |> Set.contains track.id |> reply.Reply
                    return! listen cache
                | Add track ->
                    do! addTrack track
                    return! cache |> Set.add track.id |> listen
                | Remove track ->
                    do! removeTrack track
                    return! cache |> Set.remove track.id |> listen
                | Count reply ->
                    reply.Reply cache.Count
                    return! listen cache
                | Tracks reply ->
                    let! tracks = loadTracks
                    reply.Reply tracks
                    // We may as well refresh the cache while we have a fresh list
                    return! listen (asCache tracks) 

            }
            async {
                let! existing = loadTracks
                return! existing |> asCache |> listen
            } |> Misc.supervise
        )

let runJob reddit (job: Settings.Job) =
    let stream = submissionFeed reddit job.Subreddit.Name job.Subreddit.Pattern

    let playlist = Playlist.load (SpotifyId job.Playlist.User) (SpotifyId job.Playlist.Id)

    let trimPlaylist () = async {
        let! count = playlist |> Playlist.count
        if count > job.Playlist.Limit then
            let! tracks = playlist |> Playlist.tracks
            let oldest = tracks |> Seq.minBy (fun track -> track.added_at)
            printfn "Playlist limit reached. Removing track '%s'" oldest.track.name
            do playlist |> Playlist.remove oldest.track
    }

    let rec run () = async {
        try
            let! songs = stream job.Subreddit.Limit

            for song in songs do

                let! track = search song

                match track with
                | Some track ->
                    let! exists = playlist |> Playlist.exists track
                    match exists with
                    | true ->
                        printfn "Skipping: '%s' -- '%s' as it's already added to playlist" song.Artist song.Title
                    | false ->
                        printfn "Adding: '%s' -- '%s' to playlist" song.Artist song.Title
                        do playlist |> Playlist.add track
                        do! trimPlaylist ()
                | None ->
                    printfn "Skipping: '%s' -- '%s' as it's not available on spotify" song.Artist song.Title

            printfn "Going to sleep (%A)" System.DateTime.Now
        with
        | error ->
            printfn "An error occured. Will try again later. %A" error

        do! Async.Sleep (job.Refresh * 1000)
        do! run ()
    }

    run ()

[<EntryPoint>]
let main argv = 
    printfn "Bot started"

    let reddit = new Reddit()

    runJob reddit settings.Job |> Async.RunSynchronously

    0
