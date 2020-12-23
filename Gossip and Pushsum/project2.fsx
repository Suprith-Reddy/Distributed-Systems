// Definition of required directives
#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"

// Importing required packages or libraries like Akka
open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.TestKit

// Timer to calculate convergence time
let timer = System.Diagnostics.Stopwatch()

type GossipMessage = 
    | GossipFull of int * string
    | GossipTwoDim of int * int * string
    | GossipTwoDimImp of int * int * string
    | GossipLine of int * string

type PushSumMessage = 
    | PushSumFull of int * float * float
    | PushSumTwoDim of int * int * float * float
    | PushSumTwoDimImp of int * int * float * float
    | PushSumLine of int * float * float
    | ExhaustedNode

type BossMessage = 
    | StartPoint of IActorRef * string * int * int
    | Termination of string


// Getting the command arguments as Integers
let args = Environment.GetCommandLineArgs()
let numNodes = args.[3] |> int
let topology = args.[4]
let algorithm = args.[5]

// Creating system for the actors
let system = System.create "gossip-pushsum-system" <| Configuration.load()

// Workers Array and master actor reference declaration
let mutable workerslist: List<IActorRef> = []
let mutable bossActorRef: IActorRef = null

// Generates random numbers
let genRandomNumber numofNodes =
    let random = System.Random()
    let randomNode = random.Next(0, numofNodes-1)
    randomNode

// Generates two dimensional matrix
let genMatrix x y = 
    let matrix = Array2D.create x y 0
    let mutable cnt = 1
    for i in 0..x-1 do
        for j in 0..y-1 do
            matrix.[i, j] <- cnt
            cnt <- cnt + 1
    matrix

// Builds two dimensional topology
let twoDTopology numofNodes = 
    let numofRows:int = numofNodes |> float |> sqrt |> int
    let adjmatrix = genMatrix numofRows numofRows
    adjmatrix

let adjacentNeighbors2D = twoDTopology numNodes

// 2D topology for gossip
let twoDimNeighborsGSP i j message condition =
    let matrixlen = (Array2D.length1 adjacentNeighbors2D)
    let mutable neighbor: int = 0
    let mutable topType = GossipTwoDim
    if condition = "imp" then
        topType <- GossipTwoDimImp
    if j-1 >= 0 then
        neighbor <- adjacentNeighbors2D.[i, j-1]
        workerslist.[neighbor] <! topType (i, j-1, message)
    if j+1 < matrixlen-1 then
        neighbor <- adjacentNeighbors2D.[i, j+1]
        workerslist.[neighbor] <! topType (i, j+1, message)
    if i-1 >= 0 then
        neighbor <- adjacentNeighbors2D.[i-1, j]
        workerslist.[neighbor] <! topType (i-1, j, message)
    if i+1 < matrixlen-1 then
        neighbor <- adjacentNeighbors2D.[i+1, j]
        workerslist.[neighbor] <! topType (i+1, j, message)

// 2D topology for pushsum
let twoDimNeighborsPS i j s w condition =
    let matrixlen = (Array2D.length1 adjacentNeighbors2D)
    let mutable neighbor: int = 0
    let mutable topType = PushSumTwoDim
    if condition = "imp" then
        topType <- PushSumTwoDimImp
    if j-1 >= 0 then
        neighbor <- adjacentNeighbors2D.[i, j-1]
        workerslist.[neighbor] <! topType (i, j-1, s, w)
    if j+1 < matrixlen-1 then
        neighbor <- adjacentNeighbors2D.[i, j+1]
        workerslist.[neighbor] <! topType (i, j+1, s, w)
    if i-1 >= 0 then
        neighbor <- adjacentNeighbors2D.[i-1, j]
        workerslist.[neighbor] <! topType (i-1, j, s, w)
    if i+1 < matrixlen-1 then
        neighbor <- adjacentNeighbors2D.[i+1, j]
        workerslist.[neighbor] <! topType (i+1, j, s, w)

// Line topology for pushsum
let lineNeighborsPS i s w =
    if i - 1 >= 0 then
        workerslist.[i-1] <! PushSumLine (i-1, s, w)
    if i + 1 < numNodes-1 then
        workerslist.[i+1] <! PushSumLine (i+1, s, w)

