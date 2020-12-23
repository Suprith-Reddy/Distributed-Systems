#time "on"
#r "nuget: Akka"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"
#r "nuget: Akka.Remote"

open System
open System.Threading
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.TestKit

type MessageMaster =
    | InitFinished
    | StartPastry
    | JoinFinished
    | RequestsFinished of float

type NodeMsg =
    | InitNode
    | JoinNode of IActorRef*string
    | UpdateTable of Set<string>*Set<string>*array<string>*string*string
    | SendMessage
    | RouteMessage of string*int*IActorRef
    | KeySearch
    | Acknowledgement of int
    
// Getting command line arguments
let mutable numNodes = fsi.CommandLineArgs.[1] |> int
let mutable numRequests = fsi.CommandLineArgs.[2] |> int
// Setting hexadecimal length
let mutable hexLen = 5
let rand = Random(1)

// Creates actor system
let system = ActorSystem.Create("Pastry-System")
let mutable nodeList = []

// generates and fills a map with random nodes
let mutable temp = Map.empty
let genNodeId id =
    let max = Math.Pow(2.0, 4.0 * (hexLen|>float)) |> int
    let mutable num = rand.Next(1, max)
    while temp.TryFind num <> None do
        num <- rand.Next(1, max)
    temp <- temp.Add(num, true)
    num.ToString("X5")

// generates random node
let genRandomNode =
    let max = Math.Pow(2.0, 4.0 * (hexLen|>float)) |> int
    let mutable num = rand.Next(1, max)
    num.ToString("X5")

// hexadecimal difference
let hexDiff a b =
    let aInt = Convert.ToInt32(a, 16)
    let bInt = Convert.ToInt32(b, 16)
    abs(aInt-bInt) 

// finds the prefix length (name suggests from pastry API)
let shl (a:string) (b:string) =
    let mutable cnt = 0
    let mutable flag = false
    for i in 0 .. a.Length-1 do
        if (a.Chars(i)=b.Chars(i)) && (not flag) then
            cnt <- cnt+1
        else
            flag <- true
    cnt

// adds the node id to the routing table
let addToRoutingTable a b (table:string array array)=
    let prefix = shl a b
    if prefix <> hexLen then
        let temp = a.Chars(prefix) |> int
        if temp > 64 then 
            table.[prefix].[temp-55] <- a
        else
            table.[prefix].[temp-48] <- a

// broadcasts the routing table status to all nodes in the network
let broadcastRoutingTable nodeId (smallerleafset:Set<string>) (largerleafset:Set<string>) (rt:string array array) = 
    for i in smallerleafset do
        if i.Length <> 0 then
            let prefix = shl nodeId i
            if prefix <> hexLen then
                system.ActorSelection("user/"+i) <! UpdateTable(smallerleafset, largerleafset, rt.[prefix], nodeId, "")
    for i in largerleafset do
        if i.Length <> 0 then
            let prefix = shl nodeId i
            if prefix <> hexLen then
                system.ActorSelection("user/"+i) <! UpdateTable(smallerleafset, largerleafset, rt.[prefix], nodeId, "")
    for j in rt do
        for i in j do
            if i.Length <> 0 then
                let prefix = shl nodeId i
                if prefix <> hexLen then
                    system.ActorSelection("user/"+i) <! UpdateTable(smallerleafset, largerleafset, rt.[prefix], nodeId, "")
    
// gets the next nodes id or address
let nextNodeID key nodeId (smallerleafset:Set<string>) (largerleafset:Set<string>) (rt:string array) = 
    let mutable diff = hexDiff key nodeId
    let mutable res = nodeId
    let mutable sleafset = Set.empty
    sleafset <- smallerleafset
    sleafset<- sleafset.Add(nodeId)
    let mutable lleafset = Set.empty
    lleafset <- largerleafset
    lleafset<- lleafset.Add(nodeId)
    if key >= sleafset.MinimumElement && key <= lleafset.MaximumElement then
        for i in sleafset do
            let tmp = hexDiff key i
            if  tmp < diff then
                diff <- tmp
                res <- i
        for i in lleafset do
            let tmp = hexDiff key i
            if  tmp < diff then
                diff <- tmp
                res <- i
        res
    else
        for k in rt do
            if k.Length <> 0 then
                let mutable tmp = hexDiff key k
                if tmp < diff then
                    diff <- tmp
                    res <- k
        res

