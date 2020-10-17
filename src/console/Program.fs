

open RedditSharp
open Settings

[<EntryPoint>]
let main argv = 
    printfn "Bot started"

    let reddit = new Reddit()

    while true do
        try Job.run reddit settings.Job |> Async.RunSynchronously
        with e ->
            printfn "There was an error in the bot, restarting..."
    0
