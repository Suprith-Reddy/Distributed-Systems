Project 4 Part 1


Team members:
Suprith Reddy Gurudu, UF ID: 9961-2134
Hima Tejaswi Gummadi, UF ID: 2455-9492

Problem description:
The goal of this project is to implement a Twitter Clone and a client tester/simulator. The problem statement is to implement an engine that (in part II) will be paired up with Web Sockets to provide full functionality.

Running Instructions:
Open two terminals – one for server code and other for client simulation and follow to the code directory
Go to the project folder in both terminals using command line.
Run the following server code in one terminal to start the twitter server engine
> dotnet fsi --langversion:preview server.fsx

Then run the client code with the following command in another terminal to start the client simulator
> dotnet fsi --langversion:preview client.fsx <serverIP> <serverPort> <SimulatorNo> <numOfUsers>
Note: here serverIP is localhost and serverPort is 1800

sample client command:
> dotnet fsi --langversion:preview client.fsx localhost 1800 10 100

Output:
The largest output managed to execute is for 10k users with time to complete execution is 2689765 milliseconds.