// Pastry nodes in the network
let PastryNode(mailbox:Actor<_>)=
    let mutable neighbourhoodSet = []
    let mutable slfset = Set.empty
    let mutable llfset = Set.empty
    let mutable cancelScheduler = []
    let mutable ackcount = 0.0
    let mutable hopackcount = 0.0
    let mutable sendcount = 0
    let mutable routingtable = Array.create hexLen [||]
    for i in 0 .. hexLen-1 do
        routingtable.[i] <- Array.create 16 ""
    let mutable nodeId = mailbox.Self.Path.Name
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with 
            | InitNode ->   
                mailbox.Sender() <! InitFinished
            | JoinNode(nodeRef,key) -> 
                let prefix = shl key nodeId
                if prefix = hexLen then
                    nodeRef <! UpdateTable(Set.empty,Set.empty,Array.empty,nodeId,"Updated")
                else 
                    if nodeId < key  then
                        if llfset.Count < 8 then
                            llfset <- llfset.Add(key)
                        else
                            let mutable rtKey = key
                            if llfset.MaximumElement > key then
                                rtKey <- llfset.MaximumElement
                                llfset <- llfset.Remove(rtKey)
                                llfset <- llfset.Add(key)
                            addToRoutingTable rtKey nodeId routingtable
                    else
                        if slfset.Count < 8 then
                            slfset <- slfset.Add(key)
                        else
                            let mutable rtKey = key
                            if slfset.MinimumElement < key then
                                rtKey <- slfset.MinimumElement
                                slfset <- slfset.Remove(rtKey)
                                slfset <- slfset.Add(key)
                            addToRoutingTable rtKey nodeId routingtable
                    let nxt = nextNodeID key nodeId slfset slfset routingtable.[prefix]
                    if nxt.CompareTo(nodeId)=0 then 
                        nodeRef<!UpdateTable(slfset,llfset,routingtable.[prefix],nodeId,"Updated")
                    else 
                        system.ActorSelection("user/"+nxt)<!JoinNode(nodeRef,key)
                        nodeRef<!UpdateTable(slfset,llfset,routingtable.[prefix],nodeId,"")
            | UpdateTable(sleafset,lleafset,rtable,key,status) ->  
                if nodeId < key then                                               
                    if llfset.Count < 8 then
                        llfset <- llfset.Add(key)
                    else
                        let mutable rtKey = key
                        if llfset.MaximumElement > key then
                            rtKey <- llfset.MaximumElement
                            llfset <- llfset.Remove(rtKey)
                            llfset <- llfset.Add(key)
                        addToRoutingTable rtKey nodeId routingtable
                else
                    if slfset.Count < 8 then
                        slfset <- slfset.Add(key)
                    else
                        let mutable rtKey = key
                        if slfset.MinimumElement < key then
                            rtKey <- slfset.MinimumElement
                            slfset <- slfset.Remove(rtKey)
                            slfset <- slfset.Add(key)
                        addToRoutingTable rtKey nodeId routingtable
                for i in sleafset do
                    if nodeId < i  then
                        if llfset.Count < 8 then
                            llfset <- llfset.Add(i)
                        else
                            let mutable rtKey = i
                            if llfset.MaximumElement > i then
                                rtKey <- llfset.MaximumElement
                                llfset <- llfset.Remove(rtKey)
                                llfset <- llfset.Add(i)
                            addToRoutingTable rtKey nodeId routingtable
                    else
                        if slfset.Count < 8 then
                            slfset <- slfset.Add(i)
                        else
                            let mutable rtKey = i
                            if slfset.MinimumElement < i then
                                rtKey <- slfset.MinimumElement
                                slfset <- slfset.Remove(rtKey)
                                slfset <- slfset.Add(i)
                            addToRoutingTable rtKey nodeId routingtable
                for i in lleafset do
                    if nodeId < i  then
                        if llfset.Count < 8 then
                            llfset <- llfset.Add(i)
                        else
                            let mutable rtKey = i
                            if llfset.MaximumElement > i then
                                rtKey <- llfset.MaximumElement
                                llfset <- llfset.Remove(rtKey)
                                llfset <- llfset.Add(i)
                            addToRoutingTable rtKey nodeId routingtable
                    else
                        if slfset.Count < 8 then
                            slfset <- slfset.Add(i)
                        else
                            let mutable rtKey = i
                            if slfset.MinimumElement < i then
                                rtKey <- slfset.MinimumElement
                                slfset <- slfset.Remove(rtKey)
                                slfset <- slfset.Add(i)
                            addToRoutingTable rtKey nodeId routingtable
                for j in rtable do
                    if j.CompareTo("") <> 0 then
                        let i = j
                        if nodeId < i  then
                            if llfset.Count < 8 then
                                llfset <- llfset.Add(i)
                            else
                                let mutable rtKey = i
                                if llfset.MaximumElement > i then
                                    rtKey <- llfset.MaximumElement
                                    llfset <- llfset.Remove(rtKey)
                                    llfset <- llfset.Add(i)
                                addToRoutingTable rtKey nodeId routingtable
                        else
                            if slfset.Count < 8 then
                                slfset <- slfset.Add(i)
                            else
                                let mutable rtKey = i
                                if slfset.MinimumElement < i then
                                    rtKey <- slfset.MinimumElement
                                    slfset <- slfset.Remove(rtKey)
                                    slfset <- slfset.Add(i)
                                addToRoutingTable rtKey nodeId routingtable
                if status = "Updated" then
                    broadcastRoutingTable nodeId slfset llfset routingtable
                    system.ActorSelection("user/master-actor") <! JoinFinished
                    mailbox.Self<!SendMessage
            | SendMessage ->    
                cancelScheduler<-[system.Scheduler.ScheduleTellRepeatedlyCancelable(1000, 1000, mailbox.Self, KeySearch ,mailbox.Self)]
            | KeySearch ->    
                let key = genRandomNode
                mailbox.Self <! RouteMessage(key, 0, mailbox.Self)
                sendcount <- sendcount + 1
                if sendcount = numRequests then
                    cancelScheduler.[0].Cancel()
            | RouteMessage(key, hopcount, pref) ->
                let prefix = shl key nodeId
                if prefix <> hexLen then 
                    let dst = nextNodeID key nodeId slfset llfset routingtable.[prefix]
                    if dst = nodeId || dst = key then
                        pref <! Acknowledgement(hopcount)
                    else
                        system.ActorSelection("user/"+dst)<!RouteMessage(key, hopcount+1 , pref)
                else
                    pref<!Acknowledgement(hopcount)
            | Acknowledgement(hopcount) ->      
                ackcount <- ackcount + 1.0
                hopackcount <- hopackcount + (hopcount |> float)
                if ackcount = (numRequests|>float) then
                    system.ActorSelection("user/master-actor")<!RequestsFinished (hopackcount/ackcount)

        return! loop()
    }
    loop()

