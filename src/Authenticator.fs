
module Authenticator

open FSpotify
open Settings

type Message =
    | Get of AsyncReplyChannel<Token> 
    | Refresh of AsyncReplyChannel<bool>

let agent =
    let auth = settings.Authorization

    let appToken = {
            access_token = auth.Token.Value
            token_type = auth.Token.Type
            expires_in = auth.Token.Expires
            refresh_token = Some auth.Token.Refresh
        }

    MailboxProcessor.Start (fun mailbox ->
        let rec handle token = async {
            let! msg = mailbox.Receive ()
            match msg with
            | Get reply ->
                reply.Reply token
                do! handle token
            | Refresh reply ->
                match Authorization.refresh auth.Client.Id auth.Client.Secret appToken with
                | Some refresh ->
                    try
                        let! newToken = Request.asyncTrySend refresh
                        match newToken with
                        | Request.Success newToken ->
                            reply.Reply true
                            do! handle newToken
                        | Request.Error _ -> reply.Reply false
                    with error -> reply.Reply false
                | None -> reply.Reply false
        }
        Misc.supervise <| handle appToken
    )


let withAuthentication operation = async {
    let! token = agent.PostAndAsyncReply Get
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
                return! operation newToken
            else
                printfn "Could not refresh token"
                return failwith "Could not refresh token"
        else return raise error
}