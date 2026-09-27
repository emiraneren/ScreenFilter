using System.ComponentModel;

namespace ScreenFilter.Services;

public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private string _lang = "tr";

    public string Language
    {
        get => _lang;
        set
        {
            if (_lang == value) return;
            _lang = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        }
    }

    public string this[string key] => T(key);

    public static string T(string key)
    {
        var table = Instance._lang == "tr" ? Tr : En;
        if (table.TryGetValue(key, out var v)) return v;
        return En.TryGetValue(key, out var e) ? e : key;
    }

    public static string F(string key, params object[] args) => string.Format(T(key), args);

    private static readonly Dictionary<string, string> En = new()
    {
        ["app.title"] = "Screen Filter",
        ["app.subtitle"] = "Real-time color & clarity filter for games and desktop",

        ["mode.title"] = "CAPTURE MODE",
        ["mode.fast"] = "Fast (color only)",
        ["mode.monitor"] = "Monitor capture",
        ["mode.window"] = "Window capture",
        ["mode.fast.desc"] = "Applies a color matrix straight to the desktop compositor. Zero added latency and no capture, but only color sliders work (dimmed sliders are unavailable).",
        ["mode.monitor.desc"] = "Captures a whole monitor on the GPU and re-draws it through the filter shader. All effects work, including sharpen and shadow lift.",
        ["mode.window.desc"] = "Captures a single window (e.g. your game) and draws the filtered image over it. The overlay follows the window and hides when it is not in the foreground.",
        ["mode.game"] = "Game",
        ["mode.game.desc"] = "A system-wide color filter (same technique as Fast mode, so it's visible in screenshots and streams) that switches on automatically the instant your game is focused, and off the instant you switch away — no capture, no overlay. Only color sliders work, same limits as Fast mode.",
        ["game.pick"] = "Pick your game",
        ["game.pick.hint"] = "Click your game's window once — it will be auto-detected from now on.",
        ["game.detected"] = "Detected: {0}",
        ["game.notrunning"] = "Not running right now — filter will switch on once you launch it.",
        ["game.change"] = "Change",

        ["source.monitor"] = "Monitor",
        ["source.window"] = "Window",
        ["source.refresh"] = "Refresh list",
        ["source.primary"] = "primary",
        ["source.none"] = "No windows found",

        ["btn.start"] = "START FILTER",
        ["btn.stop"] = "STOP FILTER",
        ["status.off"] = "Filter is off",
        ["status.on.fast"] = "Filter active — fast mode",
        ["status.on.monitor"] = "Filter active — monitor {0}",
        ["status.on.window"] = "Filter active — {0}",
        ["status.error"] = "Could not start: {0}",
        ["status.nowindow"] = "Pick a window first",
        ["status.windowgone"] = "Target window closed — filter stopped",
        ["status.fps"] = "{0} FPS",

        ["presets.title"] = "PRESETS",
        ["presets.save"] = "Save current as preset",
        ["presets.delete"] = "Delete",
        ["presets.name"] = "Preset name",
        ["presets.custom"] = "CUSTOM",
        ["presets.builtin"] = "BUILT-IN",
        ["presets.reset"] = "Reset all",
        ["presets.saved"] = "Preset saved",

        ["sec.light"] = "LIGHT",
        ["sec.color"] = "COLOR",
        ["sec.detail"] = "DETAIL",
        ["sec.film"] = "FILM",
        ["sec.film.desc"] = "Vignette darkens the screen edges; grain adds a subtle animated film-like texture. Both are cosmetic and off by default.",
        ["s.vignette"] = "Vignette",
        ["s.grain"] = "Film grain",
        ["sec.target"] = "COLOR TARGET (ENEMY HIGHLIGHT)",
        ["sec.target.desc"] = "Boosts one hue (e.g. red enemy outlines, orange tracers) and slightly mutes everything else so it pops out.",

        ["s.darkboost"] = "Dark boost (darks only)",
        ["s.brightness"] = "Brightness",
        ["s.contrast"] = "Contrast",
        ["s.gamma"] = "Gamma",
        ["s.exposure"] = "Exposure",
        ["s.shadow"] = "Shadow lift",
        ["s.highlights"] = "Highlight control",
        ["s.saturation"] = "Saturation",
        ["s.vibrance"] = "Vibrance",
        ["s.hue"] = "Hue shift",
        ["s.temperature"] = "Temperature",
        ["s.tint"] = "Tint",
        ["s.red"] = "Red gain",
        ["s.green"] = "Green gain",
        ["s.blue"] = "Blue gain",
        ["s.grayscale"] = "Grayscale",
        ["s.sharpen"] = "Sharpen",
        ["s.clarity"] = "Clarity",
        ["s.dehaze"] = "Fog / haze removal",
        ["s.targetamount"] = "Strength",
        ["s.targethue"] = "Target hue",
        ["s.targetrange"] = "Hue width",
        ["s.invert"] = "Invert colors",
        ["s.unsupported"] = "Not available in fast mode",
        ["preset.fastwarn"] = "Not visible right now: {0} (this mode only supports plain color adjustments).",


        ["hotkeys.title"] = "HOTKEYS",
        ["hotkeys.fail"] = "Some hotkeys are already used by another app",
        ["hk.toggle"] = "Toggle filter",
        ["hk.next"] = "Next preset",
        ["hk.prev"] = "Previous preset",
        ["hk.up"] = "Brightness up",
        ["hk.down"] = "Brightness down",
        ["hk.reset"] = "Reset all",
        ["hk.press"] = "Press keys...",
        ["hk.default"] = "Restore default hotkeys",
        ["hk.needmod"] = "Use at least one of Ctrl / Alt / Shift / Win",
        ["hk.dup"] = "Already used by another action",
        ["hk.taken"] = "This combination is used by another app",
        ["hk.hint"] = "Click a key to change it. Esc cancels.",

        ["opt.onlyfg"] = "Show overlay only while the window is in foreground",
        ["opt.excludecapture"] = "Hide overlay from screenshots / streams",
        ["opt.startmin"] = "Keep running when window is closed (minimize to tray)",


        ["tray.show"] = "Show",
        ["tray.toggle"] = "Toggle filter",
        ["tray.exit"] = "Exit",

        ["lang"] = "Language",
        ["view.title"] = "VIEW",
        ["view.basic"] = "Basic",
        ["view.detailed"] = "Detailed",
        ["fps.title"] = "CAPTURE RATE (Hz)",
        ["fps.auto"] = "Auto (display refresh rate)",
        ["fps.unlimited"] = "Unlimited (not recommended)",
        ["fps.desc"] = "Maximum frames per second the filter redraws. Match your monitor; lower it if the game loses FPS.",
        ["toast.on"] = "Screen filter: ON",
        ["toast.off"] = "Screen filter: OFF",

        // presets
        ["preset.default"] = "Default (no filter)",
        ["preset.default.desc"] = "Neutral — nothing changed.",
        ["preset.darkboost"] = "Dark Boost",
        ["preset.darkboost.desc"] = "Brightens dark areas only and keeps highlights intact.",
        ["preset.competitive"] = "Competitive FPS",
        ["preset.competitive.desc"] = "Lifted shadows, punchier colors and light sharpening for spotting players faster.",
        ["preset.darkreveal"] = "Dark Corners Reveal",
        ["preset.darkreveal.desc"] = "Aggressively brightens shadows so nobody can hide in dark corners.",
        ["preset.enemyred"] = "Enemy Highlight (red)",
        ["preset.enemyred.desc"] = "Boosts red / orange enemy outlines and mutes the rest of the scene.",
        ["preset.enemypurple"] = "Enemy Highlight (purple)",
        ["preset.enemypurple.desc"] = "Boosts purple / magenta outlines used by many shooters.",
        ["preset.vibrant"] = "Vibrant",
        ["preset.vibrant.desc"] = "Rich, saturated colors with a small contrast bump.",
        ["preset.sharp"] = "Ultra Sharp",
        ["preset.sharp.desc"] = "Strong sharpening and clarity for crisp long-range targets.",
        ["preset.fog"] = "Fog Buster",
        ["preset.fog.desc"] = "Cuts through fog, smoke and washed-out maps.",
        ["preset.nightvision"] = "Night Vision",
        ["preset.nightvision.desc"] = "Green-tinted, heavily lifted image for very dark scenes.",
        ["preset.filmmode"] = "Film Mode",
        ["preset.filmmode.desc"] = "Warm-cool film grade with a soft vignette and animated grain.",
        ["preset.cinematic"] = "Cinematic",
        ["preset.cinematic.desc"] = "Warm highlights, cooler shadows and a softer palette.",
        ["preset.bw"] = "High-contrast B&W",
        ["preset.bw.desc"] = "Black & white with strong contrast; movement stands out from color noise.",
        ["preset.nightmode"] = "Night Mode (eye comfort)",
        ["preset.nightmode.desc"] = "Warm, dimmed image with less blue light for late sessions.",
    };

    private static readonly Dictionary<string, string> Tr = new()
    {
        ["app.title"] = "Ekran Filtresi",
        ["app.subtitle"] = "Oyunlar ve masaüstü için gerçek zamanlı renk ve netlik filtresi",

        ["mode.title"] = "YAKALAMA MODU",
        ["mode.fast"] = "Hızlı (yalnız renk)",
        ["mode.monitor"] = "Monitör yakalama",
        ["mode.window"] = "Pencere yakalama",
        ["mode.fast.desc"] = "Renk matrisini doğrudan masaüstü birleştiricisine uygular. Ek gecikme yok, yakalama yok; ancak yalnızca renk ayarları çalışır (soluk kaydırıcılar bu modda kullanılamaz).",
        ["mode.monitor.desc"] = "Tüm monitörü GPU üzerinde yakalar ve filtre gölgelendiricisinden geçirerek yeniden çizer. Keskinleştirme ve gölge açma dahil tüm efektler çalışır.",
        ["mode.window.desc"] = "Tek bir pencereyi (ör. oyununuzu) yakalar ve filtrelenmiş görüntüyü üzerine çizer. Katman pencereyi takip eder, pencere ön planda değilken gizlenir.",
        ["mode.game"] = "Oyun",
        ["mode.game.desc"] = "Sistem geneli bir renk filtresi (Hızlı modla aynı teknik, o yüzden ekran görüntüsü ve yayında görünür); oyunun odaklandığı an otomatik açılır, başka pencereye geçince otomatik kapanır — yakalama yok, katman yok. Yalnızca renk ayarları çalışır, Hızlı modla aynı sınırlar geçerli.",
        ["game.pick"] = "Oyununu seç",
        ["game.pick.hint"] = "Oyununun penceresine bir kere tıkla — bundan sonra otomatik tanınır.",
        ["game.detected"] = "Algılandı: {0}",
        ["game.notrunning"] = "Şu an açık değil — açtığında filtre otomatik devreye girer.",
        ["game.change"] = "Değiştir",

        ["source.monitor"] = "Monitör",
        ["source.window"] = "Pencere",
        ["source.refresh"] = "Listeyi yenile",
        ["source.primary"] = "ana",
        ["source.none"] = "Pencere bulunamadı",

        ["btn.start"] = "FİLTREYİ BAŞLAT",
        ["btn.stop"] = "FİLTREYİ DURDUR",
        ["status.off"] = "Filtre kapalı",
        ["status.on.fast"] = "Filtre aktif — hızlı mod",
        ["status.on.monitor"] = "Filtre aktif — monitör {0}",
        ["status.on.window"] = "Filtre aktif — {0}",
        ["status.error"] = "Başlatılamadı: {0}",
        ["status.nowindow"] = "Önce bir pencere seçin",
        ["status.windowgone"] = "Hedef pencere kapandı — filtre durduruldu",
        ["status.fps"] = "{0} FPS",

        ["presets.title"] = "ÖNAYARLAR",
        ["presets.save"] = "Mevcut ayarı önayar olarak kaydet",
        ["presets.delete"] = "Sil",
        ["presets.name"] = "Önayar adı",
        ["presets.custom"] = "KİŞİSEL",
        ["presets.builtin"] = "HAZIR",
        ["presets.reset"] = "Tümünü sıfırla",
        ["presets.saved"] = "Önayar kaydedildi",

        ["sec.light"] = "IŞIK",
        ["sec.color"] = "RENK",
        ["sec.detail"] = "DETAY",
        ["sec.film"] = "FİLM",
        ["sec.film.desc"] = "Vinyet ekranın kenarlarını karartır; film grenliliği hafif, hareketli bir doku ekler. İkisi de kozmetiktir, varsayılan olarak kapalıdır.",
        ["s.vignette"] = "Vinyet (kenar karartma)",
        ["s.grain"] = "Film grenliliği",
        ["sec.target"] = "RENK HEDEFİ (DÜŞMAN VURGUSU)",
        ["sec.target.desc"] = "Tek bir rengi (ör. kırmızı düşman çizgisi, turuncu izler) öne çıkarır ve geri kalanı hafifçe soldurur.",

        ["s.darkboost"] = "Karanlık aydınlatma (yalnız koyu alan)",
        ["s.brightness"] = "Parlaklık",
        ["s.contrast"] = "Kontrast",
        ["s.gamma"] = "Gama",
        ["s.exposure"] = "Pozlama",
        ["s.shadow"] = "Gölge açma",
        ["s.highlights"] = "Parlak alan kontrolü",
        ["s.saturation"] = "Doygunluk",
        ["s.vibrance"] = "Canlılık",
        ["s.hue"] = "Ton kaydırma",
        ["s.temperature"] = "Renk sıcaklığı",
        ["s.tint"] = "Renk tonu (yeşil/mor)",
        ["s.red"] = "Kırmızı kazancı",
        ["s.green"] = "Yeşil kazancı",
        ["s.blue"] = "Mavi kazancı",
        ["s.grayscale"] = "Gri tonlama",
        ["s.sharpen"] = "Keskinleştirme",
        ["s.clarity"] = "Netlik",
        ["s.dehaze"] = "Sis / pus giderme",
        ["s.targetamount"] = "Güç",
        ["s.targethue"] = "Hedef renk tonu",
        ["s.targetrange"] = "Ton genişliği",
        ["s.invert"] = "Renkleri ters çevir",
        ["s.unsupported"] = "Hızlı modda kullanılamaz",
        ["preset.fastwarn"] = "Şu an görünmüyor: {0} (bu mod yalnızca düz renk ayarlarını destekler).",


        ["hotkeys.title"] = "KISAYOLLAR",
        ["hotkeys.fail"] = "Bazı kısayollar başka bir uygulama tarafından kullanılıyor",
        ["hk.toggle"] = "Filtreyi aç/kapat",
        ["hk.next"] = "Sonraki önayar",
        ["hk.prev"] = "Önceki önayar",
        ["hk.up"] = "Parlaklık artır",
        ["hk.down"] = "Parlaklık azalt",
        ["hk.reset"] = "Tümünü sıfırla",
        ["hk.press"] = "Tuşlara bas...",
        ["hk.default"] = "Varsayılan kısayollara dön",
        ["hk.needmod"] = "Ctrl / Alt / Shift / Win tuşlarından en az birini kullan",
        ["hk.dup"] = "Başka bir işlem tarafından kullanılıyor",
        ["hk.taken"] = "Bu kombinasyon başka bir uygulama tarafından kullanılıyor",
        ["hk.hint"] = "Değiştirmek için bir tuşa tıkla. Esc iptal eder.",

        ["opt.onlyfg"] = "Katmanı yalnızca pencere ön plandayken göster",
        ["opt.excludecapture"] = "Katmanı ekran görüntüsü / yayında gizle",
        ["opt.startmin"] = "Pencere kapanınca çalışmaya devam et (tepsiye küçült)",


        ["tray.show"] = "Göster",
        ["tray.toggle"] = "Filtreyi aç/kapat",
        ["tray.exit"] = "Çıkış",

        ["lang"] = "Dil",
        ["view.title"] = "GÖRÜNÜM",
        ["view.basic"] = "Basit",
        ["view.detailed"] = "Detaylı",
        ["fps.title"] = "YAKALAMA HIZI (Hz)",
        ["fps.auto"] = "Otomatik (ekran yenileme hızı)",
        ["fps.unlimited"] = "Sınırsız (önerilmez)",
        ["fps.desc"] = "Filtrenin saniyede en fazla kaç kare çizeceği. Monitörünle aynı yap; oyunda FPS düşüyorsa azalt.",
        ["toast.on"] = "Ekran filtresi: AÇIK",
        ["toast.off"] = "Ekran filtresi: KAPALI",

        ["preset.default"] = "Varsayılan (filtre yok)",
        ["preset.default.desc"] = "Nötr — hiçbir şey değişmez.",
        ["preset.darkboost"] = "Karanlık Aydınlatma",
        ["preset.darkboost.desc"] = "Yalnızca karanlık alanları aydınlatır, parlak yerleri korur.",
        ["preset.competitive"] = "Rekabetçi FPS",
        ["preset.competitive.desc"] = "Açılmış gölgeler, canlı renkler ve hafif keskinleştirme; oyuncuları daha hızlı fark edin.",
        ["preset.darkreveal"] = "Karanlık Köşe Açıcı",
        ["preset.darkreveal.desc"] = "Gölgeleri agresif şekilde aydınlatır; karanlık köşelerde kimse saklanamaz.",
        ["preset.enemyred"] = "Düşman Vurgusu (kırmızı)",
        ["preset.enemyred.desc"] = "Kırmızı / turuncu düşman çizgilerini öne çıkarır, sahnenin geri kalanını soldurur.",
        ["preset.enemypurple"] = "Düşman Vurgusu (mor)",
        ["preset.enemypurple.desc"] = "Birçok nişancı oyunundaki mor / macenta çizgileri öne çıkarır.",
        ["preset.vibrant"] = "Canlı",
        ["preset.vibrant.desc"] = "Zengin, doygun renkler ve hafif kontrast artışı.",
        ["preset.sharp"] = "Ultra Keskin",
        ["preset.sharp.desc"] = "Uzak hedefler için güçlü keskinleştirme ve netlik.",
        ["preset.fog"] = "Sis Giderici",
        ["preset.fog.desc"] = "Sis, duman ve solgun haritaları temizler.",
        ["preset.nightvision"] = "Gece Görüşü",
        ["preset.nightvision.desc"] = "Çok karanlık sahneler için yeşil tonlu, güçlü aydınlatılmış görüntü.",
["preset.filmmode"] = "Film Modu",
        ["preset.filmmode.desc"] = "Sıcak-soğuk film tonlaması, yumuşak vinyet ve hareketli grenlilik.",
                ["preset.cinematic"] = "Sinematik",
        ["preset.cinematic.desc"] = "Sıcak parlak alanlar, soğuk gölgeler ve yumuşak bir palet.",
        ["preset.bw"] = "Yüksek Kontrastlı S&B",
        ["preset.bw.desc"] = "Güçlü kontrastlı siyah-beyaz; hareket renk karmaşasından sıyrılır.",
        ["preset.nightmode"] = "Gece Modu (göz konforu)",
        ["preset.nightmode.desc"] = "Sıcak, kısılmış ve daha az mavi ışıklı görüntü; uzun seanslar için.",
    };
}
