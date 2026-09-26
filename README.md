# Screen Filter

**EN** · Real-time screen filter for games and the desktop. Tweak brightness, contrast, gamma, shadows, colors, sharpness and more, with ready-made presets, and see the result instantly. UI languages: **English / Türkçe**.

**TR** · Oyunlar ve masaüstü için gerçek zamanlı ekran filtresi. Parlaklık, kontrast, gama, gölge, renk, keskinlik ve daha fazlasını ayarlayın; hazır önayarlarla anında sonuç alın. Arayüz dilleri: **İngilizce / Türkçe**.

## Capture modes / Yakalama modları

| Mode | What it does | Notes |
|---|---|---|
| **Fast (color only)** / Hızlı | Sets a system-wide color matrix through the Windows Magnification API. | Zero added latency, no capture. Only color sliders work (brightness, contrast, saturation, hue, temperature, tint, RGB gain, grayscale, invert, exposure). |
| **Monitor capture** / Monitör yakalama | Captures a whole monitor on the GPU (Windows.Graphics.Capture) and re-draws it through a pixel shader in a click-through overlay. | All effects work: gamma, shadow lift, sharpen, clarity, dehaze, color-target highlight. |
| **Window capture** / Pencere yakalama | Captures one window (e.g. your game) and draws the filtered image exactly over it. | Overlay follows the window and hides when it is not in the foreground. |

> Games must run in **Borderless / Windowed** mode. Exclusive fullscreen cannot be filtered by any overlay tool.

## Presets / Önayarlar

Competitive FPS, Dark Corners Reveal, Enemy Highlight (red / purple), Vibrant, Ultra Sharp, Fog Buster, Night Vision, Cinematic, High-contrast B&W, Night Mode. You can save your own presets too.

## Sliders / Ayarlar

- **Light:** brightness, contrast, gamma, exposure, shadow lift, highlight control
- **Color:** saturation, vibrance, hue shift, temperature, tint, R/G/B gain, grayscale, invert
- **Detail:** sharpen, clarity, fog/haze removal
- **Color target:** boosts one hue (e.g. red enemy outlines) and mutes the rest

Double-click a slider label to reset that slider.

**Capture rate (Hz):** in monitor/window modes you can cap how many frames per second the filter redraws (Auto = your display refresh rate). Lower it if the game loses FPS. Rates snap to the nearest value your display can deliver.

## Hotkeys / Kısayollar

All hotkeys can be changed in the app (click a key, press the new combination). / Tüm kısayollar uygulamadan değiştirilebilir (bir tuşa tıkla, yeni kombinasyona bas).

| Keys | Action |
|---|---|
| `Ctrl+Alt+F` | Toggle filter |
| `Ctrl+Alt+→` / `←` | Next / previous preset |
| `Ctrl+Alt+↑` / `↓` | Brightness up / down |
| `Ctrl+Alt+Home` | Reset all |

A small message appears at the top of the screen so you get feedback while in-game. Closing the window minimizes to the tray (can be turned off).

## Run / Çalıştırma

Download `ScreenFilter.exe` from **Releases** and run it. It is a single self-contained file, no .NET install needed. Windows 10 (2004+) / Windows 11.

Build from source (project is in the `source` folder):

```
cd source
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

## Notes / Notlar

- This tool only re-draws pixels that are already on your screen. It never reads or writes game files or memory. Some competitive games or anti-cheats may still have rules about overlays or color filters — use it at your own risk.
- Bu araç yalnızca ekrandaki pikselleri yeniden çizer; oyun dosyalarına veya belleğine dokunmaz. Yine de bazı rekabetçi oyunların kuralları katman/renk filtrelerini kısıtlayabilir; sorumluluk size aittir.
- Settings are stored in `%AppData%\ScreenFilter\config.json`.

**Developer:** emirhaneren34@gmail.com
