using System;
using Pigeon.Movement;
using Sparroh.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RedactedHUD
{
    private readonly RedactedBoardScanner _scanner;
    private Color _baseAlertColor = UIColors.Orchid;
    private Color _baseIdleColor = UIColors.TextMuted;
    private Sprite _fallbackTriangle;

    private HudHandle _hud;
    private Image _iconImage;
    private bool _lastDetected;
    private bool _lastShowIdle;

    public RedactedHUD(RedactedBoardScanner scanner)
    {
        _scanner = scanner;
        Instance = this;
    }

    public static RedactedHUD Instance { get; private set; }

    private bool IsHudAlive => HudHandle.IsValid(_hud) && _hud.Primary != null;

    public void OnConfigChanged()
    {
        if (ConfigManager.EnableHud != null && !ConfigManager.EnableHud.Value && _hud != null)
            DestroyHud();
        UpdateHudVisibility();
        RefreshVisual(true);
        ApplyBlinkAlpha(1f);
    }

    public void Update()
    {
        try
        {
            if (ConfigManager.EnableHud == null || !ConfigManager.EnableHud.Value)
            {
                if (IsHudAlive && _hud.IsActive)
                    _hud.SetActive(false);
                return;
            }

            if (_hud != null && !IsHudAlive)
                ClearDestroyedHud();

            if (Player.LocalPlayer == null ||
                Player.LocalPlayer.PlayerLook == null ||
                Player.LocalPlayer.PlayerLook.Reticle == null)
                return;

            var detected = _scanner != null && _scanner.HasRedactedOnBoard;
            var wantVisible = detected ||
                              (ConfigManager.ShowWhenNotDetected != null && ConfigManager.ShowWhenNotDetected.Value);

            if (!wantVisible)
            {
                if (IsHudAlive)
                    _hud.SetActive(false);
                return;
            }

            if (!IsHudAlive)
            {
                CreateHud();
                if (!IsHudAlive)
                    return;
            }

            _hud.SetActive(true);
            RefreshVisual(false);
            UpdateBlink(detected);
        }
        catch (Exception ex)
        {
            RedactedNotificationPlugin.Logger.LogError($"Error in RedactedHUD.Update(): {ex.Message}");
        }
    }

    private void UpdateBlink(bool detected)
    {
        if (!IsHudAlive)
            return;

        if (!detected || ConfigManager.EnableBlink == null || !ConfigManager.EnableBlink.Value)
        {
            ApplyBlinkAlpha(1f);
            return;
        }

        var speed = ConfigManager.BlinkSpeed != null ? Mathf.Max(0.1f, ConfigManager.BlinkSpeed.Value) : 2f;
        var minAlpha = ConfigManager.BlinkMinAlpha != null ? Mathf.Clamp01(ConfigManager.BlinkMinAlpha.Value) : 0.35f;

        var t = Mathf.PingPong(Time.unscaledTime * speed, 1f);
        var alpha = Mathf.Lerp(minAlpha, 1f, t);
        ApplyBlinkAlpha(alpha);
    }

    private void ApplyBlinkAlpha(float alpha)
    {
        if (!IsHudAlive)
            return;

        alpha = Mathf.Clamp01(alpha);

        if (_hud.Primary != null && _hud.Primary.Tmp != null)
        {
            var c = _lastDetected ? _baseAlertColor : _baseIdleColor;
            c.a = alpha;
            _hud.Primary.Tmp.color = c;
        }

        if (_iconImage != null)
        {
            var ic = _iconImage.color;
            var baseA = _lastDetected ? 1f : 0.85f;
            ic.a = baseA * alpha;
            _iconImage.color = ic;
        }
    }

    public void UpdateHudVisibility()
    {
        if (!IsHudAlive)
        {
            ClearDestroyedHud();
            return;
        }

        if (ConfigManager.EnableHud == null || !ConfigManager.EnableHud.Value)
        {
            _hud.SetActive(false);
            return;
        }

        var detected = _scanner != null && _scanner.HasRedactedOnBoard;
        var wantVisible = detected ||
                          (ConfigManager.ShowWhenNotDetected != null && ConfigManager.ShowWhenNotDetected.Value);
        _hud.SetActive(wantVisible);
    }

    private void ClearDestroyedHud()
    {
        _hud = null;
        _iconImage = null;
    }

    private void CreateHud()
    {
        if (IsHudAlive)
            return;

        ClearDestroyedHud();

        var anchors = ConfigManager.Anchors;
        var ax = anchors != null ? anchors.XValue : 0.50f;
        var ay = anchors != null ? anchors.YValue : 0.12f;

        _hud = HudBuilder.Create("RedactedNotificationHUD")
            .ParentToReticle()
            .Anchor(ax, ay)
            .Pivot(new Vector2(0.5f, 0.5f))
            .Size(420f, 32f)
            .AddText("StatusText", 18f, TextAlignmentOptions.Center)
            .Build();

        if (!IsHudAlive)
            return;

        try
        {
            var iconRt = UIFactory.CreateRect("RedactedIcon", _hud.Rect);
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.sizeDelta = UITheme.ScaledSize(28f, 28f);
            iconRt.anchoredPosition = new Vector2(UITheme.S(4f), 0f);

            _iconImage = iconRt.gameObject.AddComponent<Image>();
            _iconImage.raycastTarget = false;
            _iconImage.preserveAspect = true;

            if (_hud.Primary != null && _hud.Primary.Rect != null)
            {
                var textRt = _hud.Primary.Rect;
                textRt.offsetMin = new Vector2(UITheme.S(34f), textRt.offsetMin.y);
            }
        }
        catch (Exception ex)
        {
            RedactedNotificationPlugin.Logger.LogWarning($"Could not create redacted icon: {ex.Message}");
            _iconImage = null;
        }

        if (anchors != null)
            _hud.EnableReposition(
                RedactedNotificationPlugin.PluginGUID,
                "ERROR REDACTED Alert",
                anchors);

        _lastDetected = false;
        _lastShowIdle = false;
        RefreshVisual(true);
    }

    private void DestroyHud()
    {
        if (_hud != null)
        {
            if (_hud.IsAlive)
                _hud.Destroy();
            _hud = null;
        }

        _iconImage = null;
    }

    private void RefreshVisual(bool force)
    {
        if (!IsHudAlive)
            return;

        var detected = _scanner != null && _scanner.HasRedactedOnBoard;
        var showIdle = ConfigManager.ShowWhenNotDetected != null && ConfigManager.ShowWhenNotDetected.Value;

        if (!force && detected == _lastDetected && showIdle == _lastShowIdle)
            return;

        _lastDetected = detected;
        _lastShowIdle = showIdle;

        if (detected)
        {
            var text = ConfigManager.AlertText != null
                ? ConfigManager.AlertText.Value
                : "ERROR REDACTED has appeared!";
            _baseAlertColor = GetAlertColor();

            _hud.Primary.Text = text;
            _hud.Primary.Tmp.color = _baseAlertColor;
            ApplyIcon(true);
            ApplyBlinkAlpha(1f);
        }
        else if (showIdle)
        {
            var text = ConfigManager.IdleText != null
                ? ConfigManager.IdleText.Value
                : "no ERROR detected.";
            _baseIdleColor = UIColors.TextMuted;
            _hud.Primary.Text = text;
            _hud.Primary.Tmp.color = _baseIdleColor;
            ApplyIcon(false);
            ApplyBlinkAlpha(1f);
        }
    }

    private Color GetAlertColor()
    {
        try
        {
            if (_scanner != null && _scanner.RedactedModifier != null)
                return _scanner.RedactedModifier.IconColor;
        }
        catch
        {
        }

        return UIColors.Orchid;
    }

    private void ApplyIcon(bool alert)
    {
        if (_iconImage == null)
            return;

        Sprite sprite = null;
        Color color;

        if (alert)
        {
            color = GetAlertColor();
            try
            {
                if (_scanner != null && _scanner.RedactedModifier != null)
                    sprite = _scanner.RedactedModifier.Icon;
            }
            catch
            {
            }
        }
        else
        {
            color = new Color(0.45f, 0.45f, 0.48f, 0.85f);
            try
            {
                if (_scanner != null && _scanner.RedactedModifier != null)
                    sprite = _scanner.RedactedModifier.Icon;
            }
            catch
            {
            }
        }

        if (sprite == null)
            sprite = GetFallbackTriangle();

        _iconImage.sprite = sprite;
        _iconImage.color = color;
        _iconImage.enabled = sprite != null;
    }

    private Sprite GetFallbackTriangle()
    {
        if (_fallbackTriangle != null)
            return _fallbackTriangle;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var clear = new Color(0f, 0f, 0f, 0f);
        var fill = Color.white;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var nx = (x + 0.5f) / size;
            var ny = (y + 0.5f) / size;
            var halfWidth = ny * 0.5f;
            var inside = ny >= 0.12f && ny <= 0.92f && Mathf.Abs(nx - 0.5f) <= halfWidth * 0.9f;
            tex.SetPixel(x, y, inside ? fill : clear);
        }

        tex.Apply(false, true);
        _fallbackTriangle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _fallbackTriangle;
    }

    public void OnDestroy()
    {
        try
        {
            DestroyHud();
        }
        catch (Exception ex)
        {
            RedactedNotificationPlugin.Logger.LogError($"Error in RedactedHUD.OnDestroy(): {ex.Message}");
        }
    }
}