# Ruhsat Hesap — Archicad 29 Windows Add-On

Bu paket, Ruhsat Hesap web arayüzünü Archicad 29.2.0 Build 5003 (Windows x64) içinde çevrimdışı çalışan sabitlenebilir bir palete dönüştüren yerel eklentidir.

## Bu sürümde hazır olanlar

- Archicad `Seçenekler` menüsüne **Ruhsat Hesap Paneli** komutu eklenmesi
- Archicad içinde açılıp kapanabilen, yeniden boyutlandırılabilen ve kenara sabitlenebilen gömülü web paleti
- İnternet bağlantısı veya harici web adresi gerektirmeyen, `.apx` içine gömülü tek dosyalı HTML/CSS/JavaScript arayüz
- Gömülü panel ile Archicad C++ API arasında `RuhsatNative` JavaScript köprüsü
- Açılır/kapanır yan menü ve dar sabitlenmiş palette otomatik uyarlanan responsive görünüm
- Parsel, TAKS/KAKS, ağaç, emsal, bağımsız bölüm, kat irtifakı, yapı inşaat alanı, inşaat alanı, istinat duvarı, kot, sığınak, otopark ve kazı-dolgu modülleri
- Paletin Archicad çalışma ortamındaki görünürlük ve sabitlenme durumunun GUID üzerinden korunması
- Plan, kesit, görünüş, 3B, detay, çalışma sayfası ve pafta pencerelerinde palet desteği
- BIM katmanları ve RH monogramından oluşan ortak web/Add-On marka ikonu
- Archicad Kat Ayarları listesindeki kat adı, kat indeksi, benzersiz kat kimliği ve kotun otomatik okunması
- Palet açıldığında ve Archicad katları düzenlendiğinde kat listesinin otomatik yenilenmesi
- Okunan katların mevcut tüm bloklara yalnız eksik kayıtlar eklenerek aktarılması
- `RH` zon adı standardındaki zonlardan blok, bağımsız bölüm no, bulunduğu kat, oda sayısı ve alanların okunması
- Net, brüt, eklenti net/brüt ve balkon zon alanlarının bağımsız bölüm bazında toplanması
- `ORTAK`, `MERDIVEN`, `HOL`, `EMSAL`, `EMSAL_DISI`, `SIGINAK`, `SACAK` ve `ASANSOR` zonlarının ilgili ortak alan, yapı inşaat alanı, emsal ve sığınak modüllerine blok/kat bazında aktarılması
- `MERDIVEN`, `HOL`, `SACAK` ve `ASANSOR` zonlarında `HESAP=EMSAL` ile aynı alanın Emsal Hesabı %30 istisna tablosuna yönlendirilmesi
- Sabit listede olmayan herhangi bir `TIP` değeriyle (örn. `TIP=HAVUZ_KENARI`) kendi Yapı İnşaat Alanı / Emsal Hesabı %30 kaleminin otomatik oluşturulması
- Emsal hesabındaki kat holü ile Yapı İnşaat Alanı toplam hol verisinin birbirinden bağımsız tutulması
- Bağımsız bölümlerin panel, JSON, Excel ve PNG paftada numaraya göre doğal sayısal sırada (`7, 8, 9, 10`) gösterilmesi
- Kat ve alan satırı adlarının Archicad çizim odağına kaçmayan panel içi metin penceresiyle girilmesi
- Palet açıldığında otomatik zon taraması ve **Zonlardan Bağımsız Bölümleri Aktar** komutuyla manuel yenileme
- Zon aktarımının tekrar çalıştırıldığında alanları mükerrer toplamayacak şekilde güncellenmesi
- Archicad zon GUID'lerinin veri kaynağı bilgisi olarak JSON'a kaydedilmesi
- Tüm Ruhsat Hesap proje verisinin sürümlü bir Add-On Object içinde PLN/PLA proje veritabanına gömülmesi
- Palet ilk açıldığında gömülü PLN verisinin otomatik yüklenmesi
- Kat/zon senkronizasyonu ve JSON içe aktarma sonrasında PLN verisinin otomatik güncellenmesi
- **Proje Verisini PLN İçine Kaydet** düğmesiyle manuel kayıt
- Tekil nesne adı ve Teamwork rezervasyon akışıyla güvenli proje içi veri yönetimi
- Web panelinden dışarı aktarılan JSON proje dosyasını açma
- JSON içindeki parsel, blok, kat, bağımsız bölüm, yapı alanı ve istinat duvarı verilerini okuma
- TAKS, KAKS/emsal, ağaç, otopark ve inşaat alanı çekirdek hesapları
- PLN'deki güncel proje verisinden yedi sayfalı, formüllü `.xlsx` çalışma kitabı üretimi
- Aynı proje anlık görüntüsünden A1 yatay, 150 DPI (`4967 × 3508 px`) `.png` hesap paftası üretimi
- Excel ve PNG paftadaki alan değerlerinin Türkçe `1,23` biçiminde iki ondalık basamakla gösterilmesi
- Emsal ile Yapı İnşaat Alanı kaynaklarının raporda birbirinden bağımsız tutulması
- İstinat duvarı toplamının herhangi bir bloğa bağlanmadan İnşaat Alanı hesabına eklenmesi
- Excel ve pafta dosyalarının tek klasör seçimiyle birlikte oluşturulması
- JSON dosyasını kaydetme
- Hesap motorunun Archicad API'den ayrılmış test edilebilir C++20 yapısı

