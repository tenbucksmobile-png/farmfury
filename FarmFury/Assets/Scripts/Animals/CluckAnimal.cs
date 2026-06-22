using System.Collections;
using UnityEngine;

public class CluckAnimal : AnimalBase
{
    [Header("Cluster Bomb")]
    [SerializeField] private GameObject _eggPrefab;
    [SerializeField] private int        _eggCount    = 5;
    [SerializeField] private float      _minEggSpeed = 5f;
    [SerializeField] private float      _spreadDeg   = 120f;

    [Header("Flash")]
    [SerializeField] private float _flashDuration = 0.12f;

    protected override void Awake()
    {
        mass       = 8f;
        bounciness = 0.4f;
        linearDrag = 0.008f;
        base.Awake();
    }

    protected override void TriggerAbility()
    {
        StartCoroutine(FlashWhite());
        SpawnEggs();
    }

    void SpawnEggs()
    {
        Vector2 vel       = _rb.linearVelocity;
        float   baseAngle = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
        float   speed     = Mathf.Max(vel.magnitude * 0.6f, _minEggSpeed);

        for (int i = 0; i < _eggCount; i++)
        {
            float t      = _eggCount > 1 ? i / (float)(_eggCount - 1) : 0.5f;
            float offset = Mathf.Lerp(-_spreadDeg * 0.5f, _spreadDeg * 0.5f, t);
            float rad    = (baseAngle + offset) * Mathf.Deg2Rad;
            var   dir    = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            var egg = Instantiate(_eggPrefab, transform.position, Quaternion.identity);

            if (egg.TryGetComponent<Rigidbody2D>(out var rb))
                rb.linearVelocity = dir * speed;
        }
    }

    IEnumerator FlashWhite()
    {
        if (_sr) _sr.color = Color.white;
        yield return new WaitForSeconds(_flashDuration);
        if (_sr) _sr.color = Color.yellow;
    }
}