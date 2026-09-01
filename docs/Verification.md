# Verification Checklist

## Otomatik

- Debug ve Release derlemeleri uyarıları hata olarak ele alır.
- Core testleri küçük/boş dosya, UTF-8 BOM, UTF-16, LF/CRLF, filter, dedupe, split, merge, extract, delimiter, pipeline sırası, iptal ve güvenli çıktı davranışlarını kapsar.
- Büyük dosya testleri seyrek indeks sıçramasını, sayfalı UTF-16 okumayı, akışlı aramayı ve çok büyük tek satırda sınırlı önizlemeyi doğrular.

## Manuel sürüm kontrolü

- Uygulamayı 100%, 125% ve 150% Windows ölçeklemede açın.
- 1100×700 minimum boyuta ve daha büyük boyutlara yeniden boyutlandırın.
- Her sidebar sayfasını, boş durumunu ve klavye odağını doğrulayın.
- Ctrl+O, Ctrl+S, Ctrl+Shift+S, Ctrl+F, Ctrl+H, Ctrl+G, Ctrl+Z, Ctrl+Y ve Ctrl+A'yı doğrulayın.
- Normal ve Büyük Dosya modunda UTF-8, UTF-8 BOM, UTF-16, CRLF ve LF örneklerini açın.
- Çalışan işi iptal edin; yarım hedef veya geçici dosya kalmadığını doğrulayın.
- Uzun dosya adı, kilitli dosya, erişim reddi ve var olan çıktı hatalarını doğrulayın.
- Drag/drop ile desteklenen ve desteklenmeyen dosya türlerini doğrulayın.
