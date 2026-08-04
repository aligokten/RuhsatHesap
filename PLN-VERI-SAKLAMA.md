# Ruhsat Hesap PLN Veri Saklama

Ruhsat Hesap proje verisi, Archicad proje veritabanında aşağıdaki tekil Add-On Object adıyla saklanır:

```text
RuhsatHesap.ProjectData.v1
```

Bu kayıt `.pln` ve `.pla` proje dosyalarıyla birlikte taşınır. Harici JSON dosyası zorunlu değildir; JSON içe/dışa aktarma yedekleme ve web paneli veri alışverişi için kullanılmaya devam eder.

## Saklanan veriler

- Parsel ve imar bilgileri
- TAKS/KAKS ve emsal yöntemi verileri
- Archicad kat bağlantıları
- Bloklar ve katlar
- Bağımsız bölümler, oda sayıları ve net/brüt alanlar
- Zon bağlantıları ve kaynak zon GUID'leri
- Emsal, yüzde 30 ve yapı inşaat alanı kalemleri
- İstinat duvarları
- Ayrılan otopark sayısı
- Web panelinden alınan özgün JSON zarfı

## Çalışma akışı

1. Ruhsat Hesap paleti ilk açıldığında PLN içindeki veri otomatik okunur.
2. Archicad katları ve `RH|` zonları mevcut veriyle eşleştirilir.
3. Zon aktarımı, kat yenilemesi veya JSON içe aktarma sonrasında Add-On Object güncellenir.
4. Kullanıcı isterse **Proje Verisini PLN İçine Kaydet** düğmesiyle manuel güncelleme yapabilir.
5. Değişikliğin fiziksel `.pln` dosyasına geçmesi için Archicad projesi normal şekilde kaydedilir.

## Veri biçimi

Add-On Object içeriği UTF-8 JSON olarak tutulur ve iki ayrı sürüm bilgisi içerir:

- `storageVersion`: PLN saklama zarfının sürümü
- `schemaVersion`: Ruhsat Hesap proje modelinin sürümü

Bu ayrım, ileride veri modeli değiştiğinde eski PLN kayıtlarının dönüştürülebilmesini sağlar.

## Teamwork

Kayıt, aynı adda ikinci bir proje verisi oluşmasını engelleyen **Unique Add-On Object** olarak oluşturulur. Başka bir Teamwork kullanıcısının sahip olduğu kayıt güncellenecekse eklenti nesneyi rezerve etmeyi dener, veriyi yazar ve rezervasyonu serbest bırakır. Çakışma veya yetki sorunu palet durum mesajında gösterilir.

## Yedekleme önerisi

PLN kaydı ana veri kaynağıdır. Proje teslimleri ve önemli revizyonlarda ayrıca **JSON Projesi Kaydet** komutuyla okunabilir bir yedek oluşturulması önerilir.
