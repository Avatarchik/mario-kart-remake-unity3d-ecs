using System.Net;
using Unity.Collections;
using UnityEngine;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using Unity.Jobs;

public class NetworkClient : MonoBehaviour
{
    public UdpNetworkDriver m_Driver;
    public NativeArray<NetworkConnection> m_Connection;
    public NativeArray<byte> m_Done;
    public JobHandle ClientJobHandle;

    public NetworkPipeline m_Pipeline;
    
    const int k_PacketSize = 256;
    void Start()
    {
        m_Driver = new UdpNetworkDriver(new SimulatorUtility.Parameters { MaxPacketSize = k_PacketSize, MaxPacketCount = 30, PacketDelayMs = 100 });
        m_Pipeline = m_Driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage), typeof(SimulatorPipelineStage));
        m_Connection = new NativeArray<NetworkConnection>(1, Allocator.Persistent);
        m_Done = new NativeArray<byte>(1, Allocator.Persistent);

        NetworkEndPoint endpoint = NetworkEndPoint.LoopbackIpv4;
        endpoint.Port = 9000;

        m_Connection[0] = m_Driver.Connect(endpoint);

    }

    public void OnDestroy()
    {
        ClientJobHandle.Complete();

        m_Connection.Dispose();
        m_Driver.Dispose();
        m_Done.Dispose();
    }

    void Update()
    {
        ClientJobHandle.Complete();
        ClientUpdateJob job = new ClientUpdateJob
        {
            driver = m_Driver,
            connection = m_Connection,
            done = m_Done,
            pipeline = m_Pipeline
        };
        ClientJobHandle = m_Driver.ScheduleUpdate();
        ClientJobHandle = job.Schedule(ClientJobHandle);
    }

}

struct ClientUpdateJob : IJob
{
    public UdpNetworkDriver driver;
    public NativeArray<NetworkConnection> connection;
    public NativeArray<byte> done;
    public NetworkPipeline pipeline;

    public void Execute()
    {
        if (!connection[0].IsCreated)
        {
            // Remember that its not a bool anymore.
            if (done[0] != 1)
                Debug.Log("Something went wrong during connect");
            return;
        }
        DataStreamReader stream;
        NetworkEvent.Type cmd;

        while ((cmd = connection[0].PopEvent(driver, out stream)) !=
               NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                Debug.Log("We are now connected to the server");

                var value = 1;
                using (var writer = new DataStreamWriter(4, Allocator.Temp))
                {
                    writer.Write(value);
                    connection[0].Send(driver, pipeline, writer);
                }
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                var readerCtx = default(DataStreamReader.Context);
                uint value = stream.ReadUInt(ref readerCtx);
                Debug.Log("Got the value = " + value + " back from the server");
                // And finally change the `done[0]` to `1`
                done[0] = 1;
                /*connection[0].Disconnect(driver);
                connection[0] = default(NetworkConnection);*/
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                Debug.Log("Client got disconnected from server");
                connection[0] = default(NetworkConnection);
            }
        }
    }
}