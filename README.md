# Screen Filter

Oyunlar ve masaüstü için gerçek zamanlı ekran filtresi. Parlaklık, renk, keskinlik ve **Karanlık Aydınlatma** (yalnızca koyu alanları açar, parlak yerleri patlatmaz) ayarlarını yapıp hazır önayarlarla anında sonuç al. Arayüz: Türkçe / İngilizce.

## Kullanım

1. [Releases](../../releases) sayfasından `ScreenFilter.exe` dosyasını indir ve çalıştır (.NET kurmana gerek yok).
2. Bir mod seç, **Filtreyi Başlat**'a bas.
3. Oyunu **Kenarlıksız (Borderless) / Pencereli** modda çalıştır. Tam ekran (exclusive) modda filtre çalışmaz.

## Modlar

- **Hızlı:** Yalnızca renk ayarları, ek gecikme yok.
- **Monitör yakalama:** Tüm ekrana filtre uygular, bütün ayarlar çalışır.
- **Pencere yakalama:** Sadece seçtiğin pencereye (oyuna) filtre uygular.

## Kısayollar

`Ctrl+Alt+F` aç/kapat · `Ctrl+Alt+←/→` önayar değiştir · `Ctrl+Alt+↑/↓` parlaklık · `Ctrl+Alt+Home` sıfırla

Hepsi uygulama içinden değiştirilebilir.

## Kaynaktan derleme

```
cd source
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

> Bu araç yalnızca ekrandaki pikselleri yeniden çizer, oyun dosyalarına dokunmaz. Yine de rekabetçi oyunlarda kullanım sorumluluğu sana aittir.
