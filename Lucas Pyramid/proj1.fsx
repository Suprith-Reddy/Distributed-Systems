(* 
Created by Suprith Gurudu, and Hima Tejaswi
*)

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

// Discriminated Union for inputs of boss and worker actors
type InputArgs = InputArgs of int64 * int64
type WorkerArgs = WorkerArgs of int64 * int64

// Creating system for the actors
let system = System.create "proj1-system" <| Configuration.load()

// Function for calculating Lucas' Square Pyramid: perfect squares that are sums of consecutive
// squares and printing the starting point value
let LucasSquarePyramid (start:int64) (window:int64) = 
    let mutable sum: int64 = 0L
    for j in start..(start+window-1L) do
        sum <- sum + (j * j)
    let sqrtsum:float = sum |> float |> sqrt
    let ceilsqrt:float = sqrtsum |> ceil
    if sqrtsum = ceilsqrt then
        printfn "%i\n" start

// Worker Actor reference, it's mailbox, and computation inside it
let workerActor (mailbox:Actor<_>) = 
    let rec loop() =
        actor{
            let! msg = mailbox.Receive()
            match msg with
            | WorkerArgs (start, window) ->  LucasSquarePyramid start window
            return! loop()
        }
    loop()

// Boss Actor reference, it's mailbox, and spawning the worker actors
let bossActor (mailbox: Actor<_>) = 
    let rec loop() = 
        actor{
            let! msg = mailbox.Receive()
            match msg with
            | InputArgs (range, width) -> 
                    let mutable i: int64 = 1L
                    let workerslist = [for k in 1..100 do yield(spawn system ("WorkerActor-" + (k |> string)) workerActor)]
                    while i <= range do
                        let workerTuple = (i, width)
                        let modval: int = (i % 100L) |> int
                        workerslist.Item(modval) <! WorkerArgs workerTuple
                        i <- i + 1L 
            return! loop()
        }
    loop()

// Getting the command arguments as Integers
let args = Environment.GetCommandLineArgs()
let bossTuple = (args.[3] |> int64, args.[4] |> int64)

// Spawning the boss actor using the actor reference
let bossActorRef = spawn system "BossActor" bossActor
bossActorRef <! InputArgs bossTuple
    
// Used to hold the program from exiting before worker actors execution
System.Console.ReadLine() |> ignore


