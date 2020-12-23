open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.ServerErrors
open Suave.Writers
open Suave.Files

open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket

open Newtonsoft.Json
open System.Data
open System
open System.Collections.Generic

open Akka
open Akka.Remote
open Akka.FSharp
open Akka.Routing

[<Class>]
type UserData(followers: List<string>, following: List<string>, tweets: List<string>) = 
    let subscribers: List<string> = followers
    let subscribedTo: List<string> = following
    let usertweets: List<string> = tweets

    member d.Followers
        with get() = subscribers
        // and set flwrs = subscribers <- flwrs
    
    member d.Following
        with get() = subscribedTo
        // and set flwrs = subscribedTo <- flwrs

    member d.Tweets
        with get() = usertweets
        // and set twts = usertweets <- twts

[<Class>]
type UserDetails(email: string, pswd: string, conn: bool, data: UserData) =
    let mutable password: string = pswd
    let mutable connection: bool = conn
    let mutable userdata: UserData = data

    member u.Email = email

    member u.Password
        with get() = password
        and set ps = password <- ps

    member u.Connection
        with get() = connection
        and set cn = connection <- cn

    member u.Data
        with get() = userdata
        and set dt = userdata <- dt


type RegisterUser =
    {
        UserName:string
        Password:string
    }

type RespRegLogFollowUser =
    {
        Success: int
        Message: string
    }

type LoginUser = 
    {
        UserName:string
        Password:string
    }

type LogoutUser = 
    {
        LOUserName: string
    }

type FollowUser = 
    {
        UserName: string
        Follow: string
    }

type SendTweet = 
    {
        UserName: string
        Message: string
    }

type GetUserTweets = 
    {
        Success: int
        Message: string
        Tweets: List<string> 
    }

type HashtagToTweet =
    {
        UserName: string
        HashTag: string
    }

type NewTweet =
    {
        UserName: string
        Tweet: string
    }

let mutable usermap: Map<string, UserDetails> = Map.empty
let mutable mentions: Map<string, List<string>> = Map.empty
let mutable hashtags: Map<string, List<string>> = Map.empty
let mutable usersktmap: Map<string, WebSocket> = Map.empty 

let twitterSystem = System.create "project4-2-system" <| Configuration.load()


type LiveFeedMsg = 
    | SendingTweet of WebSocket * NewTweet
    | SendingMention of WebSocket * string
    | SelfTweet of WebSocket * NewTweet
    | Following of WebSocket * string
    | HashTagTweets of WebSocket * string

let LiveFeedActor (mailbox:Actor<_>) =
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with  
        | SelfTweet (ws, tweet) -> 
            let resp = "Tweeted {"+tweet.Tweet+"}"
            let byteResp = resp |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
            printfn "Sending message to %A" ws
            let skt = socket{ do! ws.send Text byteResp true }
            Async.StartAsTask skt |> ignore
        | SendingTweet (ws, tweet) -> 
            let resp = tweet.UserName+" has tweeted {"+tweet.Tweet+"}"
            let byteResp = resp |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
            printfn "Sending message to %A" ws
            let skt = socket{ do! ws.send Text byteResp true }
            Async.StartAsTask skt |> ignore
        | SendingMention (ws, tweet) -> 
            let resp = tweet
            let byteResp = resp |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
            printfn "Sending message to %A" ws
            let skt = socket{ do! ws.send Text byteResp true }
            Async.StartAsTask skt |> ignore
        | Following (ws, flwr) -> 
            let resp = flwr
            let byteResp = resp |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
            printfn "Sending message to %A" ws
            let skt = socket{ do! ws.send Text byteResp true }
            Async.StartAsTask skt |> ignore
        | HashTagTweets (ws, dt) -> 
            let resp = dt
            let byteResp = resp |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
            printfn "Sending message to %A" ws
            let skt = socket{ do! ws.send Text byteResp true }
            Async.StartAsTask skt |> ignore
        return! loop()
    }
    loop()

let liveFeedRef = spawn twitterSystem "livefeed" LiveFeedActor

let getString (rawForm: byte[]) =
    System.Text.Encoding.UTF8.GetString(rawForm)

