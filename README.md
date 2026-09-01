# GalaXako Editor

**GX Editor 1.0.0** — Large Files. Clean Lists. Fast.

GalaXako Editor, Windows üzerinde TXT, LOG, CSV, TSV, JSONL ve diğer satır tabanlı dosyaları yerel olarak incelemek, düzenlemek ve dönüştürmek için geliştirilmiş .NET 10 WPF uygulamasıdır. Uygulama dosya içeriğini herhangi bir uzak servise göndermez.

## Özellikler

- Yapılandırılabilir 16/32/64/128 MB eşiğe sahip Normal Düzenleme ve Büyük Dosya modları
- AvalonEdit tabanlı normal editör: satır numarası, bul/değiştir, regex, sözcük eşleme, boşluk görünümü, kaydırma, geri al/yinele ve yakınlaştırma
- Büyük dosyalarda belleği sınırlı önizleme, seyrek satır indeksi, satır/bayt/yüzde gezintisi ve akışlı arama
- Clean, çoklu AND/OR Filter, Dedupe, Extract, Delimiter/Column, Sort, Split, Merge ve Compare motorları
- Büyük veri için disk bölümlü dedupe ve geçici parça tabanlı harici birleştirmeli sıralama
- Gerçek ilerleme, işlenen bayt/satır, hız, ETA ve iptal içeren İşler sayfası
- JSON olarak kaydedilen yeniden sıralanabilir pipeline'lar ve sınırlı eşzamanlı batch çalıştırma
- Güvenli çıktı: dönüşümler mevcut çıktının üzerine yazmaz; editör kaydı aynı klasörde geçici dosya ve güvenli değiştirme kullanır
- Yerel JSON ayar/geçmiş/pipeline depolama ve içerik kaydetmeyen dönen günlükler

## Ekran görüntüleri

Dağıtım ekran görüntüleri sürüm paketine eklenecektir. Uygulama içinde koyu Fluent tabanlı GalaXako tasarım sistemi, özel başlık çubuğu ve özgün vektör GX işareti bulunur.

## Mimari

- `GalaXakoEditor/`: WPF uygulaması, görünümler ve MVVM görünüm modelleri
- `src/GalaXako.Editor.Core/`: akışlı G/Ç, büyük dosya indeksi, işlemler ve pipeline motoru
- `src/GalaXako.Editor.Infrastructure/`: JSON kalıcılık ve yerel dönen günlükler
- `tests/GalaXako.Editor.Tests/`: dış servise ihtiyaç duymayan gerçek geçici dosya testleri
- `tools/GalaXako.Editor.DatasetGenerator/`: 100 MB, 1 GB veya özel boyutta benchmark verisi üreteci

Pahalı dosya işlemleri UI iş parçacığı dışında async çalışır. Görünür ilerleme yaklaşık saniyede birkaç kez güncellenir. Büyük dosya önizlemesi tek bir dev satırı dahi bütünüyle bir `string` olarak ayırmaz; görünür satır örneğini sınırlı tamponla üretir.

## Gereksinimler

- Geliştirme: Windows 10/11 x64 ve .NET 10 SDK `10.0.400` veya uyumlu daha yeni feature band
- Çalıştırma: framework-dependent Debug için .NET 10 Desktop Runtime; self-contained publish için ek runtime gerekmez
- Visual Studio ile açmak için `GalaXakoEditor.slnx`

## Derleme ve test

```powershell
dotnet restore .\GalaXakoEditor.slnx
dotnet build .\GalaXakoEditor.slnx -c Debug
dotnet test .\GalaXakoEditor.slnx -c Release
```

Depodaki `global.json`, .NET 10'un Microsoft Testing Platform çalıştırıcısını seçer.

## Yayınlama

Self-contained Windows x64 tek dosya yayını:

```powershell
dotnet publish .\GalaXakoEditor\GalaXakoEditor.csproj -c Release -p:PublishProfile=win-x64
```

Çıktı `GalaXakoEditor\bin\Release\publish\win-x64-single\` altında oluşur.

## Benchmark veri üretimi

Üretilen büyük dosyalar kaynak denetimine dahil edilmez.

```powershell
dotnet run --project .\tools\GalaXako.Editor.DatasetGenerator -- --output .\benchmark-data\sample-100mb.txt --size 100MB
dotnet run --project .\tools\GalaXako.Editor.DatasetGenerator -- --output .\benchmark-data\sample-1gb.txt --size 1GB
```

## Yerel uygulama verileri

`%LocalAppData%\GalaXakoEditor\` altında:

- `settings.json`
- `pipelines.json`
- `history.json`
- `logs\gx-YYYYMMDD.log`

Günlükler dosya içeriğini kaydetmez.

## Güvenlik ve kapsam

GX Editor genel amaçlı, yalnızca yerel metin/liste işleme aracıdır. Hesap veya kimlik bilgisi doğrulama, brute force, proxy kötüye kullanımı, yetkisiz scraping ya da üçüncü taraf servislerle veri doğrulama içermez.

Bilinen ve bilinçli olarak ertelenen etkileşimler [DeferredFeatures.md](DeferredFeatures.md) içinde açıkça listelenmiştir.