// Master actor
let master(mailbox:Actor<_>)=
    let mutable cnt = 0
    let mutable joincount = 0
    let mutable reqcount = 0
    let mutable hoptotal = 0.0
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with
            | StartPastry -> 
                for i in 0 .. numNodes-1 do
                    nodeList.Item(i) <! InitNode
            | InitFinished ->    
                cnt <- cnt + 1 
                if cnt = numNodes then
                    printfn "Network Initialized"
                    for i in 1 .. numNodes-1 do
                        nodeList.Item((i-1)%100)<! JoinNode(nodeList.Item(i),nodeList.Item(i).Path.Name)
            | JoinFinished ->       
                joincount <- joincount + 1
                if joincount = numNodes - 1 then
                    printfn "All pastry nodes joined the network"
            | RequestsFinished (avgHops) ->     
                reqcount <- reqcount + 1
                hoptotal <- hoptotal + avgHops
                if reqcount = numNodes - 1 then
                    printfn "Total requests: %i" (numNodes * numRequests)
                    printfn "Average hops count: %f" ((hoptotal / (reqcount|>float))*2.0)
                    mailbox.Context.System.Terminate() |> ignore

        return! loop()
    }
    loop()

// actor references list
nodeList <- [for id in 0 .. numNodes-1 do yield(spawn system (genNodeId id) PastryNode)] 

// spawning master actor
let masterRef = spawn system "master-actor" master
masterRef <! StartPastry

// terminates when all master actor terminated
system.WhenTerminated.Wait()