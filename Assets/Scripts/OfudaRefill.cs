using System.Collections;
using UnityEngine;

public class OfudaRefill : MonoBehaviour
{
    [Header("Настройки печати")]
    public float respawnTime = 2.5f;

    [Header("Плавное парение")]
    public float floatAmplitude = 0.15f;
    public float floatSpeed = 3f;

    [Header("Цвета")]
    public Color activeColor = new Color(1f, 0.2f, 0.278f, 1f);
    public Color cooldownColor = new Color(0.431f, 0.231f, 0.275f, 0.35f);

    private SpriteRenderer sr;
    private Vector3 startPos;
    private bool isReady = true;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        startPos = transform.position;
        sr.color = activeColor;
    }

    void Update()
    {
        float offsetY = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        transform.position = startPos + new Vector3(0f, offsetY, 0f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryRefill(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryRefill(other);
    }

    private void TryRefill(Collider2D other)
    {
        if (!isReady) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null && player.NeedsRefill)
        {
            player.RefillDash();
            StartCoroutine(CooldownRoutine());
        }
    }

    private IEnumerator CooldownRoutine()
    {
        isReady = false;
        sr.color = cooldownColor;

        yield return new WaitForSeconds(respawnTime);

        sr.color = activeColor;
        isReady = true;
    }
}