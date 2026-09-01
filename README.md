# GalaXako Editor

> **Büyük Dosyalar. Temiz Listeler. Hızlı İşlem.**

Windows için geliştirilmiş modern ve yüksek performanslı metin/liste işleme uygulaması.

**C# · .NET 10 · WPF · MVVM**

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D4)
![Language](https://img.shields.io/badge/Dil-C%23-239120)
![Status](https://img.shields.io/badge/Durum-Geliştiriliyor-orange)

---

## GalaXako Editor Nedir?

GalaXako Editor; büyük **TXT, LOG, CSV, TSV ve JSONL** dosyalarını görüntülemek, temizlemek, filtrelemek ve dönüştürmek için geliştirilmiştir.

Klasik bir Not Defteri alternatifi olmaktan çok, büyük satır tabanlı dosyalar üzerinde hızlı işlem yapmaya odaklanır.

Tüm işlemler **yerel olarak bilgisayarınızda** gerçekleştirilir. Dosya içerikleri herhangi bir uzak sunucuya gönderilmez.

---

## Özellikler

### Büyük Dosya Desteği

- Dosyanın tamamını RAM'e yüklemeden görüntüleme
- Akış tabanlı dosya okuma
- Seyrek satır indeksi
- Büyük dosyalarda hızlı gezinme
- Satır, bayt ve yüzde üzerinden konuma gitme
- Akış tabanlı arama

### Metin Editörü

- Satır numaraları
- Bul / Değiştir
- Regex desteği
- Büyük/küçük harf duyarlılığı
- Satıra git
- Yakınlaştırma
- Geri al / yinele
- Encoding ve satır sonu bilgileri

### Temizleme

- Baş ve sondaki boşlukları kaldırma
- Boş satırları silme
- Tekrarlanan satırları kaldırma
- Boşlukları normalleştirme
- Satır sonlarını normalleştirme
- Minimum / maksimum satır uzunluğu
- Büyük / küçük harf dönüşümü

### Filtreleme

- İçerir
- İçermez
- Bununla başlar
- Bununla biter
- Eşittir
- Eşit değildir
- Regex
- Satır uzunluğu kuralları
- AND / OR koşulları

### Veri Ayıklama

Metin içerisinden:

- URL
- Alan adı
- E-posta
- IPv4 / IPv6
- MD5
- SHA-1
- SHA-256
- Özel Regex

değerleri ayıklanabilir.

### Sütun Araçları

CSV ve diğer ayraç tabanlı dosyalarda:

- Sütun çıkarma
- Sütun kaldırma
- Sütunları yeniden sıralama
- Sütun birleştirme
- Sütuna göre filtreleme

işlemleri yapılabilir.

### Sıralama

- A-Z / Z-A
- Sayısal artan / azalan
- Kısadan uzuna
- Uzundan kısaya
- Doğal sıralama

### Böl & Birleştir

Dosyalar:

- Satır sayısına göre
- Yaklaşık dosya boyutuna göre
- Regex sınırlarına göre

bölünebilir.

Birden fazla dosya akış tabanlı olarak birleştirilebilir.

### Karşılaştırma

İki dosya arasında:

- Yalnızca A'da bulunan satırlar
- Yalnızca B'de bulunan satırlar
- Ortak satırlar
- Farklı satırlar

tespit edilebilir.

### Pipeline

Birden fazla işlem art arda çalıştırılabilir.

Örnek:

```text
Dosya
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
Çıktı
```

---

## Performans

GalaXako Editor büyük dosyalarda bellek kullanımını sınırlı tutacak şekilde tasarlanmıştır.

Kullanılan başlıca yöntemler:

- Streaming I/O
- `FileStream`
- Async işlemler
- CancellationToken
- Seyrek indeksleme
- Disk destekli geçici işlemler
- Harici sıralama
- Sınırlı eşzamanlılık

Dosya işleme işlemleri arayüz iş parçacığından ayrı çalıştırılır.

---

## Gizlilik

- Telemetri yok
- Analitik yok
- Dosya yükleme yok
- Uzak sunucuya veri gönderimi yok

Uygulama ayarları yerel olarak şu klasörde tutulur:

```text
%LocalAppData%\GalaXakoEditor\
```

---

## Teknolojiler

- **C#**
- **.NET 10**
- **WPF**
- **MVVM**
- **AvalonEdit**

---

## Proje Yapısı

```text
GalaXakoEditor/
├── GalaXakoEditor/
├── src/
│   ├── GalaXako.Editor.Core/
│   └── GalaXako.Editor.Infrastructure/
├── tests/
│   └── GalaXako.Editor.Tests/
└── tools/
    └── GalaXako.Editor.DatasetGenerator/
```

---

## Derleme

Repoyu klonlayın:

```powershell
git clone https://github.com/AliAkgun0/gxeditor.git
cd gxeditor
```

Bağımlılıkları yükleyin:

```powershell
dotnet restore .\GalaXakoEditor.slnx
```

Projeyi derleyin:

```powershell
dotnet build .\GalaXakoEditor.slnx -c Release
```

Testleri çalıştırın:

```powershell
dotnet test .\GalaXakoEditor.slnx -c Release
```

---

## Durum

GalaXako Editor şu anda **aktif olarak geliştirilmektedir**.

Özellikler otomatik ve manuel testlerden geçirilmektedir. İlk kararlı sürüm öncesinde bazı hata ve arayüz sorunları bulunabilir.

---

## Planlanan Geliştirmeler

- UI/UX iyileştirmeleri
- Önizleme sisteminin geliştirilmesi
- Büyük dosya performans testleri
- Batch işlemlerinin geliştirilmesi
- Encoding seçeneklerinin genişletilmesi
- Portable / Installer sürümü

---

## Hata Bildirimi

Bir hata bildirirken mümkünse şunları ekleyin:

- GalaXako Editor sürümü
- Windows sürümü
- Dosya türü
- Yaklaşık dosya boyutu
- Hatayı tekrar oluşturma adımları

Hassas dosya içeriklerini paylaşmayın.

---

<p align="center">
  <strong>GalaXako Editor</strong><br>
  Büyük Dosyalar. Temiz Listeler. Hızlı İşlem.
</p>
