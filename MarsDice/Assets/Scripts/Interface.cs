using UnityEngine;

public class Interface : MonoBehaviour
{
    [Header("Положение стека полосок относительно этого объекта")]
    [SerializeField] private Vector3 stackOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Расстояние между полосками по вертикали (центр к центру)")]
    [SerializeField] private float verticalBarStep = 0.42f;

    private Unit unit;
    private UnitBarStack stack;

    private void Awake()
    {
        unit = GetComponent<Unit>();
        if (unit == null)
        {
            Debug.LogWarning($"{name}: на этом объекте нужен компонент Unit.");
        }
    }

    private void Start()
    {
        if (unit == null)
        {
            return;
        }

        stack = new UnitBarStack(transform, unit, stackOffset, verticalBarStep);
    }

    private void LateUpdate()
    {
        stack?.Tick();
    }

    private void OnDestroy()
    {
        stack?.DestroyRoot();
        stack = null;
    }

    private sealed class UnitBarStack
    {
        private const float BarWidth = 1.2f;
        private const float BgThickness = 0.15f;
        private const float FillThickness = 0.1f;

        private readonly Transform follow;
        private readonly Unit unit;
        private readonly Vector3 offset;
        private readonly Transform root;
        private readonly StatBarVisual hpBar;
        private readonly StatBarVisual energyBar;
        private readonly StatBarVisual shieldBar;

        public UnitBarStack(Transform follow, Unit unit, Vector3 offset, float step)
        {
            this.follow = follow;
            this.unit = unit;
            this.offset = offset;

            GameObject rootGo = new GameObject($"{follow.name}_InterfaceBars");
            root = rootGo.transform;

            float y = 0f;
            // Фон — тёмный «потраченный» трек; заливка — яркая, без затемнения от света (Unlit в Create).
            hpBar = StatBarVisual.Create(root, "HP", new Vector3(0f, y, 0f), BarWidth, BgThickness, FillThickness,
                new Color(0.1f, 0.04f, 0.04f), new Color(0.2f, 1f, 0.35f));
            y -= step;
            energyBar = StatBarVisual.Create(root, "Energy", new Vector3(0f, y, 0f), BarWidth, BgThickness, FillThickness,
                new Color(0.12f, 0.1f, 0.03f), new Color(1f, 0.95f, 0.15f));
            y -= step;
            shieldBar = StatBarVisual.Create(root, "Shield", new Vector3(0f, y, 0f), BarWidth, BgThickness, FillThickness,
                new Color(0.04f, 0.1f, 0.12f), new Color(0.35f, 0.92f, 1f));
        }

        public void Tick()
        {
            if (follow == null || unit == null)
            {
                root.gameObject.SetActive(false);
                return;
            }

            root.gameObject.SetActive(true);
            root.position = follow.position + offset;

            // Плоскость полосок и TextMesh совпадают с экраном камеры — не задом и без зеркального текста.
            Camera cam = Camera.main;
            if (cam != null)
            {
                root.rotation = cam.transform.rotation;
            }

            hpBar.Refresh(unit.CurrentHealth, unit.MaxHealth);
            unit.GetDisplayedEnergy(out int energyCurrent, out int energyMax);
            energyBar.Refresh(energyCurrent, energyMax);
            shieldBar.Refresh(unit.CurrentShield, unit.MaxShield);
        }

        public void DestroyRoot()
        {
            if (root != null)
            {
                Object.Destroy(root.gameObject);
            }
        }

        private sealed class StatBarVisual
        {
            // При root.rotation = camera.rotation локальный +Z — вглубь сцены, −Z — к камере.
            // Подложка дальше (+Z), яркая заливка и текст ближе (−Z), иначе чёрный трек перекрывает цвет.
            private const float BackgroundLocalZ = 0.05f;
            private const float FillLocalZ = -0.05f;
            private const float TextBackdropLocalZ = -0.071f;
            private const float TextLocalZ = -0.08f;

