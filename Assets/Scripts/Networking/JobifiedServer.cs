using System.Net;
using UnityEngine;

using Unity.Networking.Transport;
using Unity.Collections;
using UnityEngine.Assertions;
using Unity.Networking.Transport.Utilities;
using Unity.Jobs;

public class JobifiedServer : MonoBehaviour
{
    public UdpNetworkDriver m_Driver;
    public NativeList<NetworkConnection> m_Connections;
    private JobHandle serverJobHandle;
    public NetworkPipeline m_Unreliable_Pipeline;
    public NetworkPipeline m_Reliable_Pipeline;

    public int connectionCapacity = 16;

    const int k_PacketSize = 256;

    void Start()
    {
        ReliableUtility.Parameters reliabilityParams = new ReliableUtility.Parameters { WindowSize = 32 };
        SimulatorUtility.Parameters simulatorParams = new SimulatorUtility.Parameters { MaxPacketSize = k_PacketSize, MaxPacketCount = 30, PacketDelayMs = 100 };

        m_Driver = new UdpNetworkDriver(simulatorParams, reliabilityParams);
        m_Connections = new NativeList<NetworkConnection>(connectionCapacity, Allocator.Persistent); // first parameter is number of connections to accept

        m_Unreliable_Pipeline = m_Driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage), typeof(SimulatorPipelineStage));
        m_Reliable_Pipeline = m_Driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage), typeof(SimulatorPipelineStage));

        NetworkEndPoint endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = 9000;
        if (m_Driver.Bind(endpoint) != 0)
            Debug.Log("Failed to bind to port 9000");
        else
            m_Driver.Listen();

        
    }

    public void OnDestroy()
    {
        serverJobHandle.Complete();
        m_Driver.Dispose();
        m_Connections.Dispose();
    }

    void Update()
    {
        serverJobHandle.Complete();

        ServerUpdateConnectionsJob connectionJob = new ServerUpdateConnectionsJob
        {
            driver = m_Driver,
            connections = m_Connections,
        };

        ServerUpdateJob serverUpdateJob = new ServerUpdateJob
        {
            driver = m_Driver.ToConcurrent(),
            connections = m_Connections.AsDeferredJobArray(),
            pipeline = m_Reliable_Pipeline
        };

        serverJobHandle = m_Driver.ScheduleUpdate();
        serverJobHandle = connectionJob.Schedule(serverJobHandle);
        serverJobHandle.Complete();
        //Debug.Log(m_Connections.Length);
        serverJobHandle = serverUpdateJob.Schedule(m_Connections.Length, 1, serverJobHandle);
    }
}

struct ServerUpdateConnectionsJob : IJob
{
    public UdpNetworkDriver driver;
    public NativeList<NetworkConnection> connections;

    public void Execute()
    {
        // Clean up connections
        for (int i = 0; i < connections.Length; i++)
        {
            if (!connections[i].IsCreated)
            {
                connections.RemoveAtSwapBack(i);
                --i;
            }
        }
        // Accept new connections
        NetworkConnection c;
        while ((c = driver.Accept()) != default(NetworkConnection))
        {
            Debug.Log("Accepted a connection");
            // spawn new player and have its name be the same as its index in connections/players
            // create new player information to keep track of this player
        }
    }
}

struct ServerUpdateJob : IJobParallelFor
{
    public UdpNetworkDriver.Concurrent driver;
    public NativeArray<NetworkConnection> connections;
    public NetworkPipeline pipeline;

    public void Execute(int index)
    {
        DataStreamReader stream;
        if (!connections[index].IsCreated)
            Assert.IsTrue(true);

        NetworkEvent.Type cmd;
        while ((cmd = driver.PopEventForConnection(connections[index], out stream)) != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Data)
            {
                var readerCtx = default(DataStreamReader.Context);
                uint cmdType = stream.ReadUInt(ref readerCtx);

                if(cmdType == CommandType.Input)
                {

                }
                else if(cmdType == CommandType.SpawnNewPlayer)
                {

                }
                else if(cmdType == CommandType.LoadLevel)
                {
                    // send the new client all of the existing players' position information
                    // format of the message: CommandType.LoadLevel;0; Player 0 position (ex: 1,1,1);1; Player 1 position; ...
                    /*string data = CommandType.LoadLevel.ToString();
                    data += ';';
                    for (int i = 0; i < connections.Length; i++)
                    {
                        if (connections[i].IsCreated)
                        {
                            // get connection info
                            string playerInfo = "0;";
                            
                        }
                    }
                    using (var writer = new DataStreamWriter(4, Allocator.Temp))
                    {
                        writer.Write(number);
                        driver.Send(pipeline, connections[index], writer);
                    }*/

                }

                /*using (var writer = new DataStreamWriter(4, Allocator.Temp))
                {
                    writer.Write(number);
                    driver.Send(pipeline, connections[index], writer);
                }*/
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                Debug.Log("Client disconnected from server");
                connections[index] = default(NetworkConnection);
            }
        }
    }
}