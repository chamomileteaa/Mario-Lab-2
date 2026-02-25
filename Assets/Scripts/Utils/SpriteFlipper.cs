using UnityEngine;

namespace Utils
{
    [DisallowMultipleComponent]
    public class SpriteFlipper : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private SpriteRenderer[] targets;

        [Header("Flip")]
        [SerializeField] private bool useLocalScale = true;
        [SerializeField] private bool flipX = true;
        [SerializeField] private bool flipY = true;
        [SerializeField, Min(0f)] private float deadzone = 0.01f;

        private float facingX = 1f;
        private float facingY = 1f;
        private Vector3[] baseScales;

        private void Awake() => Initialize();

        public void SetDirection(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) > deadzone) facingX = Mathf.Sign(direction.x);
            if (Mathf.Abs(direction.y) > deadzone) facingY = Mathf.Sign(direction.y);
            Apply();
        }

        private void OnValidate()
        {
            deadzone = Mathf.Max(0f, deadzone);
            Initialize();
        }

        private void Initialize()
        {
            if (targets == null || targets.Length == 0)
                targets = GetComponentsInChildren<SpriteRenderer>(true);

            if (baseScales != null && baseScales.Length == targets.Length) return;

            baseScales = new Vector3[targets.Length];
            for (var i = 0; i < targets.Length; i++)
                baseScales[i] = targets[i] ? targets[i].transform.localScale : Vector3.one;
        }

        private void Apply()
        {
            if (targets == null || targets.Length == 0) return;

            if (!useLocalScale)
            {
                foreach (var target in targets)
                {
                    if (!target) continue;
                    if (flipX) target.flipX = facingX < 0f;
                    if (flipY) target.flipY = facingY < 0f;
                }

                return;
            }

            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (!target) continue;

                var scale = baseScales[i];
                if (flipX) scale.x = Mathf.Abs(scale.x) * facingX;
                if (flipY) scale.y = Mathf.Abs(scale.y) * facingY;
                target.transform.localScale = scale;
            }
        }
    }
}
