using System.Net;
using UnityEngine;

using Unity.Networking.Transport;
using Unity.Collections;
using UnityEngine.Assertions;
using Unity.Networking.Transport.Utilities;
using Unity.Jobs;

public class NetworkServer : MonoBehaviour
{
    public UdpNetworkDriver m_Driver;
    public NativeList<NetworkConnection> m_Connections;
    private JobHandle ServerJobHandle;
    public NetworkPipeline m_Pipeline;

    const int k_PacketSize = 256;

    void Start()
    {
        m_Driver = new UdpNetworkDriver(new SimulatorUtility.Parameters { MaxPacketSize = k_PacketSize, MaxPacketCount = 30, PacketDelayMs = 100});
        m_Pipeline = m_Driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage), typeof(SimulatorPipelineStage));

        NetworkEndPoint endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = 9000;
        if (m_Driver.Bind(endpoint) != 0)
            Debug.Log("Failed to bind to port 9000");
        else
            m_Driver.Listen();

        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent); // first parameter is number of connections to accept

    }

    public void OnDestroy()
    {
        ServerJobHandle.Complete();
        m_Driver.Dispose();
        m_Connections.Dispose();
    }

    void Update()
    {
        ServerJobHandle.Complete();

        ServerUpdateConnectionsJob connectionJob = new ServerUpdateConnectionsJob
        {
            driver = m_Driver,
            connections = m_Connections
        };

        ServerUpdateJob serverUpdateJob = new ServerUpdateJob
        {
            driver = m_Driver.ToConcurrent(),
            connections = m_Connections.AsDeferredJobArray(),
            pipeline = m_Pipeline
        };

        ServerJobHandle = m_Driver.ScheduleUpdate();
        ServerJobHandle = connectionJob.Schedule(ServerJobHandle);
        ServerJobHandle = serverUpdateJob.Schedule(m_Connections, 1, ServerJobHandle);
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
            connections.Add(c);
            Debug.Log("Accepted a connection");
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
                uint number = stream.ReadUInt(ref readerCtx);

                Debug.Log("Got " + number + " from the Client adding + 2 to it.");
                number += 2;

                using (var writer = new DataStreamWriter(4, Allocator.Temp))
                {
                    writer.Write(number);
                    driver.Send(pipeline, connections[index], writer);
                }
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                Debug.Log("Client disconnected from server");
                connections[index] = default(NetworkConnection);
            }
        }
    }
}