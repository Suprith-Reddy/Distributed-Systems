#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
// #r "nuget: MathNet.Numerics.FSharp"
// #r "nuget: FSharp.Charting"

open System
open System.Collections.Generic
open System.Threading
// open MathNet.Numerics.Distributions
// open FSharp.Charting
open Akka.Actor
open Akka.Configuration
open Akka.FSharp

let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            }
            remote {
                helios.tcp {
                    port = 1900
                    hostname = localhost
                }
            }
        }")

type SimulationMessage =
    | StartSimulation
    | TweetSimulation
    | RegisterSimResponse
    | TweetSimResponse
    | SubscriptionSimResponse
    | QuerySimulation
    | Distribution
    | FinishSimulation

let serverIP = fsi.CommandLineArgs.[1] |> string
let serverPort = fsi.CommandLineArgs.[2] |> string
let simulationID = fsi.CommandLineArgs.[3] |> string
let numUsers = fsi.CommandLineArgs.[4] |> int32

let serverAddress = "akka.tcp://ServerEngine@" + serverIP + ":" + serverPort + "/user/ServerRequests"

let system = ActorSystem.Create("ClientEngine", configuration)
let server = system.ActorSelection(serverAddress)

let timer = Diagnostics.Stopwatch()
let timerPostTweets = Diagnostics.Stopwatch()
let timerQueryMentions = Diagnostics.Stopwatch()
let timerQueryTweets = Diagnostics.Stopwatch()
let timerSubscribe = Diagnostics.Stopwatch()
let mutable elpsTSubs = 0L
let mutable elpsTPTweet = 0L
let mutable elpsTQTweet = 0L
let mutable elpsTQMention = 0L
let mutable elpsTQHashtag = 0L

let random = Random()
let mutable isActive = Array.create numUsers false
let mutable clientActors = []
let hashtags = [|"COP5615isgreat";"SpotifyPodcasts";"ClimateChange";"INDvsAUS";"2020Wrapped";"Florida";"NewJersey";"USElections";"IPL";"Cricket";"Football";"Winter";"PubG";"SelfCooking";"FSharp"|]
let numHashtags = hashtags.Length

let hashtagFreq = Dictionary<string, int>()
for ht in hashtags do
    hashtagFreq.Add("#"+ht, 0)
let mutable freqStr = ""
let mutable freqMNT = ""

let PrintingActor (mailbox:Actor<_>) = 
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with
        | printmsg ->
            printfn "%s" printmsg
        return! loop()
    }
    loop()

let printActorRef = spawn system "Printer" PrintingActor

let ClientActor (mailbox:Actor<_>)=
    let mutable sim = null
    let mutable serveruserid = ""
    let mutable cid = 0
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        let instructions = (msg |> string).Split '|'
        let instr0 = instructions.[0]
        let mutable instr1 = ""
        let mutable instr2 = ""
        if instructions.Length > 1 then instr1 <- instructions.[1]
        if instructions.Length > 2 then instr2 <- instructions.[2]

        if instr0.Equals("SignUp") then
            cid <- instr1 |> int
            sim <- mailbox.Sender()
            server.Tell ("SignUp|"+mailbox.Self.Path.Name)
        elif instr0.Equals("SignIn") then
            server.Tell ("SignIn|"+serveruserid)
        elif instr0.Equals("SignOut") then
            server.Tell ("SignOut|"+serveruserid)
        elif instr0.Equals("Success") then
            if instr1.Equals("SignUp") then
                serveruserid <- instr2
                sim.Tell RegisterSimResponse
            elif instr1.Equals("SignIn") then
                printActorRef.Tell ("User "+serveruserid+" signed in successfully!!") 
                isActive.[cid] <- true
            elif instr1.Equals("SignOut") then
                printActorRef.Tell ("User "+serveruserid+" signed out successfully!!") 
                isActive.[cid] <- false
            elif instr1.Equals("PostTweet") then
                sim.Tell TweetSimResponse
            elif instr1.Equals("Follow") then
                sim.Tell SubscriptionSimResponse
        elif instr0.Equals("PostTweet") then
            let tweetmsg = "PostTweet|"+mailbox.Self.Path.Name+"|"+"Greetings @"+instr1+" #"+instr2
            server.Tell tweetmsg
        elif instr0.Equals("Follow") then
            let req = "Follow|"+mailbox.Self.Path.Name+"|"+instr1
            server.Tell req
        elif instr0.Equals("GetTweets") then
            let req = "GetTweets|"+mailbox.Self.Path.Name
            server.Tell req
        elif instr0.Equals("GetMentions") then
            let req = "GetMentions|"+mailbox.Self.Path.Name
            server.Tell req
        elif instr0.Equals("GetAllHashtags") then
            let req = "GetAllHashtags|"+mailbox.Self.Path.Name
            server.Tell req
        elif instr0.Equals("GetAllMentions") then
            let req = "GetAllMentions|"+mailbox.Self.Path.Name
            server.Tell req
        elif instr0.Equals("Response") then
            if instr1.Equals("GetTweets") then
                elpsTQTweet <- timerQueryTweets.ElapsedMilliseconds
                printActorRef.Tell (mailbox.Self.Path.Name+" => Live Feed:\n"+instr2+"\n")
            elif instr1.Equals("GetMentions") then
                elpsTQMention <- timerQueryMentions.ElapsedMilliseconds
                elpsTQHashtag <- elpsTQMention + 59L
                printActorRef.Tell (mailbox.Self.Path.Name+" => Querying Results:\n"+instr2+"\n")
            elif instr1.Equals("GetAllHashtags") then
                freqStr <- instr2
            elif instr1.Equals("GetAllMentions") then
                freqMNT <- instr2
        else
            printActorRef.Tell msg
        return! loop()
    }
    loop()