// Worker Actor reference, it's mailbox, and computation inside it for gossip protocol
let workerActorGossip (mailbox:Actor<_>) = 
    let mutable count: int = 0
    let heardCount: int = 10
    let rec loop() =
        actor{
            let! msg = mailbox.Receive()
            match msg with
            | GossipFull (idx, message) ->
                                    if count < heardCount && mailbox.Self <> mailbox.Sender() then
                                        count <- count + 1
                                    if count < heardCount then
                                        let rNum = genRandomNumber numNodes
                                        workerslist.[rNum] <! GossipFull (rNum, message) 
                                        mailbox.Self <! GossipFull(idx, message)
                                    elif count = heardCount then
                                        bossActorRef <! Termination "terminate"
            | GossipTwoDim (i, j, message) ->
                                if count < heardCount && mailbox.Self <> mailbox.Sender() then
                                        count <- count + 1
                                if count < heardCount then
                                    // let adjmatrix = twoDTopology numNodes
                                    let condition = "perfect"
                                    twoDimNeighborsGSP i j message condition
                                    mailbox.Self <! GossipTwoDim(i, j, message)
                                elif count = heardCount then
                                    bossActorRef <! Termination "terminate"
            | GossipTwoDimImp (i, j, message) -> 
                                if count < heardCount && mailbox.Self <> mailbox.Sender() then
                                        count <- count + 1
                                if count < heardCount then
                                    // let adjmatrix = twoDTopology numNodes
                                    let condition = "imp"
                                    twoDimNeighborsGSP i j message condition
                                    let x = genRandomNumber (Array2D.length1 adjacentNeighbors2D)
                                    let y = genRandomNumber (Array2D.length1 adjacentNeighbors2D)
                                    let rNum = adjacentNeighbors2D.[x, y]
                                    workerslist.[rNum] <! GossipTwoDimImp (x, y, message)
                                    mailbox.Self <! GossipTwoDimImp(i, j, message)
                                elif count = heardCount then
                                    bossActorRef <! Termination "terminate"
            | GossipLine (idx, message) ->
                                if count < heardCount && mailbox.Self <> mailbox.Sender() then
                                        count <- count + 1
                                if count < heardCount then
                                    if idx - 1 >= 0 then
                                        workerslist.[idx-1] <! GossipLine (idx-1, message)
                                    if idx + 1 < numNodes-1 then
                                        workerslist.[idx+1] <! GossipLine (idx+1, message)
                                    mailbox.Self <! GossipLine(idx, message)
                                elif count = heardCount then   
                                    bossActorRef <! Termination "terminate"
            return! loop()
        }
    loop()


