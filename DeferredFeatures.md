# Deferred Features

GX Editor 1.0.0'da güvenilir olmayan kontroller gösterilmemiştir. Aşağıdaki etkileşimler daha sonraki sürüme bırakılmıştır:

- İş türüne göre kalıcı **Pause/Resume**: akış durumunu ve geçici dosya bütünlüğünü süreçler arası güvenle geri yükleyen checkpoint formatı henüz yoktur. İptal güvenilir biçimde desteklenir.
- Merge listesini fareyle sürükleyerek yeniden sıralama ve ayrı Move Up/Move Down kontrolleri: mevcut seçim dosya seçicideki sırayı kullanır; akışlı birleştirme motoru tamamdır.
- Batch çıktısı için UI üzerinden özel kök klasör seçme ve dizin yapısını koruma seçeneği: Core API bu seçenekleri destekler; mevcut UI güvenli varsayılanla her girişin yanına `_pipeline` çıktısı üretir.
- Delimiter önizlemesinde etkileşimli tablo/sütun sürükleme: akışlı extract/remove/reorder/join/filter motorları ve alanları vardır; görsel tablo düzenleyici yoktur.
- Windows azaltılmış hareket tercihinin otomatik algılanması: mevcut arayüz uzun/dekoratif animasyon kullanmaz.
- Kullanıcıya açık özel G/Ç tamponu ve toplu çıktı kodlama dönüşümü: motorlar güvenli 1 MB tampon ve kodlama koruma kullanır; Extract çıktısı UTF-8'dir. Yanlış bir kontrol sunmamak için bu iki ileri ayar UI'da gösterilmez.

Bu maddeler için uygulamada çalışmayan düğme veya sahte ilerleme bulunmaz.
