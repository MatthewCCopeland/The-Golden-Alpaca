﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCollision : MonoBehaviour
{
    void OnCollisionEnter(Collision collisionInfo){
        Debug.Log("Collision Detected with" + collisionInfo.collider.name);
    }

}
