open System
open System.IO
open System.Text
open SmartScreener.Core
open SmartScreener.Core.Extract
open SmartScreener.Core.MatchEngine

let readNonEmpty (prompt:string) =
    let rec loop () =
        printf "%s" prompt   // avoids Console.Write overload ambiguity
        let s = Console.ReadLine()
        if String.IsNullOrWhiteSpace s then loop() else s
    loop()


let rec readExistingFile prompt =
    let path = readNonEmpty prompt
    if File.Exists path then path
    else
        printfn "File not found: %s\nPlease try again." path
        readExistingFile prompt


[<EntryPoint>]
let main _ =
    Console.OutputEncoding <- Encoding.UTF8
    printfn "\n=== Smart Resume Screener (CLI) ===\n"
    let resumePath = readExistingFile "Enter path to resume (.pdf or .txt): "
    let jdTitle    = readNonEmpty "Job Title: "

    printfn "Paste Job Description below (finish with a single line containing only 'END'):\n"
    let mutable lines = []
    let mutable doneRead = false
    while not doneRead do
        let line = Console.ReadLine()
        if line = "END" then doneRead <- true else lines <- line :: lines
    let jdText = lines |> List.rev |> String.concat "\n"

    try
        let resume = Extract.loadResume resumePath
        let jd = { JobDescription.Title = jdTitle; Text = jdText }
        let scorer : IScorer = TfidfScorer() :> IScorer
        let result = scorer.Score(resume, jd)

        printfn "\nMatch Score (0–1): %.4f" result.Score
        printfn "Top Overlapping Terms:"
        result.TopOverlap
        |> List.iter (fun t -> printfn "  %-20s r=%.3f  jd=%.3f" t.Term t.ResumeTfIdf t.JdTfIdf)
        printfn "\nResume Preview:\n%s" result.ResumePreview
        0
    with ex ->
        eprintfn "Error: %s" ex.Message
        1