## Geliştirme durumu

Bu paket `0.5.9` geliştirme sürümüdür. Graphisoft tarafından onaylanan Developer ID ve **Ruhsat Hesap** Local ID, `RFIX/AddOnFix.grc` içindeki `MDID` kaynağına uygulanmıştır. Eklenti yeniden derlendiğinde normal Archicad 29 oturumunda yüklenmeye hazırdır.

Modal başlangıç penceresi kaldırılmıştır. **Seçenekler → Ruhsat Hesap Paleti** komutu artık Archicad'i kilitlemeden çalışan tek örnekli bir paleti gösterir veya gizler. Paletin tek kontrolü Graphisoft `DG::Browser` bileşenidir; yerel web arayüzü kaynak olarak `.apx` içine gömülür. Palet başlığındaki kapatma düğmesi paleti yok etmek yerine gizler; böylece aynı oturumda veri ve konum korunur.

Panelde yapılan değişiklikler kısa bir gecikmeyle C++ katmanına gönderilir ve PLN içindeki proje nesnesine yazılır. Kat okuma, zon aktarımı, PLN kaydı, JSON açma/kaydetme ve Excel + pafta üretimi doğrudan web arayüzündeki komutlardan yerel Archicad API işlevlerine bağlanır. Köprü ve veri akışı [GOMULU-WEB-PANELI.md](GOMULU-WEB-PANELI.md) dosyasında açıklanmıştır.

Palet her açıldığında `ACAPI_ProjectSetting_GetStorySettings` ile Kat Ayarları listesini okur. Archicad içinde kat ekleme, silme, yeniden adlandırma veya kot değiştirme işlemi yapıldığında `APINotify_ChangeProjectDB` bildirimiyle liste otomatik yenilenir. **Archicad Katlarını Yeniden Oku** düğmesi de manuel yenileme olanağı sağlar. Alanları daha önce doldurulmuş kat kayıtları korunur; senkronizasyon yalnız eksik katları ekler.

Palet ayrıca Archicad projesindeki zonları tarar. Adı `RH|` ile başlayan zonlar, [ZON-ADLANDIRMA-STANDARDI.md](ZON-ADLANDIRMA-STANDARDI.md) dosyasındaki kurallarla bağımsız bölüm, ortak alan, yapı inşaat alanı, emsal ve sığınak kayıtlarına dönüştürülür. Kat adı zonun Archicad ana katından otomatik alınır. `RH|` ile başlamayan normal mahal zonları hesaba dahil edilmez.

Ruhsat Hesap verileri `RuhsatHesap.ProjectData.v1` adlı tekil Add-On Object içinde PLN proje veritabanında tutulur. Palet ilk açıldığında kayıt otomatik okunur. Zon/kat aktarımı veya JSON içe aktarma sonrasında gömülü kayıt güncellenir. Verinin fiziksel `.pln` dosyasına yazılması için Archicad projesinin normal **Kaydet** komutuyla kaydedilmesi gerekir. Ayrıntılar [PLN-VERI-SAKLAMA.md](PLN-VERI-SAKLAMA.md) dosyasındadır.

Paletteki **Excel ve Pafta Tablosu Üret** komutu önce Archicad zonlarını yeniler, proje verisini PLN nesnesine yazar ve kullanıcıdan bir çıktı klasörü seçmesini ister. Aynı klasörde formüllü Excel çalışma kitabı ile A1 yatay, 150 DPI PNG hesap paftası oluşturulur. PNG pafta Archicad paftasına dış çizim olarak yerleştirilebilir. Sayfa içerikleri ve veri bağlantıları [RAPOR-CIKTILARI.md](RAPOR-CIKTILARI.md) belgesinde açıklanmıştır.

## Gereksinimler

- Archicad 29.2.0 Build 5003 x64
- Archicad API Development Kit 29
- Visual Studio 2022 ve v143 araç seti
- CMake 3.19+
- Python 3.10+

## Derleme

