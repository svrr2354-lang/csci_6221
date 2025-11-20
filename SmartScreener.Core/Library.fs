namespace SmartScreener.Core

open System
open System.IO
open System.Text
open System.Text.RegularExpressions


[<CLIMutable>]
type Resume = { Path: string; Text: string }

[<CLIMutable>]
type JobDescription = { Title: string; Text: string }

[<CLIMutable>]
type MatchTerm = { Term: string; ResumeTfIdf: float; JdTfIdf: float }

[<CLIMutable>]
type MatchResult = {
    Score: float
    TopOverlap: MatchTerm list
    ResumePreview: string
}


module Normalize =
    open System

    let private rxSpace = Regex(@"\s+", RegexOptions.Compiled)
    let private rxNonAlnum = Regex(@"[^a-z0-9]+", RegexOptions.Compiled)

    let private stopwords =
        set [
            "a"; "an"; "the"; "and"; "or"; "of"; "to"; "in"; "on"; "for"; "by"; "with"
            "at"; "from"; "as"; "is"; "are"; "was"; "were"; "be"; "this"; "that"
            "we"; "you"; "our"; "their"; "it"; "they"; "i"
        ]

    let clean (s:string) =
        s.ToLowerInvariant()
        |> fun t -> rxNonAlnum.Replace(t, " ")
        |> fun t -> rxSpace.Replace(t, " ")
        |> fun t -> t.Trim()

    let tokenize (s:string) =
        let t = clean s
        if String.IsNullOrWhiteSpace t then
            [||]
        else
            t.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun w -> w.Trim())
            |> Array.filter (fun w ->
                w.Length > 2 &&               
                not (stopwords.Contains w))   


// -------- TF-IDF --------

module TfIdf =
    /// term frequencies
    let tf (tokens:string[]) =
        tokens
        |> Array.countBy id
        |> Array.map (fun (t,c) -> t, float c / float tokens.Length)
        |> Map.ofArray

    
    let idf (docs:string[][]) =
        let n = float docs.Length
        docs
        |> Array.collect Array.distinct
        |> Array.distinct
        |> Array.map (fun term ->
            let df =
                docs
                |> Array.sumBy (fun d -> if Array.exists ((=) term) d then 1 else 0)
                |> float
            term, Math.Log((n + 1.0) / (df + 1.0)) + 1.0
        )
        |> Map.ofArray

    
    let vector (tfMap:Map<string,float>) (idfMap:Map<string,float>) =
        tfMap
        |> Map.map (fun term tfv ->
            let idf =
                match Map.tryFind term idfMap with
                | Some v -> v
                | None -> 0.0
            tfv * idf
        )

    
    let dot (a:Map<string,float>) (b:Map<string,float>) =
        let smaller, bigger = if Map.count a < Map.count b then a,b else b,a
        smaller
        |> Seq.sumBy (fun (KeyValue(term, av)) ->
            let bv = defaultArg (Map.tryFind term bigger) 0.0
            av * bv
        )

    
    let norm (v:Map<string,float>) =
        v |> Seq.sumBy (fun (KeyValue(_, x)) -> x * x) |> sqrt

    let cosine (a:Map<string,float>) (b:Map<string,float>) =
        let d  = dot a b
        let na = norm a
        let nb = norm b
        if na = 0.0 || nb = 0.0 then 0.0 else d / (na * nb)

// -------- Extractors --------

module Extract =
    open UglyToad.PdfPig   

    type ITextExtractor =
        abstract member CanHandle : string -> bool
        abstract member Extract : string -> string

    type PdfExtractor() =
        interface ITextExtractor with
            member _.CanHandle path = path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            member _.Extract path =
                use doc = PdfDocument.Open(path)
                doc.GetPages()
                |> Seq.collect (fun p -> p.GetWords() |> Seq.map (fun w -> w.Text))
                |> String.concat " "

    type TxtExtractor() =
        interface ITextExtractor with
            member _.CanHandle path = path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            member _.Extract path = File.ReadAllText(path)

    let defaultExtractors : ITextExtractor list = [ PdfExtractor() :> ITextExtractor; TxtExtractor() :> ITextExtractor ]

    let loadResume (path:string) =
        let extractor =
            defaultExtractors |> List.tryFind (fun e -> e.CanHandle path)
            |> Option.defaultWith (fun _ -> failwith $"No extractor for extension: {Path.GetExtension path}")
        let text = extractor.Extract path
        { Path = path; Text = text }

// -------- Matching Engine --------

module MatchEngine =
    open Normalize

    type IScorer =
        abstract member Score : Resume * JobDescription -> MatchResult

    
    type TfidfScorer() =
        interface IScorer with
            member _.Score (resume, jd) =
                let rTok = tokenize resume.Text
                let jTok = tokenize jd.Text
                let rTf = TfIdf.tf rTok
                let jTf = TfIdf.tf jTok
                let idf = TfIdf.idf [| rTok; jTok |]
                let rVec = TfIdf.vector rTf idf
                let jVec = TfIdf.vector jTf idf
                let score = TfIdf.cosine rVec jVec
                
                let overlap =
                    rVec
                    |> Seq.choose (fun (KeyValue(term, rv)) ->
                        match Map.tryFind term jVec with
                        | Some jv -> Some { Term = term; ResumeTfIdf = rv; JdTfIdf = jv }
                        | None -> None)
                    |> Seq.sortByDescending (fun x -> x.ResumeTfIdf + x.JdTfIdf)
                    |> Seq.truncate 50
                    |> Seq.toList
                let preview = resume.Text |> fun t -> if t.Length > 240 then t.Substring(0,240) + "…" else t
                { Score = score; TopOverlap = overlap; ResumePreview = preview }