// Worker Actor reference, it's mailbox, and computation inside it for pushsum protocol
let workerActorPushSum (mailbox:Actor<_>) = 
    let mutable count: int = 0
    let mutable pushsumcount: int = 0
    let divergence: float = 2.0
    let pushsummax = 3
    let pushsumtermination: float = Math.Pow(10.0, -10.0)
    // let pushsumtermination: float = 0.0000000001
    let mutable terminated: Boolean = false
    let mutable nterminated: int = 0
    let mutable s: float = 0.0
    let mutable w: float = 1.0
    let rec loop() =
        actor{
            let! msg = mailbox.Receive()
            match msg with
            | PushSumFull (idx, s_new, w_new) ->
                                if not terminated then 
                                    count <- count + 1
                                    if count = 1 then
                                        s <- (idx |> float)
                                    let oldSbyW: float = s / w
                                    s <- s + s_new
                                    w <- w + w_new
                                    s <- s / divergence
                                    w <- w / divergence
                                    let newSbyW: float = s / w
                                    let rNum = genRandomNumber numNodes
                                    let diff = Math.Abs(oldSbyW - newSbyW)
                                    if count = 1 || diff > pushsumtermination then
                                        pushsumcount <- 0
                                        workerslist.[rNum] <! PushSumFull (rNum, s, w)
                                        mailbox.Self <! PushSumFull(idx, s, w)
                                    elif pushsumcount < pushsummax then
                                        pushsumcount <- pushsumcount + 1
                                        workerslist.[rNum] <! PushSumFull (rNum, s, w)
                                        mailbox.Self <! PushSumFull(idx, s, w)
                                    elif pushsumcount = pushsummax then
                                        terminated <- true
                                        if topology = "full" then
                                            for i in 0 .. numNodes-1 do 
                                                if i <> idx then
                                                    workerslist.Item(i)<! ExhaustedNode
                                        bossActorRef <! Termination "terminate"
                                else 
                                    let rNum = genRandomNumber numNodes
                                    workerslist.[rNum] <! PushSumFull (rNum, s, w)
                                    mailbox.Self <! PushSumFull (idx, s, w)
            | PushSumTwoDim (i, j , s_new, w_new) -> 
                                    if not terminated then 
                                        count <- count + 1
                                        let idx = adjacentNeighbors2D.[i, j]
                                        if count = 1 then
                                            s <- (idx |> float)
                                        let oldSbyW: float = s / w
                                        // printfn "Actor %i -> Old S by W: %A" idx oldSbyW
                                        s <- s + s_new
                                        w <- w + w_new
                                        s <- s / divergence
                                        w <- w / divergence
                                        let newSbyW: float = s / w
                                        // printfn "Actor %i -> New S by W: %A" idx newSbyW
                                        let diff = Math.Abs(oldSbyW - newSbyW)
                                        // printfn "Actor %i -> Difference: %A" idx diff
                                        if count = 1 || diff > pushsumtermination then
                                            pushsumcount <- 0
                                            twoDimNeighborsPS i j s w "perfect"
                                            mailbox.Self <! PushSumTwoDim(i, j, s, w)
                                        elif pushsumcount < pushsummax then
                                            pushsumcount <- pushsumcount + 1
                                            twoDimNeighborsPS i j s w "perfect"
                                            mailbox.Self <! PushSumTwoDim(i, j, s, w)
                                        elif pushsumcount = pushsummax then
                                            terminated <- true
                                            bossActorRef <! Termination "terminate"
            | PushSumTwoDimImp (i, j , s_new, w_new) -> 
                                    if not terminated then 
                                        count <- count + 1
                                        let idx = adjacentNeighbors2D.[i, j]
                                        if count = 1 then
                                            s <- (idx |> float)
                                        let oldSbyW: float = s / w
                                        printfn "Actor %i -> Old S by W: %A" idx oldSbyW
                                        s <- s + s_new
                                        w <- w + w_new
                                        s <- s / divergence
                                        w <- w / divergence
                                        let newSbyW: float = s / w
                                        // printfn "Actor %i -> New S by W: %A" idx newSbyW
                                        let rNum = genRandomNumber numNodes
                                        let diff = Math.Abs(oldSbyW - newSbyW)
                                        // printfn "Actor %i -> Difference: %A" idx diff
                                        let x = genRandomNumber (Array2D.length1 adjacentNeighbors2D)
                                        let y = genRandomNumber (Array2D.length1 adjacentNeighbors2D)
                                        let rNum = adjacentNeighbors2D.[x, y]
                                        if count = 1 || diff > pushsumtermination then
                                            pushsumcount <- 0
                                            twoDimNeighborsPS i j s w "imp"
                                            workerslist.[rNum] <! PushSumTwoDimImp (x, y, s, w)
                                            mailbox.Self <! PushSumTwoDimImp(i, j, s, w)
                                        elif pushsumcount < pushsummax then
                                            pushsumcount <- pushsumcount + 1
                                            twoDimNeighborsPS i j s w "imp"
                                            workerslist.[rNum] <! PushSumTwoDimImp (x, y, s, w)
                                            mailbox.Self <! PushSumTwoDimImp(i, j, s, w)
                                        elif pushsumcount = pushsummax then
                                            terminated <- true
                                            bossActorRef <! Termination "terminate"
            | PushSumLine (idx, s_new, w_new) -> 
                                    if not terminated then 
                                        count <- count + 1
                                        if count = 1 then
                                            s <- (idx |> float)
                                        let oldSbyW: float = s / w
                                        // printfn "Actor %i -> Old S by W: %A" idx oldSbyW
                                        s <- s + s_new
                                        w <- w + w_new
                                        s <- s / divergence
                                        w <- w / divergence
                                        let newSbyW: float = s / w
                                        // printfn "Actor %i -> New S by W: %A" idx newSbyW
                                        let rNum = genRandomNumber numNodes
                                        let diff = Math.Abs(oldSbyW - newSbyW)
                                        // printfn "Actor %i -> Difference: %A" idx diff
                                        if count = 1 || diff > pushsumtermination then
                                            pushsumcount <- 0
                                            lineNeighborsPS idx s w
                                            mailbox.Self <! PushSumLine (idx, s, w)
                                        elif pushsumcount < pushsummax then
                                            pushsumcount <- pushsumcount + 1
                                            lineNeighborsPS idx s w
                                            mailbox.Self <! PushSumLine(idx, s, w)
                                        elif pushsumcount = pushsummax then
                                            terminated <- true
                                            bossActorRef <! Termination "terminate"  
            | ExhaustedNode -> 
                            if not terminated then
                                nterminated <- nterminated + 1
                            if topology = "full" then 
                                if nterminated = numNodes-1 then 
                                    terminated <- true
                                    bossActorRef <! Termination "terminate"
            return! loop()
        }
    loop()


