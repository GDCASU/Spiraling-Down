using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/* -----------------------------------------------------------
 * Author:
 * Eliza Chook
 *
 * Modified By:
 * Jacob Kaufman-Warner, Ian Fletcher
 */// --------------------------------------------------------

/* -----------------------------------------------------------
 * Pupose:
 * Handle Item attraction and pickup
 */// --------------------------------------------------------

/// <summary>
/// Attracts objects that has the tag collectible to collect them (applies to all collectable objects/enemy drops)
/// </summary>
public class MagnetAttraction : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField][Range(0f,10f)] private float attractionSpeed; // Speed at which objects are pulled towards the player
    [SerializeField][Range(0.1f,10f)] private float pickupRadius = 0.1f;

    // Local variables
    private Dictionary<Collider, ItemPickups> cachedScripts = new(); // Script Cache

    private void Start()
    {
        StartCoroutine(NullColliderCheckRoutine());
    }

    private void OnTriggerEnter(Collider other)
    {
        //Debug.Log(other.name + " FOUND!");
        // Check if the collider is already cached
        if (cachedScripts.ContainsKey(other)) return; // It does, dont need to do anything
        
        // Check if the item is of type collectible
        if (!other.CompareTag(TagDefinitions.CollectibleTag)) return; // Not a collectible
        
        // Else, add it to the dictionary
        ItemPickups scr = other.GetComponent<ItemPickups>();
        if (scr == null) return; // dont add if null
        if (!scr.isMagnetAttractable) return; // Dont add if not magnet attractable
        cachedScripts.Add(other, scr);
        Debug.Log(other.name + " FOUND!");
    }
    
    // Runs per collider found within the sphere
    private void OnTriggerStay(Collider other)
    {
        // Check if its on cache
        if (!cachedScripts.TryGetValue(other, out ItemPickups itemPickup)) return; // Not on cache

        // Check which type of collectible it is
        switch (itemPickup.itemData.itemType)
        {
            case CollectibleType.Ammo:
                HandleAmmoCollectible(itemPickup);
                break;
            default:
                // TODO: HANDLE OTHER TYPES
                Debug.LogError("ERROR! ITEM TYPE CASE NOT DEFINED! We dont know how to handle = " + itemPickup.itemData.itemType);
                return;
        }
    }
    
    // Remove the collider from the cache when it exits the trigger
    private void OnTriggerExit(Collider other)
    {
        // Remove from cache
        cachedScripts.Remove(other);
    }

    /// <summary>
    /// Handle the collection of ammo drops
    /// </summary>
    private void HandleAmmoCollectible(ItemPickups itemPickup)
    {
        // Only attract if not at max ammo
        ElementInvSlot elementInvSlot = ElementInventoryManager.Instance.GetInvSlotFromElement(itemPickup.itemData.element);
        if (elementInvSlot == null) return;
        if (elementInvSlot.ammoAmount >= elementInvSlot.ammoMaxAmount)
        {
            // At max ammo, re-enable gravity
            itemPickup.rb.useGravity = true;
            return;
        }
        // Check if not within pickup radius
        float distance = Vector3.Distance(itemPickup.transform.position, transform.position);
        if (distance <= pickupRadius)
        {
            // Pick up ammo
            ElementInventoryManager.Instance.AddAmmoToElement(itemPickup.itemData.element, itemPickup.itemData.value);
            Destroy(itemPickup.gameObject);
            return;
        }
        // Else, Attract
        AttractObject(itemPickup);
    }

    /// <summary>
    /// Function to call if the object is to be attracted to player
    /// </summary>
    private void AttractObject(ItemPickups itemPickup)
    {
        // Disable gravity
        itemPickup.rb.useGravity = false;
        // Attract
        itemPickup.transform.position = Vector3.MoveTowards(itemPickup.transform.position, transform.position, attractionSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Coroutine that will check periodically if the cache dictionary contains null keys
    /// </summary>
    /// <returns></returns>
    private IEnumerator NullColliderCheckRoutine()
    {
        float checkPeriod = 5f;
        float elapsedTime = 0f;
        List<Collider> keyList;
        
        while (true)
        {
            elapsedTime += Time.deltaTime;
            if (elapsedTime <= checkPeriod)
            {
                // Haven't reached the check period yet
                yield return null;
                continue;
            }
            
            // We have started our periodic check, check for null keys
            keyList = new List<Collider>(cachedScripts.Keys);
            foreach (Collider key in keyList)
            {
                if (!key) // The object has been destroyed
                {
                    cachedScripts.Remove(key);
                }
            }
            
            // Restart timer
            elapsedTime = 0f;
            yield return null;
        }
    }

    // Function to draw a sphere representing the item pickup zone
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
    
}
