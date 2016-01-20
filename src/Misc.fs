module Misc

let rec supervise workflow = async {
    try
        return! workflow
    with
    | error ->
        printfn "Trapped uncaught exception in workflow: %s" error.Message
        printfn "Restarting workflow (note: agent state is reset)..."
        return! supervise workflow
}