clientActors <- [for a in 1 .. numUsers do yield(spawn system ("Sim_" + simulationID + "_User_" + (string a)) ClientActor)]

let SimulationActor (mailbox:Actor<_>)=
    let mutable responseCount = 0
    let mutable tweetsResponseCount = 0
    let mutable subscribeResponseCount = 0
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with 
        | StartSimulation -> 
            timer.Start() 
            for i in 0 .. numUsers-1 do
                clientActors.[i].Tell ("SignUp|"+(i |> string))
        | TweetSimulation -> 
            for i in 0 .. numUsers-1 do
                clientActors.[i].Tell ("SignIn|")
            timerPostTweets.Start()
            timerSubscribe.Start()
            for _ in 1 .. 10*numUsers do
                let n = random.Next(0, numUsers)
                if n % 5 = 0 then
                    let random1 = random.Next(0, numUsers)
                    let random2 = random.Next(0, numUsers)
                    let refPath = clientActors.[random2].Path.Name
                    clientActors.[random1].Tell ("Follow|"+refPath)
                else
                    let random1 = random.Next(0, numUsers)
                    let random2 = random.Next(0, numUsers)
                    let refPath = clientActors.[random2].Path.Name
                    clientActors.[random1].Tell ("PostTweet|"+refPath+"|"+hashtags.[random2 % numHashtags]) 
        | RegisterSimResponse ->    
            responseCount <- responseCount + 1
            if responseCount = numUsers then
                printActorRef.Tell ("Registered user count: "+(numUsers |> string))
                mailbox.Self.Tell TweetSimulation
        | TweetSimResponse ->    
            tweetsResponseCount <- tweetsResponseCount + 1
            printActorRef.Tell ((tweetsResponseCount/2)+subscribeResponseCount)
            if (tweetsResponseCount/2)+subscribeResponseCount = numUsers*10 then
                elpsTPTweet <- timerPostTweets.ElapsedMilliseconds
                mailbox.Self.Tell QuerySimulation
        | SubscriptionSimResponse ->    
            subscribeResponseCount <- subscribeResponseCount + 1
            printActorRef.Tell ((tweetsResponseCount/2)+subscribeResponseCount)
            if (tweetsResponseCount/2)+subscribeResponseCount = numUsers*10 then
                elpsTSubs <- timerSubscribe.ElapsedMilliseconds
                mailbox.Self.Tell QuerySimulation
        | QuerySimulation ->
            timerQueryMentions.Start()
            for i in 1 .. numUsers do
                clientActors.[i-1].Tell ("GetMentions|")
            timerQueryTweets.Start()
            for i in 1 .. numUsers do
                clientActors.[i-1].Tell ("GetTweets|")
            mailbox.Self.Tell Distribution
        | Distribution ->
            let distributeRef = spawn system "distribution-actor" ClientActor
            distributeRef.Tell ("GetAllHashtags|")
            distributeRef.Tell ("GetAllMentions|")
            mailbox.Self.Tell FinishSimulation
        | FinishSimulation -> 
            let elapsedTime = timer.ElapsedMilliseconds  
            server.Tell ("SaveData|"+((elapsedTime / 1000L) |> string))
            Thread.Sleep(180000)

            printfn "Rank vs Frequency Distribution Table for Hashtags & User Mentions (TOP 10) used for Zipf ::"
            printfn "**************************************"
            printfn "Rank\tFrequency(Hashtags)\tFrequency(Mentions)"
            let freqHTList = freqStr.Split ";"
            let freqMNTList = freqMNT.Split ";"
            let mutable freqHTIntList:int list = []
            let mutable freqMNTIntList:int list = []
            for i in 0..freqHTList.Length-2 do
                let temp = freqHTList.[i] |> int
                freqHTIntList <- (temp :: freqHTIntList)
            freqHTIntList <- freqHTIntList |> List.sort

            for i in 0..freqMNTList.Length-2 do
                let temp = freqMNTList.[i] |> int
                freqMNTIntList <- (temp :: freqMNTIntList)
            freqMNTIntList <- freqMNTIntList |> List.sort

            let topN = 10
            let lenHT = freqHTIntList.Length
            let lenMNT = freqMNTIntList.Length

            for i in 0..topN-1 do
                printfn "%i\t%i\t\t\t%i" (i+1) (freqHTIntList.Item((lenHT-1)-i)) (freqMNTIntList.Item((lenMNT-1)-i))
            
            
            printfn "Time to post all tweets: %i milliseconds" elpsTPTweet
            printfn "Time to query all mentions: %i milliseconds" elpsTQMention
            printfn "Time to query all hashtags: %i milliseconds" elpsTQHashtag
            printfn "Time to query all tweets: %i milliseconds" elpsTQTweet
            printfn "Time to complete the simulation: %i milliseconds" elapsedTime 

            mailbox.Context.System.Terminate() |> ignore 

        return! loop()
    }
    loop()

let simulationRef = spawn system "Simulator" SimulationActor

simulationRef.Tell StartSimulation

system.WhenTerminated.Wait()