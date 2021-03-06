using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

public class PlayerHealthUI : MonoBehaviour
{
    [Header("Health Stats")]
    public float maxHealth;
    public float currentHealth;

    [Header("Heart Assets")]
    public GameObject canvas;
    public GameObject heartStorage;
    [Space]
    public GameObject emptyHeart;
    public GameObject halfHeart;
    public GameObject fullHeart;
    public GameObject lastHalfHeart;

    [Header("Game Over Assets")]
    public GameObject gameOverText;

    private Status status;
    private Player player;

    private void Start()
    {
        canvas = transform.parent.gameObject;
        status = canvas.GetComponent<Status>();
        player = GameObject.Find("Alpaca").GetComponent<Player>();
    }

    public void UpdateHealth()
    {
        currentHealth = player.health;
        maxHealth = player.maxHealth;
        if (currentHealth > maxHealth) // Check if health goes over max health
        {
            currentHealth = maxHealth; // Don't allow health overflow
        }
        else
        {
            foreach (Transform heart in transform) // Remove all heart prefabs from the Heart Storage
            {
                Destroy(heart.gameObject);
            }

            for (int i = 0; i < maxHealth; i++) // Instantiate heart prefabs
            {
                if (currentHealth == i + 1)
                {
                    Instantiate(fullHeart, heartStorage.transform);
                }
                else if (currentHealth > i)
                {
                    if (currentHealth < (i + 1))
                    {
                        if (currentHealth == 0.5f)
                        {
                            Instantiate(lastHalfHeart, heartStorage.transform);
                        }
                        else
                        {
                            Instantiate(halfHeart, heartStorage.transform);
                        }
                    }
                    else
                    {
                        Instantiate(fullHeart, heartStorage.transform);
                    }
                }
                else
                {
                    Instantiate(emptyHeart, heartStorage.transform);
                }
            }

            if (currentHealth <= 0.0f) //Check if player is dead
            {
                currentHealth = 0.0f;
                if (status != null) // For death feature testing
                {
                    status.UpdateText("Dead"); // Update health status text
                }
                // Create Game Over Text in parent object (Script is attached to Heart Storage object, child of Canvas)
                Instantiate(gameOverText, transform.parent.gameObject.transform);
                this.enabled = false; // Disable this script
            }
        }
    }
}
