using System.Net;
using Unity.Collections;
using UnityEngine;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using Unity.Jobs;

public class JobifiedClient : MonoBehaviour
{
    public UdpNetworkDriver m_Driver;
    public NativeArray<NetworkConnection> m_Connection;
    public NativeArray<byte> m_Done;
    public JobHandle clientJobHandle;

    public NetworkPipeline m_Unreliable_Pipeline;
    public NetworkPipeline m_Reliable_Pipeline;

    const int k_PacketSize = 256;
    void Start()
    {
        ReliableUtility.Parameters reliabilityParams = new ReliableUtility.Parameters { WindowSize = 32 };
        SimulatorUtility.Parameters simulatorParams = new SimulatorUtility.Parameters { MaxPacketSize = k_PacketSize, MaxPacketCount = 30, PacketDelayMs = 100 };

        m_Driver = new UdpNetworkDriver(simulatorParams, reliabilityParams);
        m_Connection = new NativeArray<NetworkConnection>(1, Allocator.Persistent);
        m_Done = new NativeArray<byte>(1, Allocator.Persistent);

        NetworkEndPoint endpoint = NetworkEndPoint.LoopbackIpv4;
        endpoint.Port = 9000;

        m_Connection[0] = m_Driver.Connect(endpoint);

    }

    public void OnDestroy()
    {
        clientJobHandle.Complete();

        m_Connection.Dispose();
        m_Driver.Dispose();
        m_Done.Dispose();
    }

    void Update()
    {
        clientJobHandle.Complete();
        ClientUpdateJob job = new ClientUpdateJob
        {
            driver = m_Driver,
            connection = m_Connection,
            done = m_Done,
            unreliablePipeline = m_Unreliable_Pipeline,
            reliablePipeline = m_Reliable_Pipeline
        };
        clientJobHandle = m_Driver.ScheduleUpdate();
        //clientJobHandle = job.Schedule(clientJobHandle);
    }

}

struct ClientUpdateJob : IJob
{
    public UdpNetworkDriver driver;
    public NativeArray<NetworkConnection> connection;
    public NativeArray<byte> done;
    public NetworkPipeline unreliablePipeline;
    public NetworkPipeline reliablePipeline;

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

                // after successfully connecting to the server
                // spawn other players as well as this current player
                // spawn this player right away
                int commandType = CommandType.LoadLevel; // <-- change this to spawnnewplayer

                using (DataStreamWriter writer = new DataStreamWriter(4, Allocator.Temp))
                {
                    writer.Write(commandType);
                    // connection status sent to the server should be guaranteed, so reliable pipeline is used here
                    connection[0].Send(driver, reliablePipeline, writer);
                }
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                DataStreamReader.Context readerCtx = default(DataStreamReader.Context);
                uint value = stream.ReadUInt(ref readerCtx);
                
                if(value == CommandType.LoadLevel)
                {
                    // spawn the new client's player game object
                    // send the new client all this player's position information so that they can be spawned
                }
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
        // testing to see if client server connection requires constant connection
        var val = 6;
        using (var writer = new DataStreamWriter(4, Allocator.Temp))
        {
            writer.Write(val);
            connection[0].Send(driver, unreliablePipeline, writer);
        }
    }
}