open Expecto
open SmartScreener.Core
open SmartScreener.Core.MatchEngine

[<Tests>]
let tests =
  testList "matching" [
    test "tfidf scorer yields higher score for similar text" {
      let resume = { Resume.Path = "mem"; Text = "java spring boot microservices aws docker" }
      let jd1 = { JobDescription.Title = "SWE"; Text = "java spring boot microservices" }
      let jd2 = { JobDescription.Title = "DS"; Text = "statistics python pandas" }
      let sc : IScorer = TfidfScorer() :> IScorer
      let s1 = sc.Score(resume, jd1).Score
      let s2 = sc.Score(resume, jd2).Score
      Expect.isGreaterThan s1 s2 "similar JD should score higher"
    }
  ]

[<EntryPoint>]
let main argv = runTestsWithCLIArgs [] argv tests