let fromJson<'a> json =
    JsonConvert.DeserializeObject(json, typeof<'a>) :?> 'a


let addUser (user: RegisterUser) =
    let userData = UserData (new List<string>(), new List<string>(), new List<string>())
    let newuser = UserDetails (user.UserName, user.Password, false, userData)
    usermap <- usermap.Add(user.UserName, newuser)
    { Success = 1; Message = "Registered Successfully" }

let loginUser (user: LoginUser)= 
    let found = usermap.TryFind user.UserName
    match found with
    | Some usr -> 
        if user.Password = usr.Password then
            usr.Connection <- true
            { Success = 1; Message = "Logged in successfully" }
        else 
            { Success = 0; Message = "Incorrect password" }
    | None -> 
        { Success = 0; Message = "No User found" }

let logoutUser (user: LogoutUser) = 
    let found = usermap.TryFind user.LOUserName
    match found with
    | Some usr -> 
        usr.Connection <- false
        { Success = 1; Message = "Logged out successfully" }
    | None -> 
        { Success = 0; Message = "No User found" }

let broadcastTweet (user: SendTweet) =
    let name = user.UserName
    let data = user.Message
    let usrtable = (usermap.TryFind name).Value
    let followerList = usrtable.Data.Followers
    for i in followerList do
        let sendskt = (usersktmap.TryFind i).Value
        let newtwt = { UserName = name; Tweet = data }
        liveFeedRef <! SendingTweet (sendskt, newtwt)

let notifyMention (username: string) (tweetWithDetails: string) =
    let sendskt = (usersktmap.TryFind username).Value
    liveFeedRef <! SendingMention (sendskt, tweetWithDetails)

let fillMentionsOrHashtagsTable (user: SendTweet) =
    let name = user.UserName
    let data = user.Message
    let wordList = data.Split " "
    for i in wordList do
        if i.StartsWith "@" then
            let mentionedUN = i.[1..]
            // printfn "%i" mentionedUN.Length
            let newstr = name+" mentioned you in the tweet {"+data+"}"
            notifyMention mentionedUN newstr
            let mentionsList = mentions.TryFind mentionedUN
            if mentionsList.IsSome then
                let mlist = mentionsList.Value
                mlist.Add(newstr)
                mentions <- mentions.Add(mentionedUN, mlist) 
            else
                let mlist = new List<string>()
                mlist.Add(newstr)
                mentions <- mentions.Add(mentionedUN, mlist) 
        else if i.StartsWith "#" then
            let tagged = i.[1..]
            let newstr = name+" tweeted in {"+data+"}"
            let hashtagList = hashtags.TryFind tagged
            if hashtagList.IsSome then
                let mlist = hashtagList.Value
                mlist.Add(newstr)
                hashtags <- hashtags.Add(tagged, mlist) 
            else
                let mlist = new List<string>()
                mlist.Add(newstr)
                hashtags <- hashtags.Add(tagged, mlist)

let sndTweet (user: SendTweet) = 
    let found = usermap.TryFind user.UserName
    match found with
    | Some usr -> 
        if usr.Connection then
            usr.Data.Tweets.Add(user.Message)
            let newtwt = { UserName = user.UserName; Tweet = user.Message }
            let skt = usersktmap.TryFind user.UserName
            if skt <> None then
                let someskt = skt.Value
                liveFeedRef <! SelfTweet (someskt,newtwt)
            broadcastTweet user
            fillMentionsOrHashtagsTable user
            { Success = 1; Message = "User tweeted successfully" }
        else
            { Success = 0; Message = "User has to sign in first to tweet" }
    | None -> 
        { Success = 0; Message = "No User found" }

let findUserDetails (username: string) = 
    let found = usermap.TryFind username
    match found with
    | Some user ->
        user
    | None -> failwith "Not found"

let followUser (flw: FollowUser) =
    let x = findUserDetails flw.UserName
    let y = findUserDetails flw.Follow
    if x.Connection then
        x.Data.Following.Add(y.Email)
        y.Data.Followers.Add(x.Email)
        let sendskt2 = (usersktmap.TryFind flw.UserName).Value
        let newstr1 = "You followed "+flw.Follow
        liveFeedRef <! Following (sendskt2, newstr1)
        let sendskt2 = (usersktmap.TryFind flw.Follow).Value
        let newstr2 = flw.UserName+" followed you!!"
        liveFeedRef <! Following (sendskt2, newstr2)
        { Success = 1; Message = "Follower added successfully" }    
    else
        { Success = 0; Message = "User has to be signed in first to follow someone" }  

let retrieveTaggedTweets (ht: HashtagToTweet) =
    let tweetList = (hashtags.TryFind ht.HashTag).Value
    let sendskt = (usersktmap.TryFind ht.UserName).Value
    let firstmsg = "Tweets Information related to Hashtag: "+ht.HashTag+"--"
    liveFeedRef <! HashTagTweets (sendskt, firstmsg)
    for i in tweetList do
        liveFeedRef <! HashTagTweets (sendskt, i)
    { Success = 1; Message = "Retrieved all tweets related to hashtag queried" } 

let userTweets (name: string) =
    let found = usermap.TryFind name
    match found with
    | Some usr -> 
        let twts = usr.Data.Tweets
        { Success = 1; Message = "User Tweets Extracted"; Tweets = twts}
    | None -> 
        { Success = 0; Message = "No User found"; Tweets = List<string>() }


let regUser = 
    request (fun r ->
    r.rawForm
    |> getString
    |> fromJson<RegisterUser>
    |> addUser
    |> JsonConvert.SerializeObject
    |> CREATED )
    >=> setMimeType "application/json"

let logUser = 
    request (fun r ->
    r.rawForm
    |> getString
    |> fromJson<LoginUser>
    |> loginUser
    |> JsonConvert.SerializeObject
    |> CREATED )
    >=> setMimeType "application/json"

let logOtUser = 
    request (fun r ->
    r.rawForm
    |> getString
    |> fromJson<LogoutUser>
    |> logoutUser
    |> JsonConvert.SerializeObject
    |> CREATED )
    >=> setMimeType "application/json"

let sendTweetByUser =
    request (fun r ->
    r.rawForm
    |> getString
    |> fromJson<SendTweet>
    |> sndTweet
    |> JsonConvert.SerializeObject
    |> CREATED )
    >=> setMimeType "application/json"

let followAUser=
    request (fun r ->
    r.rawForm
    |> getString
    |> fromJson<FollowUser>
    |> followUser
    |> JsonConvert.SerializeObject
    |> OK )
    >=> setMimeType "application/json"

let tweetsForHashtag =
    request (fun r ->
    r.rawForm
    |> getString
    |> fromJson<HashtagToTweet>
    |> retrieveTaggedTweets
    |> JsonConvert.SerializeObject
    |> OK )
    >=> setMimeType "application/json"

let getAllTweetsOfUser name = 
    userTweets name
    |> JsonConvert.SerializeObject
    |> OK
    >=> setMimeType "application/json"


let ws (webSocket : WebSocket) (context: HttpContext) =
  socket {
    let mutable loop = true

    while loop do
      let! msg = webSocket.read()

      match msg with
      | (Text, data, true) ->
        let str = UTF8.toString data
        
        if str.StartsWith("UserName:") then
            let un = str.Split(':').[1]
            usersktmap <- usersktmap.Add(un, webSocket)
            printfn "%s websocket" un
        else
            let response = sprintf "response to %s" str
            let byteResponse =
              response
              |> System.Text.Encoding.ASCII.GetBytes
              |> ByteSegment
            do! webSocket.send Text byteResponse true

      | (Close, _, _) ->
        let emptyResponse = [||] |> ByteSegment
        do! webSocket.send Close emptyResponse true

        loop <- false

      | _ -> ()
    }

let app =
    choose
        [ 
          path "/websocket" >=> handShake ws
          path "/2/websocket" >=> handShake ws
          path "/3/websocket" >=> handShake ws
          
          GET >=> choose
            [ path "/" >=> file "twitterclient.html"; browseHome 
              path "/2/" >=> file "twitterclient2.html"; browseHome
              path "/3/" >=> file "twitterclient3.html"; browseHome
              pathScan "/usertweets/%s" (fun name -> getAllTweetsOfUser name)]

          POST >=> choose
            [ path "/register" >=> regUser
              path "/login" >=> logUser
              path "/logout" >=> logOtUser
              path "/sendtweet" >=> sendTweetByUser
              path "/followuser" >=> followAUser 
              path "/hashtag" >=> tweetsForHashtag]

          PUT >=> choose
            [ ]

          DELETE >=> choose
            [ ]
        ]

[<EntryPoint>]
let main argv =
    startWebServer defaultConfig app
    0