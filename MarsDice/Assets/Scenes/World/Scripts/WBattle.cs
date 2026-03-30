using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WBattle : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private Transform target;
    [SerializeField] private float triggerDistance = 2.5f;

    [Header("Scene")]
    [SerializeField] private string battleSceneName = "Battle";

    private bool _isLoading;

    // Update is called once per frame
    void Update()
    {
        if (_isLoading || target == null) return;

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist <= triggerDistance)
        {
            _isLoading = true;
            SceneManager.LoadScene(battleSceneName);
        }
    }
}
