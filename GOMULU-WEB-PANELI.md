# Archicad İçinde Gömülü Web Paneli

Ruhsat Hesap 0.5.8 arayüzü harici bir internet sayfasını açmaz. `RFIX/RuhsatHesapPanel.html` dosyası derleme sırasında Archicad kaynak dosyasına, ardından `RuhsatHesap.apx` paketine gömülür. Kaynak derleyici, Graphisoft DevKit ile aynı şekilde hem `RFIX/Images` hem de `RFIX` klasöründe dış kaynak arar. Palet açıldığında bu kaynak Graphisoft `DG::Browser` kontrolünde çalıştırılır.

## Veri akışı

1. Palet açılırken C++ katmanı PLN içindeki `RuhsatHesap.ProjectData.v1` nesnesini okur.
2. `GetProjectData` çağrısı proje verisini ve hesap özetini JSON olarak web arayüzüne gönderir.
3. Kullanıcı bir alanı değiştirdiğinde panel 850 ms bekler ve `SaveProjectData` çağrısını yapar.
4. C++ katmanı JSON'u sürümlü `ProjectData` modeline dönüştürür ve PLN proje nesnesini günceller.
5. Archicad dosyasına fiziksel yazım, kullanıcının normal **Kaydet** komutuyla tamamlanır.

## JavaScript köprüsü

Panelde `window.RuhsatNative` adıyla aşağıdaki yerel işlevler bulunur:

| İşlev | Görev |
| --- | --- |
| `GetProjectData` | PLN proje verisini ve hesap özetini panele gönderir |
| `SaveProjectData` | Paneldeki güncel veriyi PLN proje nesnesine yazar |
| `ReadStories` | Archicad katlarını yeniden okur ve bloklarla eşleştirir |
| `ReadZones` | `RH|` standardındaki zonları bağımsız bölümlere aktarır |
| `SaveToPln` | Proje verisini manuel olarak PLN nesnesine kaydeder |
| `OpenJson` | Windows dosya seçicisiyle JSON içe aktarır |
| `SaveJson` | Windows dosya seçicisiyle JSON dışa aktarır |
| `ExportReports` | Excel çalışma kitabı ve PNG hesap paftası üretir |

Köprü çağrıları Graphisoft'un JavaScript bağlantısı üzerinden Archicad ana iş parçacığında çalışır. Panel, normal bir tarayıcıda geliştirme amacıyla açıldığında aynı veri modelini `localStorage` içinde tutan çevrimdışı bir yedek davranış kullanır.

## Veri sınırları

- Emsal hesabındaki kat holü ile yapı inşaat alanındaki toplam hol birbirinden bağımsız alanlardır.
- Yapı İnşaat Alanı modülü emsal tablosuna bağlanmaz; yalnız bağımsız bölüm brütleri ortak bağımsız bölüm verisinden gelir.
- İstinat duvarları bloklara bağlanmaz ve toplamları İnşaat Alanı hesabına ayrıca eklenir.
- Dinamik `%30` ve yapı inşaat alanı başlıkları `auxiliaryData` içinde saklandığından JSON ve PLN kayıtlarında korunur.

## Çalışma biçimi

Gömülü panel CEF tabanlıdır ve yerel HTML kaynağıyla çalışır. Bu nedenle ChatGPT Sites, Cloudflare veya başka bir web sunucusunun erişilebilir olması gerekmez. Excel/pafta üretimi ve JSON dosya iletişim kutuları C++ katmanında yerel Windows işlemleri olarak kalır.
