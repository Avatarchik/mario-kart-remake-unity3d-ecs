using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class NetworkClientComponentProxy : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        NetworkClient client = new NetworkClient();

        dstManager.AddComponentData(entity, client);
    }
}