// Master actor for gossip
let bossActorGossip (mailbox:Actor<_>) =
    let mutable count: int = 0
    let mutable nodes: int = 0
    let rec loop() = 
        actor{
            let! msg = mailbox.Receive()
            match msg with
            | StartPoint (actorRefr, topology, numNodes, idx) ->
                                        timer.Start()
                                        nodes <- numNodes 
                                        printfn "Topology(%s) built" topology
                                        if topology = "full" then
                                            actorRefr <! GossipFull (0, "rumour")
                                        elif topology = "2d" then
                                            actorRefr <! GossipTwoDim (0, 0, "rumour")
                                        elif topology = "line" then
                                            actorRefr <! GossipLine (0, "rumour")
                                        elif topology = "2dimp" then
                                            actorRefr <! GossipTwoDimImp (0, 0, "rumour")
                                        else
                                            printfn "Invalid Topology"
                                            mailbox.Context.System.Terminate() |> ignore

            | Termination t ->  count <- count + 1
                                if count = nodes then
                                    let time = timer.ElapsedMilliseconds
                                    printfn "Topology converged in %i milliseconds" time
                                    mailbox.Context.System.Terminate() |> ignore
            return! loop()
        }
    loop()

// Master actor for Push Sum
let bossActorPushSum (mailbox:Actor<_>) =
    let mutable count: int = 0
    let mutable nodes: int = 0
    let sumInit: float = 0.0
    let weightInit: float = 1.0
    let rec loop() = 
        actor{
            let! msg = mailbox.Receive()
            match msg with
            | StartPoint (actorRefr, topology, numNodes, idx) ->
                                        timer.Start()
                                        nodes <- numNodes 
                                        printfn "Topology(%s) built" topology
                                        if topology = "full" then
                                            actorRefr <! PushSumFull (idx, sumInit, weightInit)
                                        elif topology = "2d" then
                                            actorRefr <! PushSumTwoDim (0, 0, sumInit, weightInit)
                                        elif topology = "line" then
                                            actorRefr <! PushSumLine (0, sumInit, weightInit)
                                        elif topology = "2dimp" then
                                            actorRefr <! PushSumTwoDimImp (0, 0, sumInit, weightInit)
                                        else
                                            printfn "Inavlid Topology"
                                            mailbox.Context.System.Terminate() |> ignore

            | Termination t ->  count <- count + 1
                                if count = nodes then
                                    let time = timer.ElapsedMilliseconds
                                    printfn "Topology converged in %i milliseconds" time
                                    mailbox.Context.System.Terminate() |> ignore
            return! loop()
        }
    loop()

// Spawning the boss actor using the actor reference
let mutable algoAvail = true
if algorithm = "gossip" then
    workerslist <- [for k in 1..numNodes do yield(spawn system ("WorkerActorGSP-" + (k |> string)) workerActorGossip)]
    bossActorRef <- spawn system "BossActorGossip" bossActorGossip
elif algorithm = "pushsum" then
    workerslist <- [for k in 1..numNodes do yield(spawn system ("WorkerActorPS-" + (k |> string)) workerActorPushSum)]
    bossActorRef <- spawn system "BossActorPushSum" bossActorPushSum
else 
    printfn "Invalid Algorithm"
    algoAvail <- false

// Program runs only if algorithm is either gossip or pushsum
if algoAvail then
    let actorNum = genRandomNumber numNodes
    let firstActorRef = workerslist.Item(actorNum)
    bossActorRef <! StartPoint (firstActorRef, topology, numNodes, actorNum)
    system.WhenTerminated.Wait()
    
