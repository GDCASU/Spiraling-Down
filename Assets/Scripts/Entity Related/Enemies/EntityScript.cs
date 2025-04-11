using Pathfinding;
using System;
using UnityEngine;

/* -----------------------------------------------------------
 * Author:
 * Ian Fletcher
 * 
 * Using Merged work from
 * Davyd Yehudin, William Peng
 * 
 * Modified By:
 * William Peng, Chandler Van
/* -----------------------------------------------------------

/* -----------------------------------------------------------
 * Purpose:
 * Handle everything that can happen to the enemy on a single script
 */// --------------------------------------------------------


/// <summary>
/// Handles everything about the enemy, health, status effects, death, etc.
/// </summary>
[RequireComponent(typeof(ElementStatusHandler))]
public class EntityScript : MonoBehaviour, IDamageable
{
    [Header("References")]
    [SerializeField] private ElementStatusHandler elementStatusHandler;
    [SerializeField] private AIPath aiPath;
    [SerializeField] private GameObject deathDeleteTarget;
    public FloatingHealthBar healthBar;
    public GameObject destroyOnDeath;
    
    [Header("Entity Stats")]
    [SerializeField] private float baseSpeed;
    [SerializeField] private int maxHealth = 10;

    [Header("Multipliers")] 
    [SerializeField] private DamageMultiplier damageMults;
    
    [Header("Readouts")]
    [InspectorReadOnly] [SerializeField] private int currentHealth;
    [InspectorReadOnly] public float speedMult = 1f; // Used by statuses to slow down the entity
    [InspectorReadOnly] public bool stunned = false;
    
    // Use this bool to gate all your Debug.Log Statements please
    [Header("Debugging")]
    [SerializeField] private bool doDebugLog;

    // Local Variables
    

    private void Start()
    {
        // Set stats and references
        currentHealth = maxHealth;
        elementStatusHandler.entityScript = this;
    }

    private void LateUpdate()
    {
        if (aiPath == null) return;

        aiPath.maxSpeed = baseSpeed * speedMult;
        aiPath.canMove = !stunned;
    }

    /// <summary>
    /// Function derived from interface to deal damage to this entity
    /// </summary>
    /// <param name="damage">The damage to deal</param>
    /// <param name="element">The element the damage represents</param>
    /// <returns></returns>
    public ReactionType TakeDamage(int damage, Elements element)
    {
        // Compute damage through multiplier
        int newDamage = damageMults.ComputeDamage(damage, element);
        // Ignore if zero/immune
        if (newDamage <= 0) return ReactionType.Undefined;
        // Damage health
        int previousHealth = currentHealth;
        currentHealth -= newDamage;
        if (currentHealth <= 0)
        {
            // Render damage
            HitpointsRenderer.Instance.PrintDamage(transform.position, currentHealth, Color.red);
        }
        else
        {
            HitpointsRenderer.Instance.PrintDamage(transform.position, newDamage, Color.red);
        }
        // Update health bar
        healthBar.UpdateHealthBar(currentHealth, maxHealth);
        if (currentHealth <= 0)
        {
            // Enemy died
            currentHealth = 0;
            OnDeath();
            // FIXME: Return undefined?
            return ReactionType.Undefined;
        }
        // Send the element to the status handler and return a reaction if caused
        return elementStatusHandler.HandleElementStatus(element);
    }
    
    /// <summary>
    /// Function called by the ElementStatusHandler to deal status damage to entity
    /// </summary>
    public void StatusDamage(int damage)
    {
        // Damage health
        int previousHealth = currentHealth;
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            // Render damage
            HitpointsRenderer.Instance.PrintDamage(transform.position, currentHealth, Color.red);
            OnDeath(); // Enemy died
            return;
        }
        // Else update health bar
        healthBar.UpdateHealthBar(currentHealth, maxHealth);
        HitpointsRenderer.Instance.PrintDamage(transform.position, damage, Color.red);
    }

    /// <summary>
    /// Called when the entity's health reaches zero
    /// </summary>
    private void OnDeath()
    {
        // TODO: UNFINISHED
        Destroy(deathDeleteTarget);
    }
}

/// <summary>
/// Helper class to define damage multipliers on the inspector
/// </summary>
[Serializable]
public class DamageMultiplier
{
    // Settings
    [Range(0,10)] public float neutralMultiplier = 1f;
    [Range(0,10)] public float fungalMultiplier = 1f;
    [Range(0,10)] public float sparkMultiplier = 1f;
    [Range(0,10)] public float fireMultiplier = 1f;

    /// <summary>
    /// Function that returns a new damage value based on the multipliers
    /// </summary>
    public int ComputeDamage(int damage, Elements element)
    {
        float newDamage = damage;

        switch(element)
        {
            case Elements.Fire:
                newDamage *= fireMultiplier;
                break;
            case Elements.Fungal:
                newDamage *= fungalMultiplier;
                break;
            case Elements.Neutral:
                newDamage *= neutralMultiplier;
                break;
            case Elements.Sparks:
                newDamage *= sparkMultiplier;
                break;
            default:
                Debug.LogError("ERROR! ELEMENT NOT DEFINED ON SWITCH AT COMPUTE DAMAGE!");
                return damage;
        }
        // Round the new damage float and return it
        return Mathf.RoundToInt(newDamage);
    }
}










