using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Networking.Transport;
using UnityEngine;

[RequiresEntityConversion]
public class NetworkServerComponentProxy : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        NetworkServer server = new NetworkServer();

        dstManager.AddComponentData(entity, server);
    }
}
