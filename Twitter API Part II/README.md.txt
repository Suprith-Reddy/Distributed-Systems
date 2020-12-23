Project 4 Part II: Twitter API using Suave (an alternative to WebSharper), Websockets and Akka framework.

Project Team Members:
Suprith Reddy Gurudu, UF ID: 9961-2134
Hima Tejaswi Gummadi, UF ID: 2455-9492

Project Implementation/Description:
Use WebSharper (used an alternative - Suave) web framework to implement a WebSocket interface to your part I implementation. That means that, even though the F#  implementation (Part I) you could use AKKA messaging to allow client-server implementation, you now need to design and use a proper WebSocket interface. Specifically:

You need to design a JSON based API that  represents all messages and their replies (including errors)
You need to re-write parts of your engine using WebSharper to implement the WebSocket interface
You need to re-write parts of your client to use WebSockets.

We have used Postman to handle JSON API requests for clients and chrome browser to display the Live Feed of each client. 


How to Run:
In the project directory,
>> dotnet run

Open Postman, 
To register a client with the example POST request body { "UserName" : "client1", "Password": "123456" }
>> http://localhost:8080/register 

To Login a client with the example POST request body { "UserName" : "client1", "Password": "123456" }
>> http://localhost:8080/login

To Logout a client with the example POST request body { "LOUserName" : "client1" }
>> http://localhost:8080/logout

To Follow another client with the POST request example body { "UserName" : "client1", "Follow": "client2" }
>> http://localhost:8080/followuser

To Send Tweet by a client with the example POST request body { "UserName" : "client1", "Message": "Hi, @client2 #UF" }
>> http://localhost:8080/sendtweet

To Retrieve Hashtag data by a client with the example POST request body { "UserName" : "client1", "HashTag": "UF" }
>> http://localhost:8080/hashtag

Open browser with urls (used three clients for demo) to check the Live Feed,
For client-1
>> http://localhost:8080/

For client-2
>> http://localhost:8080/2/

For client-3
>> http://localhost:8080/3/


Implementation Code:
Program.fs (contains server related functionality)
-> LiveFeedActor handles the live feed of each client that sends the messages asynchronously to the websocket interface.
-> Includes few data structure maps for user details, mentions, hashtags, and web sockets.
-> Includes utility functions for registering, logging in and out, following, sending tweets, notifying user upon any tweet or mention, and retrieving hastag information upon query.
-> Includes httpcontext (webpart of application) handlers for register, login, logout, sendtweet, follow, hashtag, etc. 

Three plain user interface files for each client (for the purpose of demo):
twitterclient.html
twitterclient2.html
twitterclient3.html

TwitterIonideProject.fsproj includes all dependency packages.
