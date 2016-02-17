module Misc

let rec supervise workflow = async {
    try
        return! workflow
    with
    | error ->
        printfn "Trapped uncaught exception in workflow: %s" error.Message
        printfn "Stacktrace: %s" error.StackTrace
        do System.Environment.Exit(1)
}