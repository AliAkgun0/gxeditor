# GalaXako Editor

> **Büyük Dosyalar. Temiz Listeler. Hızlı İşlem.**

Büyük metin tabanlı dosyaları görüntülemek, temizlemek, filtrelemek, ayıklamak ve dönüştürmek için geliştirilmiş modern bir Windows masaüstü uygulaması.

**C# · .NET 10 · WPF · MVVM** ile geliştirilmiştir.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D4)
![Dil](https://img.shields.io/badge/Dil-C%23-239120)
![Arayüz](https://img.shields.io/badge/UI-WPF-68217A)
![Durum](https://img.shields.io/badge/Durum-Geliştiriliyor-orange)

---

## Hakkında

**GalaXako Editor**, TXT, LOG, CSV, TSV, JSONL ve diğer satır tabanlı dosyalar üzerinde hızlı ve yerel işlem yapmak için geliştirilmiştir.

Amaç klasik bir Not Defteri alternatifi olmak değil; özellikle büyük veri dosyalarında:

- hızlı görüntüleme,
- filtreleme,
- temizleme,
- tekrarları kaldırma,
- veri ayıklama,
- sıralama,
- bölme,
- birleştirme

gibi işlemleri tek uygulamada sunmaktır.

Tüm dosya işlemleri **yerel olarak bilgisayarınızda** gerçekleştirilir.

Dosya içerikleri uzak sunuculara gönderilmez.

---

## Özellikler

### Büyük Dosya Modu

- Akış tabanlı dosya okuma
- Seyrek satır indeksi
- Sayfalı / sanallaştırılmış önizleme
- Satır, bayt konumu veya yüzde üzerinden gezinme
- Dosyanın tamamını RAM'e yüklemeden arama
- Yapılandırılabilir normal / büyük dosya eşiği

### Editör

- AvalonEdit tabanlı metin editörü
- Satır numaraları
- Bul / Değiştir
- Regex arama
- Büyük-küçük harf duyarlılığı
- Tam kelime eşleme
- Satır kaydırma
- Satıra git
- Yakınlaştırma
- Geri al / Yinele
- Encoding ve satır sonu bilgileri

### Temizleme

- Satır başı ve sonu boşluklarını temizleme
- Boş satırları kaldırma
- Tekrarlanan boşlukları normalleştirme
- Satır sonlarını normalleştirme
- Tekrarlanan satırları kaldırma
- Minimum / maksimum satır uzunluğu filtresi
- Büyük harf / küçük harf dönüşümü

### Filtreleme

Birden fazla kural oluşturulabilir:

- İçerir
- İçermez
- Bununla başlar
- Bununla biter
- Eşittir
- Eşit değildir
- Regex eşleşmesi
- Uzunluk kuralları
- AND / OR mantığı

### Ayıklama

Metin içinden yapılandırılmış veriler çıkarılabilir:

- URL
- Alan adı
- E-posta biçimleri
- IPv4
- IPv6
- MD5
- SHA-1
- SHA-256
- Özel Regex

### Sütun Araçları

Ayraç tabanlı veriler için genel amaçlı işlem araçları:

- CSV
- TSV
- `:`
- `|`
- özel ayraçlar

Desteklenen işlemler:

- sütun çıkarma
- sütun kaldırma
- sütunları yeniden sıralama
- sütun birleştirme
- sütuna göre filtreleme

### Sıralama

- A-Z
- Z-A
- Sayısal artan
- Sayısal azalan
- Kısadan uzuna
- Uzundan kısaya
- Doğal sıralama

Büyük dosyalarda disk destekli harici sıralama yaklaşımı kullanılabilir.

### Böl & Birleştir

Dosyalar şu şekilde bölünebilir:

- satır sayısına göre
- yaklaşık dosya boyutuna göre
- regex sınırlarına göre

Birden fazla dosya akış tabanlı şekilde birleştirilebilir.

### Karşılaştırma

İki satır tabanlı dosya karşılaştırılabilir:

- yalnızca A'da olanlar
- yalnızca B'de olanlar
- ortak satırlar
- farklı satırlar

### Pipeline

Birden fazla işlem art arda çalıştırılabilir.

Örnek:

```text
Girdi
  ↓
Boşlukları Temizle
  ↓
Boş Satırları Kaldır
  ↓
Filtrele
  ↓
Tekrarları Kaldır
  ↓
Sırala
  ↓
Dışa Aktar

Pipeline'lar yerel olarak kaydedilebilir ve batch işlemlerde tekrar kullanılabilir.

Performans Yaklaşımı

GalaXako Editor, çok büyük dosyaları tek bir string veya dizi içine yüklememek üzere tasarlanmıştır.

Büyük dosya işlemlerinde kullanılan başlıca yaklaşımlar:

FileStream
akış tabanlı işleme
sınırlı bellek kullanımı
seyrek indeks
disk destekli geçici işlemler
harici birleştirmeli sıralama
sınırlandırılmış eşzamanlılık
async işlemler
iptal desteği
seyreltilmiş ilerleme güncellemeleri

Pahalı dosya işlemleri UI iş parçacığından ayrı çalıştırılır.

Gizlilik

GX Editor yerel çalışan bir masaüstü uygulamasıdır.

Varsayılan olarak telemetri yok
Analitik yok
Dosya yükleme yok
Uzak sunucuya içerik gönderimi yok

Yerel uygulama verileri şu klasörde saklanır:

%LocalAppData%\GalaXakoEditor\
Proje Yapısı
GalaXakoEditor/
│
├── GalaXakoEditor/
│   └── WPF uygulaması / Views / ViewModels
│
├── src/
│   ├── GalaXako.Editor.Core/
│   └── GalaXako.Editor.Infrastructure/
│
├── tests/
│   └── GalaXako.Editor.Tests/
│
└── tools/
    └── GalaXako.Editor.DatasetGenerator/
Gereksinimler
Geliştirme
Windows 10 / Windows 11 x64
.NET 10 SDK
Visual Studio 2026 veya uyumlu bir IDE
Çalıştırma

Self-contained Windows x64 sürümünde son kullanıcının ayrıca .NET Runtime kurması gerekmez.

Derleme

Repoyu klonlayın:

git clone https://github.com/AliAkgun0/gxeditor.git
cd gxeditor

Bağımlılıkları yükleyin:

dotnet restore .\GalaXakoEditor.slnx

Release derlemesi:

dotnet build .\GalaXakoEditor.slnx -c Release

Testleri çalıştırın:

dotnet test .\GalaXakoEditor.slnx -c Release
Yayınlama

Windows x64 için self-contained sürüm:

dotnet publish .\GalaXakoEditor\GalaXakoEditor.csproj -c Release -p:PublishProfile=win-x64

Çıktı klasörü:

GalaXakoEditor\bin\Release\publish\win-x64-single\
Geliştirme Durumu

GX Editor aktif olarak geliştirilmektedir.

Çekirdek dosya işleme özellikleri hem otomatik hem manuel testlerden geçirilmektedir.

İlk kararlı sürüm öncesinde bazı UI ve edge-case problemleri bulunabilir.

Yol Haritası

Planlanan geliştirmeler:

UI/UX iyileştirmeleri
tüm modüllerde daha kapsamlı önizleme desteği
gelişmiş büyük dosya stres testleri
batch işlem kontrollerinin iyileştirilmesi
gelişmiş encoding seçenekleri
daha kapsamlı benchmark sonuçları
installer / portable paketleme
Katkıda Bulunma

Bug raporları, özellik önerileri ve pull request'ler kabul edilir.

Bug bildirirken mümkünse şunları ekleyin:

GX Editor sürümü
Windows sürümü
dosya türü
yaklaşık dosya boyutu
hatayı tekrar oluşturma adımları

Hata bildirirken hassas dosya içeriklerini paylaşmayın.

Lisans

İlk kararlı sürümden önce uygun bir lisans seçilecektir.

<p align="center"> <strong>GalaXako Editor</strong><br> Büyük Dosyalar. Temiz Listeler. Hızlı İşlem. </p> ```
