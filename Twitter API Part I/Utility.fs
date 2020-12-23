module Utils

open System
open System.Data
open DataModel

let isRegistered (username:string) =
    let resultSeq = query{
        for user in usersTable.AsEnumerable() do
        where (user.["username"] |> string = username)
        select (user.["userid"] |> string) 
    }
    let output = Seq.toList resultSeq
    output

let isFollower (userid:string) (followerid:string) :bool =
    let resultSeq = query{
        for follower in followersTable.AsEnumerable() do
        where ((follower.["userid"] |> string = userid) && follower.["followerid"] |> string = followerid)
        select (follower)
    }
    let output = Seq.toList resultSeq
    output.Length <> 0

let getUserName (userid:string) = 
    let resultSeq = query{
        for user in usersTable.AsEnumerable() do
        where (user.["userid"]|>string = userid)
        select (user.["username"]|>string)
    }
    let output = Seq.toList resultSeq
    if output.Length > 0 then output.Item(0)
    else ""

let getUserID (username:string) =
    let userid = isRegistered username
    if userid.Length <> 0 then userid.Item(0)
    else ""

let getFollowers (userid:string) =
    let resultSeq = query{
        for follower in followersTable.AsEnumerable() do
            where (follower.["followerid"] |> string = userid)
            select (follower.["userid"] |> string)
    }
    let output = Seq.toList resultSeq
    output

let getTweets (userid:string) =
    let resultSeq = query{
        for tweet in tweetsTable.AsEnumerable() do
        join tweetrelation in usersTweetsMappingTable.AsEnumerable() on
            ((tweet.["tweetid"] |> string) = (tweetrelation.["tweetid"] |> string))
        where (tweetrelation.["userid"] |> string=userid)
        select("TWEET-ID:{"+(tweet.["tweetid"] |> string)+"} with TWEET:{"+(tweet.["tweet"] |> string)+"} AT {"+(tweet.["tweettime"] |> string)+"} BY {"+(getUserName (tweetrelation.["ownerid"]|>string))+"}\n")
    }
    let output = Seq.toList resultSeq
    output

let getMentions (userid:string) = 
    let resultSeq = query {
        for tweet in tweetsTable.AsEnumerable() do
        join mention in mentionsTable.AsEnumerable() on
            ((tweet.["tweetid"] |> string) = (mention.["tweetid"] |> string))
        where (mention.["userid"] |> string =userid)
        select ("MENTION BY {"+(getUserName (mention.["mentionedby"] |> string))+"} in the TWEET:{"+(tweet.["tweet"] |> string)+"} with TWEET-ID:{"+(mention.["tweetid"] |> string)+"}\n") 
    }
    let output = Seq.toList resultSeq
    output

let getAllHashtags = 
    let mutable output = ""
    let table = dataSet.Tables.Item("Hashtags_Table")
    for i in 0..table.Rows.Count-1 do
        let rows = table.Rows.Item(i)
        output <- output + (rows.[0]|>string) + ";"
    output

let addUser (username:string) (userid:string) = 
    usersTable.Rows.Add(username, userid) |> ignore
    usersTable.AcceptChanges()

let addFollower (username:string) (followername:string) =
    let followerid = getUserID followername
    let userid = getUserID username
    if not(isFollower userid followerid) then
        followersTable.Rows.Add(userid, followerid) |> ignore
        followersTable.AcceptChanges()

let addTweet (tweetid:string) (tweet:string) =
    tweetsTable.Rows.Add(tweetid, tweet, DateTime.Now) |> ignore
    tweetsTable.AcceptChanges()

let addHashtag (userid:string) (tweetid:string) (hashtag:string) = 
    hashtagsTable.Rows.Add(hashtag, userid, tweetid) |> ignore
    hashtagsTable.AcceptChanges()

let addMention (userid:string) (tweetid:string) (mention:string) =
    let mentionedusername = (mention).Split '@'
    let mentioneduserid = getUserID mentionedusername.[1]
    mentionsTable.Rows.Add(mentioneduserid, userid, tweetid) |> ignore
    mentionsTable.AcceptChanges()

let addUserTweetMapping (userid:string) (ownerid:string) (tweetid:string) =
    usersTweetsMappingTable.Rows.Add(userid, ownerid, tweetid) |> ignore
    usersTweetsMappingTable.AcceptChanges()

