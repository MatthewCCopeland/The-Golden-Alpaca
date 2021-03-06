using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EnemyController : MonoBehaviour
{
    // MVC
    private EnemyModel model;
    private EnemyView view;

    //Weapons
    private MeleeWeapon melee;
    private RangedWeapon ranged;

    private BossHealthUI bossHealthScript;

    private void Start()
    {
        //MVC linking
        model = GetComponent<EnemyModel>();
        view = GetComponent<EnemyView>();

        if (model.isBoss)
        {
            bossHealthScript = GameObject.Find("HealthBarStorage").GetComponent<BossHealthUI>();
        }

        //Setup melee
        if (model.meleeEnabled == true)
        {
            melee = gameObject.GetComponent<MeleeWeapon>();
            melee.AttackDamage = model.meleeAttackDamage;
            melee.attackRange = model.meleeAttackRange;
        }

        //Setup ranged
        if (model.rangedEnabled == true)
        {
            ranged = gameObject.GetComponent<RangedWeapon>();
            ranged.damage = model.rangedAttackDamage;
            ranged.speed = model.rangedAttackProjectileSpeed;
        }

        //Increase the enemy counter
        GameObject.Find("CounterCanvas").GetComponentInChildren<EnemyCounter>().increaseCount();
    }

    private void Update()
    {
        if (model != null && view != null)
        {
            //Check Health
            if (model.health <= 0)
            {
                if (model.isBoss)
                {
                    bossHealthScript.UpdateHealthBar();
                }
                Die();
            }

            //Move enemy
            if (model.movementEnabled)
            {
                CalculateMovement();
            }

            //If the enemy can attack and has line of sight and has a weapon, then attack
            if (Time.time >= model.NextAttackTime && PlayerInLineOfSight() && (model.meleeEnabled || model.rangedEnabled))
            {
                CalculateAttack();
            }
        }
    }

    /**
    * This method moves the player either to chase or idle 
    */
    private void CalculateMovement()
    {
        if (PlayerInLineOfSight())
        {
            model.ChasePlayer();
        }
        else
        {
            model.Timer += Time.deltaTime;
            if (model.Timer >= model.WanderTimer)
            {
                model.IdleMove();
            }
        }
    }

    /**
    * This method calculates if the enemy has line of sight to the player
    */
    private bool PlayerInLineOfSight()
    {
        //Raycast between the player and the enemy
        if (Physics.Raycast(model.AttackPoint.position, model.AttackPoint.forward, out RaycastHit hit, model.sightRange))
        {
            //If the raycast hits the player then it has line of sight
            if (hit.transform.CompareTag("Player"))
            {
                if (model.isBoss && !bossHealthScript.isHealthBarActive)
                {
                    bossHealthScript.ShowHealthBar();
                }
                return true;
            }
        }
        return false;
    }

    /**
    * This method calculates an enemy attack based on their assigned weapon structure
    */
    private void CalculateAttack()
    {
        //If the enemy has a melee and ranged attack
        if (model.meleeEnabled && model.rangedEnabled)
        {
            //Check if the player is within range for melee attack
            if (Physics.CheckSphere(melee.attackPoint.position, model.meleeAttackRange, model.targetLayer))
            {
                PerformMeleeAttack();
            } //Check if the player is within range for ranged attack
            else if (Physics.CheckSphere(ranged.attackPoint.position, model.rangedAttackRange, model.targetLayer))
            {
                if (model.isBoss && SceneManager.GetActiveScene().name.Equals("LevelThree"))
                {
                    Instantiate(model.fireBurst, transform); // Create a particle effect if enemy is a boss
                }
                PerformRangedAttack();
            }
        } //If the enemy has a melee but no ranged attack
        else if (model.meleeEnabled && !model.rangedEnabled)
        {
            //Check if the player is within range
            if (Physics.CheckSphere(melee.attackPoint.position, model.meleeAttackRange, model.targetLayer))
            {
                PerformMeleeAttack();
            }
        } //If the enemy has a ranged but no melee attack
        else if (model.rangedEnabled && !model.meleeEnabled)
        {
            //Check if the player is within range
            if (Physics.CheckSphere(ranged.attackPoint.position, model.rangedAttackRange, model.targetLayer))
            {
                PerformRangedAttack();
            }
        }

        // Level 3 boss spawns bees
        if (model.isBoss && SceneManager.GetActiveScene().name.Equals("LevelThree"))
        {
            float numOfBees = Random.Range(1, 9);
            if (numOfBees % 3 == 0)
            {
                for (int i = 0; i < numOfBees; i++)
                {
                    Instantiate(model.fireSwirl, transform);
                    Instantiate(model.enemyBee, transform.position, new Quaternion(0, 0, 0, 0));
                }
            }
        }
    }
    private void PerformMeleeAttack()
    {
        //Reset the attack time
        model.NextAttackTime = Time.time + 1f / model.meleeAttackRate;

        StartCoroutine(MeleeAttackDelay(model.meleeAttackDelay));
    }
    private void PerformRangedAttack()
    {
        //Reset the attack time
        model.NextAttackTime = Time.time + 1f / model.rangedAttackRate;

        StartCoroutine(RangedAttackDelay(model.rangedAttackDelay));
    }

    IEnumerator MeleeAttackDelay(float delayTime)
    {
        yield return new WaitForSeconds(delayTime);
        melee.Attack();
        view.animator.SetTrigger("Attack");
    }
    IEnumerator RangedAttackDelay(float delayTime)
    {
        yield return new WaitForSeconds(delayTime);
        ranged.Attack(model.rangedAttackPattern.ToString());
        view.animator.SetTrigger("Attack");
    }

    private void Die()
    {
        if (model.deathCloudObject != null)
        {
            //Create death cloud particle effect
            Instantiate(model.deathCloudObject, transform.position, new Quaternion(0, 0, 0, 0));
        }

        //Random chance of dropping health
        if (Random.Range(0f, 1f) <= model.dropChance)
        {
            Instantiate(model.heartPickup, transform.position, new Quaternion(0, 0, 0, 0));
        }

        //Decrease enemy counter
        GameObject.Find("CounterCanvas").GetComponentInChildren<EnemyCounter>().decreaseCount();

        //Deactivate MVC
        model.enabled = false;
        view.enabled = false;
        this.enabled = false;
        Destroy(gameObject);
    }

    public float GetHealth()
    {
        return model.health;
    }

    public float GetMaxHealth()
    {
        return model.maxHealth;
    }

    public void decreaseHealth(float damage)
    {
        model.health -= damage;
        view.animator.SetTrigger("Hit");
        if (model.isBoss)
        {
            bossHealthScript.UpdateHealthBar();
        }
    }
}