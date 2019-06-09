using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[RequiresEntityConversion]
public class UnitSpawnerComponentProxy : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    public GameObject prefab;
    public GameObject spawnPoint; // TODO: make sure that the gameobject passed in is a list of spawn points so that one can be chosen randomly
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        Vector3 spawnLocation = spawnPoint.transform.position;

        UnitSpawner unitSpawner = new UnitSpawner
        {
            prefab = conversionSystem.GetPrimaryEntity(prefab),
            spawnPoint = new float3(spawnLocation.x, spawnLocation.y, spawnLocation.z)
        };

        dstManager.AddComponentData(entity, unitSpawner);
    }

    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
    {
        referencedPrefabs.Add(prefab);
    }
}
