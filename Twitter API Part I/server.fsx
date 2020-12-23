#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
// #r "nuget: MathNet.Numerics.FSharp"
// #r "nuget: FSharp.Charting"
#load "Datamodel.fs"
#load "Utility.fs"


open System
// open MathNet.Numerics.Distributions
open System.Collections.Generic
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
// open FSharp.Charting
open DataModel
open Utils


let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            }
            remote {
                helios.tcp {
                    port = 1800
                    hostname = localhost
                }
            }
        }")

type RegistrationAuthentication =
    | RegisterUser of string * IActorRef
    | SignInUser of string * IActorRef
    | SignOutUser of string * IActorRef

type Subscription = 
    | AddFollower of string * string * IActorRef
    | UpdateFeed of string * string * IActorRef

type ParsingTweets = 
    | ParseTweet of string * string * string * IActorRef

type Tweeting =
    | AddTweet of string * string * string * IActorRef
    | GetTweets of string * IActorRef
    | GetMentions of string * IActorRef
    | GetAllHashtags of IActorRef
    | GetAllMentions of IActorRef

let random = Random()
let system = ActorSystem.Create("ServerEngine", configuration)

let hashtagFreq = Dictionary<string, int>()
let mentionFreq = Dictionary<string, int>()

let distributionUtil (tablename:string) (map:Dictionary<string, int>) =
    let table = dataSet.Tables.Item(tablename)
    for i in 0..table.Rows.Count-1 do
        let rows = table.Rows.Item(i)
        let it = (rows.[0]|>string)
        if map.ContainsKey(it) then
            map.Item(it) <- map.Item(it) + 1
        else 
            map.Add(it, 1)
    let mutable resp = ""
    let values = map.Values
    for v in values do
        resp <- resp + (v |> string) + ";"
    resp

let RegisterAuthenticateActor (mailbox:Actor<_>) =
    let mutable userid = 10000
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with 
        | RegisterUser (username, userref) -> 
            let userList = isRegistered username
            let mutable sendid = ""
            if userList.Length = 0 then
                addUser username (userid |> string)
                sendid <- userid |> string
                userid <- userid + random.Next(1, 10)
            else
                sendid <- userList.Item(0)
            userref.Tell ("Success|SignUp|"+sendid)
        | SignInUser (userid, userref) ->  
            userref.Tell ("Success|SignIn|")
        | SignOutUser (userid, userref) ->
            userref.Tell ("Success|SignOut|")

        return! loop()
    }
    loop()

let registerAuthenticateRef = spawn system "register-authenticate-actor" RegisterAuthenticateActor

let TweetParser (mailbox:Actor<_>) = 
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with 
        | ParseTweet (userid, tweetid, tweet, userRef) ->  
            let splittedTweet = (tweet).Split ' '
            for word in splittedTweet do
                if word.StartsWith '#' then
                    addHashtag userid tweetid word
                elif word.StartsWith '@' then
                    addMention userid tweetid word
            userRef.Tell ("Success|PostTweet|")
        return! loop()
    }
    loop()

let tweetParserRef = spawn system "TweetParser" TweetParser


let SubcriberActor (mailbox:Actor<_>) = 
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with
        | AddFollower (user, follower, userRef) ->     
            addFollower user follower
            userRef.Tell ("Success|Follow|")
        | UpdateFeed (ownerid, tweetid, userRef)->    
            let followers = getFollowers ownerid
            addUserTweetMapping ownerid ownerid tweetid
            for flwrid in followers do
                addUserTweetMapping flwrid ownerid tweetid
            userRef.Tell ("Success|PostTweet|")
        return! loop()
    }
    loop()

let subscriberActorRef = spawn system "subscriber-actor" SubcriberActor

let TweetingActor (mailbox:Actor<_>) =
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with
        | AddTweet(username, tweetid, tweet, userRef) ->   
            let userid = getUserID username
            addTweet tweetid tweet 
            tweetParserRef.Tell (ParseTweet (userid, tweetid, tweet, userRef))
            subscriberActorRef.Tell (UpdateFeed (userid, tweetid, userRef))
        | GetTweets(username, req) ->    
            let userid = getUserID username
            let resp = List.fold (+) "" (getTweets userid)
            req.Tell ("Response|GetTweets|"+resp)
        | GetMentions(username, req)->   
            let userid = getUserID username
            let resp = List.fold (+) "" (getMentions userid) 
            req.Tell ("Response|GetMentions|"+resp)
        | GetAllHashtags (req) ->
            let resp = distributionUtil "Hashtags_Table" hashtagFreq
            req.Tell ("Response|GetAllHashtags|"+resp)
        | GetAllMentions (req) ->
            let resp = distributionUtil "Mentions_Table" mentionFreq
            req.Tell ("Response|GetAllMentions|"+resp)
        return! loop()
    }
    loop()

let tweetingRef = spawn system "tweet-actor" TweetingActor


let mainserver = 
    spawn system "ServerRequests" <| fun mailbox ->
        let mutable requestCount = 0
        let rec loop() =
            actor {
                let! msg = mailbox.Receive()
                let senderRef = mailbox.Sender()
                requestCount <- requestCount + 1
                let instructions = (msg |> string).Split '|'
                let instr0 = instructions.[0]
                let mutable instr1 = ""
                let mutable instr2 = ""
                if instructions.Length > 1 then 
                    instr1 <- instructions.[1]
                if instructions.Length > 2 then
                    instr2 <- instructions.[2]
                if instr0.Equals("SignUp") then
                    registerAuthenticateRef.Tell (RegisterUser(instr1, senderRef))
                elif instr0.Equals("SignIn") then
                    registerAuthenticateRef.Tell (SignInUser(instr1, senderRef))
                elif instr0.Equals("SignOut") then
                    registerAuthenticateRef.Tell (SignOutUser(instr1, senderRef))
                elif instr0.Equals("Follow") then
                    subscriberActorRef.Tell (AddFollower(instr1, instr2, senderRef))
                elif instr0.Equals("PostTweet") then
                    tweetingRef.Tell (AddTweet(instr1, (requestCount |> string), instr2, senderRef))
                elif instr0.Equals("GetTweets") then
                    tweetingRef.Tell (GetTweets(instr1, senderRef))
                elif instr0.Equals("GetMentions") then
                    tweetingRef.Tell (GetMentions(instr1, senderRef))
                elif instr0.Equals("GetAllHashtags") then
                    tweetingRef.Tell (GetAllHashtags (senderRef))
                elif instr0.Equals("GetAllMentions") then
                    tweetingRef.Tell (GetAllMentions (senderRef))
                elif instr0.Equals("SaveData") then
                    dataSet.WriteXml("datastore.xml")
                    
                    
                    printfn "Total number of requests served: %i" requestCount
                    printfn "Number of requests served per second: %i" (requestCount / (instr1 |> int))
                return! loop() 
            }
        loop()


printfn "Server Status: Running"

system.WhenTerminated.Wait()

