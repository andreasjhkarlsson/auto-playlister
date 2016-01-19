
module Authenticator

open FSpotify
open Settings

type Message =
    | Get of AsyncReplyChannel<Token> 
    | Refresh of AsyncReplyChannel<bool>

let control token =
    let startToken = token
    MailboxProcessor.Start (fun mailbox ->
        let rec handle token = async {
            let! msg = mailbox.Receive ()
            match msg with
            | Get reply ->
                reply.Reply token
                do! handle token
            | Refresh reply ->
                match Authorization.refresh clientId clientSecret startToken with
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
        handle token
    )


let get (controller: MailboxProcessor<Message>) = controller.PostAndAsyncReply Get

let refresh (controller: MailboxProcessor<Message>) = controller.PostAndAsyncReply Refresh