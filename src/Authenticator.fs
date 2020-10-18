
module Authenticator

open FSpotify

type Config = {
    ClientId: string
    ClientSecret: string
    RefreshToken: Token
}

type Message =
    | Configure of Config
    | Get of AsyncReplyChannel<Token option> 
    | Refresh of AsyncReplyChannel<bool>

type State = {
    Config: Config option
    Token: Token option
}

let agent =

    MailboxProcessor.Start (fun mailbox ->
        let rec handle state = async {
            let! msg = mailbox.Receive ()
            match msg with
            | Configure config ->
                do! handle { state with Config = Some config; Token = Some config.RefreshToken }
            | Get reply ->
                reply.Reply state.Token
                do! handle state
            | Refresh reply ->

                match state.Config with
                | Some config -> 
                    match Authorization.refresh config.ClientId config.ClientSecret config.RefreshToken with
                    | Some refresh ->
                        try
                            let! newToken = Request.asyncTrySend refresh
                            match newToken with
                            | Request.Success newToken ->
                                reply.Reply true
                                do! handle { state with Token = Some newToken }
                            | Request.Error _ ->
                                reply.Reply false
                        with error -> reply.Reply false
                    | None -> reply.Reply false
                | None ->
                    reply.Reply false
                

                
        }
        Misc.supervise <| handle { Config = None; Token = None}
    )

let configure config = agent.Post (Configure config)

let withAuthentication operation = async {
    let! token = agent.PostAndAsyncReply Get
    match token with
    | Some token ->
        try
            return! operation token
        with
        | SpotifyError (code, msg) as error ->
            if code = "401" then
                printfn "Token expired. Refreshing..."
                let! refreshSucceeded = agent.PostAndAsyncReply Refresh
                if refreshSucceeded then
                    printfn "Token refreshed!"
                    let! newToken = agent.PostAndAsyncReply Get
                    match newToken with
                    | Some token ->
                        return! operation token
                    | None ->
                        return failwith "Something went wrong when fetching the new token"
                else
                    printfn "Could not refresh token"
                    return failwith "Could not refresh token"
            else return raise error
    | None ->
        return failwith "No authentication has been set!"
}