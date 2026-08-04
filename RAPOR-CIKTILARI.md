# Excel ve Pafta Tablosu Çıktıları

## Kullanım

1. Ruhsat Hesap paletini açın.
2. Gerekirse **Zonlardan Bağımsız Bölümleri Aktar** komutuyla modeli yenileyin.
3. **Excel ve Pafta Tablosu Üret** düğmesine basın.
4. Çıktı klasörünü seçin.

Add-On aynı proje adıyla iki dosya üretir:

- `<Proje>_Ruhsat_Hesap.xlsx`
- `<Proje>_Ruhsat_Hesap_Pafta.png`

Dosya adındaki Windows tarafından kabul edilmeyen karakterler güvenli karakterlerle değiştirilir.

## Excel çalışma kitabı

Çalışma kitabı aşağıdaki sayfalardan oluşur:

| Sayfa | İçerik ve bağlantı |
|---|---|
| Özet | Parsel girdileri, TAKS/KAKS, emsal kontrolü, ağaç ve otopark sonucu |
| Emsal Hesabı | Blok/kat satırları, değişken `%30` kalemleri, emsal dışı, emsal ve toplam inşaat alanı |
| Bağımsız Bölümler | Zonlardan gelen net/brüt alanlar, eklentiler, balkon ve otopark payı |
| Kat İrtifakı | Bağımsız bölüm alanlarından otomatik gelen blok/no/brüt/net ve manuel tapu alanları |
| Yapı İnşaat Alanı | Bağımsız bölüm brüt alanı ile kullanıcı tanımlı ortak alan kalemleri; emsalden bağımsızdır |
| İnşaat Alanı | Yapı İnşaat Alanı kat yüzölçümlerini blok bazında toplar; istinat duvarını blok dışı satırda ekler |
| Diğer Hesaplar | İstinat duvarları, 0.00/subasman kotları, otopark, kazı-dolgu ve sığınak notları |

Mavi hücreler proje girdilerini, yeşil çerçeveli hücreler formül sonuçlarını gösterir. Özet, blok toplamları ve genel toplamlar sabit değer değil Excel formülüdür. Çalışma kitabı açıldığında tam yeniden hesaplama istenir. Sayısal alanlar Türkçe yerel biçimde iki ondalık basamakla (`1,23`) gösterilir.

`Emsale Konu Kat Holü`, Emsal Hesabı sayfasındaki `%30` kalemidir. `Toplam Kat Holü` ise Yapı İnşaat Alanı sayfasındaki bağımsız yapı alanı kalemidir; iki veri birbirine bağlanmaz.

## A1 pafta tablosu

PNG çıktı A1 yatay oranında, 150 DPI ve `4967 × 3508 px` çözünürlükte üretilir. Alan değerleri virgülden sonra iki basamakla gösterilir. İçeriğinde:

- parsel / TAKS–KAKS özeti,
- emsal hesabı,
- yapı inşaat ve inşaat alanı özeti,
- bağımsız bölüm / kat irtifakı tablosu,
- istinat duvarı ve kotlar,
- genel toplamlar ile emsal/otopark uygunluk sonucu

yer alır.

Archicad'de bir Layout açıp PNG dosyasını paftaya sürükleyebilir veya uygun dış çizim yerleştirme komutuyla seçebilirsiniz. Kaynak proje verisi değiştiğinde raporu yeniden üretmek gerekir. Aktif Layout'a otomatik yerleştirme ayrı bir sonraki geliştirme adımıdır.

## Veri ilkeleri

- Her iki çıktı, düğmeye basıldığı anda PLN'de bulunan ve zonlardan yenilenen aynı `ProjectData` anlık görüntüsünden üretilir.
- Bağımsız bölüm brüt alanı tek kaynaktır ve ilgili tüm rapor sayfalarında aynı değeri kullanır.
- Yapı İnşaat Alanı verisi Emsal Hesabı tablosundan türetilmez.
- İstinat duvarı hiçbir bloğa bağlanmaz; yalnız İnşaat Alanı genel toplamına eklenir.