1. [Archicad 29 API DevKit'i](https://github.com/GRAPHISOFT/archicad-api-devkit/releases/download/29.3100/API.Development.Kit.WIN.29.3100.zip) indirip bir klasöre açın. Arşivin içindeki `Support` klasörü derleme için kullanılan yoldur.
2. Proje klasöründe derleme betiğini çalıştırın.

PowerShell (önerilen):

```powershell
cd C:\RuhsatHesap-v0.5.9\archicad-ruhsat-hesap
.\build-windows.ps1 -DevKitDir "C:\Graphisoft\API.Development.Kit.WIN.29.3100\Support"
```

Komut istemi:

```bat
cd /d C:\RuhsatHesap-v0.5.9\archicad-ruhsat-hesap
set AC_API_DEVKIT_DIR=C:\Graphisoft\API.Development.Kit.WIN.29.3100\Support
build-windows.bat
```

`-DevKitDir` / `AC_API_DEVKIT_DIR` verilmezse betikler DevKit'i bilinen kurulum
konumlarında kendileri arar. Her iki betik de CMake ve Python'ı bulur, DevKit
klasörünü doğrular ve sonunda üretilen `.apx` dosyasının tam yolunu yazar.

Üretilen dosya:

```
Build\Release\RuhsatHesap.apx
```

Alternatif CMake komutu:

```bat
cmake -B Build -G "Visual Studio 17 2022" -A x64 -T v143 ^
  -DAC_VERSION=29 ^
  -DAC_API_DEVKIT_DIR="C:\Graphisoft\API.Development.Kit.WIN.29.3100\Support" ^
  -DAC_WIN_LANGCHARSET=040904b0 -DAC_WIN_LANGUAGEID=1033 -DAC_WIN_CHARSETID=1200
cmake --build Build --config Release
```

`AC_WIN_*` değerleri Windows sürüm kaynağı (`VERSIONINFO`) için dil/karakter
kümesi çiftini belirler. Verilmezlerse `CMakeLists.txt` bunları INT dili için
`040904b0 / 1033 / 1200` olarak varsayar; komut satırında verilen değerler
her zaman önceliklidir.

### Derleme sorunları

| Belirti | Neden ve çözüm |
| --- | --- |
| `Build` klasörü hiç oluşmuyor | `cmake` PATH üzerinde değil. CMake 3.19+ kurun ya da Visual Studio Installer'dan **C++ CMake tools for Windows** bileşenini ekleyin. Betikler artık Visual Studio ile gelen `cmake.exe`'yi de otomatik bulur. |
| `build-windows.bat` hiçbir çıktı vermeden kapanıyor | Dosya LF satır sonlarıyla açılmış olabilir; `cmd.exe` LF-only `.bat` dosyalarındaki çok satırlı blokları hatalı ayrıştırır. Depodaki `.gitattributes` bu dosyaları CRLF olarak sabitler. |
| `rc.exe` sürüm kaynağında sözdizimi hatası veriyor | `AC_WIN_LANGCHARSET` / `AC_WIN_LANGUAGEID` / `AC_WIN_CHARSETID` verilmemiş. Güncel `CMakeLists.txt` bunlara varsayılan atar. |
| `CompileResources.py` hatası | Python 3.10+ PATH üzerinde değil. Kurulumda **Add python.exe to PATH** seçeneğini işaretleyin. |
| Uyarılar hata olarak derlemeyi kesiyor | Üst proje `/W4 /WX` kullanır. `-DAC_ADDON_WARNINGS_AS_ERRORS=OFF` ile kapatılabilir; derleme betikleri bunu zaten geçer. |
| DevKit yolu hatası | Yol DevKit'in `Support` **alt** klasörünü göstermelidir, üst klasörünü değil. |

## Archicad'e yükleme

Derleme betiğiyle Release yapılandırmasında üretilen `Build\Release\RuhsatHesap.apx` dosyasını normal Archicad 29 oturumunda `Seçenekler → Eklenti Yöneticisi → Ekle` yolundan seçin. Bu sürüm gerçek MDID içerdiği için `-DEMO` başlatma parametresi gerekmez.

## JSON uyumluluğu

Eklenti, web panelindeki **JSON İndir** düğmesiyle oluşturulan zarf biçimini okuyabilir. Yüklenen JSON, kaydedilirken kayıpsız olarak korunur. Yerel çekirdek şeması da ayrıca desteklenir.

## Sonraki adımlar

1. Döşeme/dolgu sınıflandırmalarından alan kalemleri oluşturma
2. Üretilen PNG paftayı aktif Archicad Layout'a otomatik yerleştirme

Resmî temel: https://github.com/GRAPHISOFT/archicad-addon-cmake
