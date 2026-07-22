using System;
using BepInEx.Configuration;
using Pigeon.Movement;
using Sparroh.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RedactedHUD
{
    private readonly ConfigFile _configFile;
    private readonly RedactedBoardScanner _scanner;

    private ConfigEntry<bool> _enableHud;
    private ConfigEntry<bool> _showWhenNotDetected;
    private ConfigEntry<bool> _enableBlink;
    private ConfigEntry<float> _blinkSpeed;
    private ConfigEntry<float> _blinkMinAlpha;
    private ConfigEntry<float> _anchorX;
    private ConfigEntry<float> _anchorY;
    private ConfigEntry<string> _alertText;
    private ConfigEntry<string> _idleText;

    private HudHandle _hud;
    private Image _iconImage;
    private Sprite _fallbackTriangle;
    private bool _lastDetected;
    private bool _lastShowIdle;
    private Color _baseAlertColor = UIColors.Orchid;
    private Color _baseIdleColor = UIColors.TextMuted;


    public static RedactedHUD Instance { get; private set; }

    public RedactedHUD(ConfigFile configFile, RedactedBoardScanner scanner)
    {
        _configFile = configFile;
        _scanner = scanner;
        Instance = this;

        try
        {
            _enableHud = configFile.Bind(
                "General",
                "EnableHUD",
                true,
                "Show the ERROR REDACTED board notification on the player HUD.");

            _showWhenNotDetected = configFile.Bind(
                "General",
                "ShowWhenNotDetected",
                false,
                "When enabled, always show a grey idle indicator (\"no ERROR detected.\") when ERROR REDACTED is not on the board. When disabled, the HUD is hidden until the modifier appears.");

            _alertText = configFile.Bind(
                "General",
                "AlertText",
                "ERROR REDACTED has appeared!",
                "Text shown when ERROR REDACTED is on the current mission board.");

            _idleText = configFile.Bind(
                "General",
                "IdleText",
                "no ERROR detected.",
                "Text shown in idle mode when ERROR REDACTED is not on the board.");

            _enableBlink = configFile.Bind(
                "General",
                "EnableBlink",
                true,
                "Pulse the alert HUD when ERROR REDACTED is on the board so it is easier to notice.");

            _blinkSpeed = configFile.Bind(
                "General",
                "BlinkSpeed",
                2.0f,
                "Blink rate in full cycles per second (soft alpha pulse). Reasonable range: 1–3.");

            _blinkMinAlpha = configFile.Bind(
                "General",
                "BlinkMinAlpha",
                0.35f,
                "Minimum alpha during the dim phase of the blink (0–1). Higher = subtler blink.");

            _anchorX = configFile.Bind(
                "HUD Positioning",
                "AnchorX",
                0.50f,
                "X anchor position for the notification (0-1).");

            _anchorY = configFile.Bind(
                "HUD Positioning",
                "AnchorY",
                0.12f,
                "Y anchor position for the notification (0-1).");

            _enableHud.SettingChanged += (_, __) => OnEnableChanged();
            _showWhenNotDetected.SettingChanged += (_, __) => RefreshVisual(force: true);
            _alertText.SettingChanged += (_, __) => RefreshVisual(force: true);
            _idleText.SettingChanged += (_, __) => RefreshVisual(force: true);
            _enableBlink.SettingChanged += (_, __) => ApplyBlinkAlpha(1f);
            _blinkMinAlpha.SettingChanged += (_, __) => { /* applied next Update */ };
            _anchorX.SettingChanged += (_, __) => OnAnchorChanged();
            _anchorY.SettingChanged += (_, __) => OnAnchorChanged();

        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Failed to initialize RedactedHUD config: {ex.Message}");
        }
    }

    private bool IsHudAlive => HudHandle.IsValid(_hud) && _hud.Primary != null;


    public void Update()
    {
        try
        {
            if (_enableHud == null || !_enableHud.Value)
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

            bool detected = _scanner != null && _scanner.HasRedactedOnBoard;
            bool wantVisible = detected || (_showWhenNotDetected != null && _showWhenNotDetected.Value);

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
            RefreshVisual(force: false);
            UpdateBlink(detected);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Error in RedactedHUD.Update(): {ex.Message}");
        }
    }

    private void UpdateBlink(bool detected)
    {
        if (!IsHudAlive)
            return;

        if (!detected || _enableBlink == null || !_enableBlink.Value)
        {
            ApplyBlinkAlpha(1f);
            return;
        }

        float speed = _blinkSpeed != null ? Mathf.Max(0.1f, _blinkSpeed.Value) : 2f;
        float minAlpha = _blinkMinAlpha != null ? Mathf.Clamp01(_blinkMinAlpha.Value) : 0.35f;

        // Soft pulse: 1 → min → 1, about `speed` full cycles per second
        float t = Mathf.PingPong(Time.unscaledTime * speed, 1f);
        float alpha = Mathf.Lerp(minAlpha, 1f, t);
        ApplyBlinkAlpha(alpha);
    }

    private void ApplyBlinkAlpha(float alpha)
    {
        if (!IsHudAlive)
            return;

        alpha = Mathf.Clamp01(alpha);

        if (_hud.Primary != null && _hud.Primary.Tmp != null)
        {
            Color c = _lastDetected ? _baseAlertColor : _baseIdleColor;
            c.a = alpha;
            _hud.Primary.Tmp.color = c;
        }

        if (_iconImage != null)
        {
            Color ic = _iconImage.color;
            // Preserve RGB from ApplyIcon; only modulate alpha for blink
            float baseA = _lastDetected ? 1f : 0.85f;
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

        if (_enableHud == null || !_enableHud.Value)
        {
            _hud.SetActive(false);
            return;
        }

        bool detected = _scanner != null && _scanner.HasRedactedOnBoard;
        bool wantVisible = detected || (_showWhenNotDetected != null && _showWhenNotDetected.Value);
        _hud.SetActive(wantVisible);
    }

    private void OnEnableChanged()
    {
        if (_enableHud != null && !_enableHud.Value && _hud != null)
            DestroyHud();
        UpdateHudVisibility();
    }

    private void OnAnchorChanged()
    {
        if (IsHudAlive)
            _hud.SetAnchor(_anchorX.Value, _anchorY.Value);
    }

    private void ClearDestroyedHud()
    {
        if (_hud == null)
            return;

        try
        {
            if (_hud.Rect != null)
                HudRepositionClient.Unregister(SparrohPlugin.PluginGUID);
        }
        catch
        {
        }

        _hud = null;
        _iconImage = null;
    }

    private void CreateHud()
    {
        if (IsHudAlive)
            return;

        ClearDestroyedHud();

        _hud = HudBuilder.Create("RedactedNotificationHUD")
            .ParentToReticle()
            .Anchor(_anchorX.Value, _anchorY.Value)
            .Pivot(new Vector2(0.5f, 0.5f))
            .Size(420f, 32f)
            .AddText("StatusText", fontSize: 18f, alignment: TextAlignmentOptions.Center)
            .Build();

        if (!IsHudAlive)
            return;

        // Icon to the left of the text
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

            // Nudge text slightly right so it doesn't sit under the icon
            if (_hud.Primary != null && _hud.Primary.Rect != null)
            {
                var textRt = _hud.Primary.Rect;
                textRt.offsetMin = new Vector2(UITheme.S(34f), textRt.offsetMin.y);
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogWarning($"Could not create redacted icon: {ex.Message}");
            _iconImage = null;
        }

        HudRepositionClient.Register(
            SparrohPlugin.PluginGUID,
            "ERROR REDACTED Alert",
            _hud.Rect,
            _anchorX,
            _anchorY);

        _lastDetected = false;
        _lastShowIdle = false;
        RefreshVisual(force: true);
    }

    private void DestroyHud()
    {
        HudRepositionClient.Unregister(SparrohPlugin.PluginGUID);
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

        bool detected = _scanner != null && _scanner.HasRedactedOnBoard;
        bool showIdle = _showWhenNotDetected != null && _showWhenNotDetected.Value;

        if (!force && detected == _lastDetected && showIdle == _lastShowIdle)
            return;

        _lastDetected = detected;
        _lastShowIdle = showIdle;

        if (detected)
        {
            string text = _alertText != null ? _alertText.Value : "ERROR REDACTED has appeared!";
            _baseAlertColor = GetAlertColor();
            // Plain text + TMP.color so blink can modulate alpha without fighting rich-text tags
            _hud.Primary.Text = text;
            _hud.Primary.Tmp.color = _baseAlertColor;
            ApplyIcon(alert: true);
            ApplyBlinkAlpha(1f);
        }
        else if (showIdle)
        {
            string text = _idleText != null ? _idleText.Value : "no ERROR detected.";
            _baseIdleColor = UIColors.TextMuted;
            _hud.Primary.Text = text;
            _hud.Primary.Tmp.color = _baseIdleColor;
            ApplyIcon(alert: false);
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

        // Simple filled triangle in a 32x32 texture
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var clear = new Color(0f, 0f, 0f, 0f);
        var fill = Color.white;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Upward triangle
                float nx = (x + 0.5f) / size;
                float ny = (y + 0.5f) / size;
                float halfWidth = ny * 0.5f;
                bool inside = ny >= 0.12f && ny <= 0.92f && Mathf.Abs(nx - 0.5f) <= halfWidth * 0.9f;
                tex.SetPixel(x, y, inside ? fill : clear);
            }
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
            SparrohPlugin.Logger.LogError($"Error in RedactedHUD.OnDestroy(): {ex.Message}");
        }
    }
}
