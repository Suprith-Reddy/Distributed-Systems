module DataModel

open System.Data

// Model structure order matters

let dataSet = new DataSet()

let usersTable = new DataTable("Users_Table")
usersTable.Columns.Add("username") |> ignore
usersTable.Columns.Add("userid") |> ignore
usersTable.PrimaryKey = [|usersTable.Columns.["userid"]|] |> ignore
dataSet.Tables.Add(usersTable)

let usersTweetsMappingTable = new DataTable("User_Tweet_Mapping_Table")
usersTweetsMappingTable.Columns.Add("userid") |> ignore
usersTweetsMappingTable.Columns.Add("ownerid") |> ignore 
usersTweetsMappingTable.Columns.Add("tweetid") |> ignore
dataSet.Tables.Add(usersTweetsMappingTable)

let tweetsTable = new DataTable("Tweets_Table")
tweetsTable.Columns.Add("tweetid") |> ignore
tweetsTable.Columns.Add("tweet") |> ignore
tweetsTable.Columns.Add("tweettime") |> ignore
tweetsTable.PrimaryKey = [|tweetsTable.Columns.["tweetid"]|] |> ignore
dataSet.Tables.Add(tweetsTable)

let mentionsTable = new DataTable("Mentions_Table")
mentionsTable.Columns.Add("userid") |> ignore
mentionsTable.Columns.Add("mentionedby") |> ignore
mentionsTable.Columns.Add("tweetid") |> ignore
dataSet.Tables.Add(mentionsTable)

let hashtagsTable = new DataTable("Hashtags_Table")
hashtagsTable.Columns.Add("hashtag") |> ignore
hashtagsTable.Columns.Add("userid") |> ignore
hashtagsTable.Columns.Add("tweetid") |> ignore
dataSet.Tables.Add(hashtagsTable)

let followersTable = new DataTable("Followers_Table")
followersTable.Columns.Add("userid") |> ignore
followersTable.Columns.Add("followerid") |> ignore
dataSet.Tables.Add(followersTable)


dataSet.AcceptChanges()