            private readonly Transform fill;
            private readonly TextMesh text;
            private readonly float barWidth;

            private StatBarVisual(Transform fill, TextMesh text, float barWidth)
            {
                this.fill = fill;
                this.text = text;
                this.barWidth = barWidth;
            }

            public static StatBarVisual Create(Transform parent, string label, Vector3 localPosition, float barWidth,
                float bgThickness, float fillThickness, Color backgroundColor, Color fillColor)
            {
                GameObject row = new GameObject(label);
                row.transform.SetParent(parent, false);
                row.transform.localPosition = localPosition;

                GameObject background = GameObject.CreatePrimitive(PrimitiveType.Cube);
                background.name = "Background";
                background.transform.SetParent(row.transform, false);
                background.transform.localPosition = new Vector3(0f, 0f, BackgroundLocalZ);
                background.transform.localScale = new Vector3(barWidth, bgThickness, 0.05f);
                SetupUnlitBarRenderer(background.GetComponent<Renderer>(), backgroundColor);
                Object.Destroy(background.GetComponent<Collider>());

                GameObject fillGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fillGo.name = "Fill";
                fillGo.transform.SetParent(row.transform, false);
                fillGo.transform.localPosition = new Vector3(0f, 0f, FillLocalZ);
                fillGo.transform.localScale = new Vector3(barWidth, fillThickness, 0.04f);
                SetupUnlitBarRenderer(fillGo.GetComponent<Renderer>(), fillColor);
                Object.Destroy(fillGo.GetComponent<Collider>());

                float textCenterY = bgThickness * 0.5f + 0.1f;
                GameObject textBackdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                textBackdrop.name = "TextBackdrop";
                textBackdrop.transform.SetParent(row.transform, false);
                textBackdrop.transform.localPosition = new Vector3(0f, textCenterY, TextBackdropLocalZ);
                textBackdrop.transform.localScale = new Vector3(barWidth * 1.04f, 0.28f, 0.02f);
                SetupUnlitBarRenderer(textBackdrop.GetComponent<Renderer>(), Color.black);
                Object.Destroy(textBackdrop.GetComponent<Collider>());

                TextMesh textMesh = new GameObject("Text").AddComponent<TextMesh>();
                textMesh.transform.SetParent(row.transform, false);
                textMesh.transform.localPosition = new Vector3(0f, textCenterY, TextLocalZ);
                textMesh.characterSize = 0.08f;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.color = Color.white;
                textMesh.fontSize = 48;
                textMesh.text = label;

                return new StatBarVisual(fillGo.transform, textMesh, barWidth);
            }

            private static void SetupUnlitBarRenderer(Renderer renderer, Color color)
            {
                Shader shader = Shader.Find("Unlit/Color")
                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Sprites/Default");

                Material mat;
                if (shader != null)
                {
                    mat = new Material(shader);
                    if (mat.HasProperty("_BaseColor"))
                    {
                        mat.SetColor("_BaseColor", color);
                    }

                    if (mat.HasProperty("_Color"))
                    {
                        mat.SetColor("_Color", color);
                    }
                    else
                    {
                        mat.color = color;
                    }
                }
                else
                {
                    mat = renderer.material;
                    mat.color = color;
                }

                renderer.material = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            public void Refresh(int current, int max)
            {
                if (max <= 0)
                {
                    Vector3 scale = fill.localScale;
                    scale.x = 0f;
                    fill.localScale = scale;
                    fill.localPosition = new Vector3(-barWidth * 0.5f, 0f, FillLocalZ);
                    text.text = $"{current}/0";
                    return;
                }

                float ratio = Mathf.Clamp01((float)current / max);
                Vector3 fillScale = fill.localScale;
                fillScale.x = barWidth * ratio;
                fill.localScale = fillScale;
                fill.localPosition = new Vector3(-barWidth * 0.5f + fillScale.x * 0.5f, 0f, FillLocalZ);
                text.text = $"{current}/{max}";
            }
        }
    }
}
