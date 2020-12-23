Project 3: Pastry


Team members:
Suprith Reddy Gurudu, UF ID: 9961-2134
Hima Tejaswi Gummadi, UF ID: 2455-9492

Problem description:
The goal of this project is to implement in F# using the actor model the Pastry protocol and a simple object access service to prove its usefulness. 

Implementation:
Algorithms used: Pastry Protocol (Overlay Networks) with b = 4 => 2^b = 16 and l = 8.

Running Instructions:
dotnet fsi --langversion:preview project3.fsx <number_of_nodes> <number_of_requests>

Working: 
Below are the set of nodes in the network with number of requests and their average number of hops:

Number of Nodes: 100, Number of requests: 10, Average number of hops: 2.458900
Number of Nodes: 500, Number of requests: 10, Average number of hops: 3.019110
Number of Nodes: 1000, Number of requests: 10, Average number of hops: 3.637309
Number of Nodes: 2000, Number of requests: 10, Average number of hops: 3.894100
Number of Nodes: 5000, Number of requests: 10, Average number of hops: 4.307861
Number of Nodes: 10000, Number of requests: 10, Average number of hops: 4.641760
Number of Nodes: 15000, Number of requests: 10, Average number of hops: 4.824558

Largest Network managed is 15000 nodes and 10 requests.


